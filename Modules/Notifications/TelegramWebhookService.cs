using System.Text;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using AgentAI.Modules.KnowledgeBase;
using AgentAI.Modules.ServiceNow;
using AgentAI.Modules.Messages;
using AgentAI.Modules.Tickets;
using AgentAI.Modules.Messages.Dto;
using AgentAI.Modules.AgentActions;

namespace AgentAI.Modules.Notifications;

public sealed partial class TelegramWebhookService : ITelegramWebhookService
{
    private static readonly ConcurrentDictionary<string, PendingTicketQuestion> PendingQuestions = new();
    private static readonly ConcurrentDictionary<string, TicketIntakeState> IntakeStates = new();

    private readonly IServiceNowConnector _serviceNow;
    private readonly IKnowledgeBaseService _knowledgeBase;
    private readonly IConfiguration _configuration;
    private readonly ITelegramMessageSender _messageSender;
    private readonly IIncomingMessageService _incomingMessageService;
    private readonly ITicketService _ticketService;
    private readonly IAgentIntakeInvoker _agentIntakeInvoker;
    private readonly IAgentActionInvoker _agentActionInvoker;
    private readonly ILogger<TelegramWebhookService> _logger;

    public TelegramWebhookService(
        IServiceNowConnector serviceNow,
        IKnowledgeBaseService knowledgeBase,
        IConfiguration configuration,
        ITelegramMessageSender messageSender,
        IIncomingMessageService incomingMessageService,
        ITicketService ticketService,
        IAgentIntakeInvoker agentIntakeInvoker,
        IAgentActionInvoker agentActionInvoker,
        ILogger<TelegramWebhookService> logger)
    {
        _serviceNow = serviceNow;
        _knowledgeBase = knowledgeBase;
        _configuration = configuration;
        _messageSender = messageSender;
        _incomingMessageService = incomingMessageService;
        _ticketService = ticketService;
        _agentIntakeInvoker = agentIntakeInvoker;
        _agentActionInvoker = agentActionInvoker;
        _logger = logger;
    }

    public async Task HandleAsync(TelegramUpdate update, CancellationToken ct = default)
    {
        var message = update.Message;
        if (message?.Chat is null)
            return;

        var chatId = message.Chat.Id.ToString();
        var text = message.Text?.Trim();

        _logger.LogInformation(
            "Telegram webhook received message. ChatId={ChatId} Text={Text}",
            chatId,
            string.IsNullOrWhiteSpace(text) ? "(empty)" : text);

        if (string.IsNullOrWhiteSpace(text))
        {
            await _messageSender.SendToChatAsync(chatId, "Enviame el numero de ticket, por ejemplo INC0010024.", ct: ct);
            return;
        }

        if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            await _messageSender.SendToChatAsync(chatId, "Hola, soy AgentAI. Enviame tu numero de ticket, por ejemplo INC0010024, y busco el estado del caso.", ct: ct);
            return;
        }

        if (PendingQuestions.TryGetValue(chatId, out var pending))
        {
            await HandlePendingAnswerAsync(chatId, pending, text, ct);
            return;
        }

        var ticketNumber = ExtractTicketNumber(text);
        if (ticketNumber is null)
        {
            await _messageSender.SendToChatAsync(chatId, "No pude identificar un numero de ticket. Enviame algo con INC, por ejemplo INC0010024 o inc10024.", ct: ct);
            return;
        }

        try
        {
            var incident = await _serviceNow.GetIncidentByNumberAsync(ticketNumber, ct);
            var response = incident is null
                ? $"No encontre el ticket {ticketNumber}. Verifica el numero e intenta nuevamente."
                : await BuildNextStepAsync(chatId, incident, ct);

            var result = await _messageSender.SendToChatAsync(chatId, response, ct: ct);
            _logger.LogInformation(
                "Telegram ticket lookup response sent. ChatId={ChatId} TicketNumber={TicketNumber} Sent={Sent} Message={Message}",
                chatId,
                ticketNumber,
                result.Sent,
                result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram webhook failed while processing ticket {TicketNumber}.", ticketNumber);
            await _messageSender.SendToChatAsync(chatId, "No pude consultar ServiceNow en este momento. Intenta nuevamente mas tarde.", ct: ct);
        }
    }

    private async Task HandlePendingAnswerAsync(
        string chatId,
        PendingTicketQuestion pending,
        string answer,
        CancellationToken ct)
    {
        try
        {
            var incident = await _serviceNow.GetIncidentAsync(pending.SysId, ct);
            if (incident is not null && IsFinalState(incident))
            {
                PendingQuestions.TryRemove(chatId, out _);
                IntakeStates.TryRemove(chatId, out _);
                await _messageSender.SendToChatAsync(chatId, BuildFinalStateResponse(incident), ct: ct);
                return;
            }

            var enrichedIncident = incident is null
                ? null
                : EnrichIncidentWithAnswer(incident, pending.Field, answer);

            var note = pending.Field switch
            {
                MissingTicketField.Description => $"Informacion agregada por Telegram: descripcion del problema: {answer}",
                MissingTicketField.System => $"Informacion agregada por Telegram: sistema afectado: {answer}",
                MissingTicketField.Email => $"Informacion agregada por Telegram: email/contacto del usuario: {answer}",
                _ => $"Informacion agregada por Telegram: {answer}"
            };

            var savedInServiceNow = true;
            try
            {
                await _serviceNow.AddCustomerCommentAsync(pending.SysId, note, ct);
                await _serviceNow.AddWorkNoteAsync(pending.SysId, $"AgentAI recibio informacion faltante por Telegram para {pending.Number}. Campo: {pending.Field}.", ct);
            }
            catch (Exception ex)
            {
                savedInServiceNow = false;
                _logger.LogWarning(ex, "Telegram webhook could not save pending answer for ticket {TicketNumber}, continuing analysis with in-memory answer.", pending.Number);
            }

            var state = IntakeStates.GetOrAdd(
                chatId,
                _ => new TicketIntakeState(pending.Number, pending.SysId));
            state.MarkCollected(pending.Field);

            PendingQuestions.TryRemove(chatId, out _);

            var response = $"Gracias. Guarde la informacion en el ticket {pending.Number}.";

            if (enrichedIncident is not null)
            {
                try
                {
                    response = await BuildNextStepAsync(chatId, enrichedIncident, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Telegram webhook saved pending answer but failed while analyzing ticket {TicketNumber}.", pending.Number);
                    response = $"Gracias. Guarde la informacion en el ticket {pending.Number}, pero no pude completar el analisis automatico para decidir si continua o se escala. Revisa la API/KB y vuelve a intentar.";
                }
            }

            if (!savedInServiceNow)
            {
                response += "\n\nAviso: pude analizar tu respuesta, pero no pude guardar ese comentario en ServiceNow. Revisa permisos/conexion de ServiceNow.";
            }

            await _messageSender.SendToChatAsync(chatId, response, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram webhook failed while saving pending answer for ticket {TicketNumber}.", pending.Number);
            await _messageSender.SendToChatAsync(chatId, "No pude guardar esa informacion en ServiceNow. Intenta nuevamente mas tarde.", ct: ct);
        }
    }

    private static ServiceNowIncident EnrichIncidentWithAnswer(
        ServiceNowIncident incident,
        MissingTicketField field,
        string answer)
    {
        var description = field switch
        {
            MissingTicketField.Description => $"{incident.Description}\n\nInformacion agregada por Telegram: descripcion del problema: {answer}",
            MissingTicketField.System => $"{incident.Description}\n\nInformacion agregada por Telegram: sistema afectado: {answer}",
            MissingTicketField.Email => $"{incident.Description}\n\nInformacion agregada por Telegram: email/contacto del usuario: {answer}",
            _ => $"{incident.Description}\n\nInformacion agregada por Telegram: {answer}"
        };

        return incident with
        {
            Description = description.Trim(),
            UpdatedAt = DateTime.UtcNow
        };
    }

    private async Task<string> BuildNextStepAsync(string chatId, ServiceNowIncident incident, CancellationToken ct)
    {
        if (IsFinalState(incident))
        {
            PendingQuestions.TryRemove(chatId, out _);
            IntakeStates.TryRemove(chatId, out _);
            return BuildFinalStateResponse(incident);
        }

        var ticket = await _ticketService.GetBySysIdAsync(incident.SysId, ct);
        if (ticket is null)
        {
            ticket = await _ticketService.SyncIncidentAsync(incident, ct);
        }

        try
        {
            await _incomingMessageService.ProcessIncomingAsync(new IncomingMessageRequest(
                TicketId: ticket.Id,
                SysId: incident.SysId, // TODO: replace with Telegram MessageId once available on TelegramUpdate
                ConversationSysId: chatId,
                SenderType: "customer",
                SenderName: chatId, // TODO: replace with Telegram user's real name once available
                Body: $"{incident.Title} {incident.Description}".Trim(),
                MessageType: "user_message",
                SentAt: DateTime.UtcNow
            ), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist Telegram conversation for ticket {TicketNumber}. Continuing with analysis.", incident.Number);
        }

        return await AnalyzeAndRouteAsync(chatId, incident, ct);
    }

    private async Task<string> AnalyzeAndRouteAsync(string chatId, ServiceNowIncident incident, CancellationToken ct)
    {
        var intakeResult = await _agentIntakeInvoker.AnalyzeAsync(incident, ct);
        if (!intakeResult.Succeeded || intakeResult.Decision is null)
        {
            var error = intakeResult.Error ?? "AgenteEntrada no devolvio una decision valida.";
            _logger.LogWarning("AgenteEntrada failed for ticket {TicketNumber}. Error={Error}", incident.Number, error);
            return $"{BuildTicketSummary(incident)}\n\nDecision: ESCALAR\nMotivo: No pude ejecutar AgenteEntrada para validar el caso con KB. Detalle: {error}";
        }

        var decision = intakeResult.Decision;
        if (decision.Decision.Equals("pedir_info", StringComparison.OrdinalIgnoreCase))
        {
            var missingField = ParseMissingField(decision.MissingField);
            if (missingField is null)
                return $"{BuildTicketSummary(incident)}\n\nDecision: ESCALAR\nMotivo: AgenteEntrada pidio informacion, pero no indico que campo falta.";

            PendingQuestions[chatId] = new PendingTicketQuestion(incident.Number, incident.SysId, missingField.Value);
            return string.IsNullOrWhiteSpace(decision.Question)
                ? BuildMissingFieldQuestion(incident.Number, missingField.Value)
                : decision.Question;
        }

        if (decision.Decision.Equals("escalar", StringComparison.OrdinalIgnoreCase))
        {
            var reason = string.IsNullOrWhiteSpace(decision.Reason)
                ? "AgenteEntrada determino que no hay solucion segura por KB."
                : decision.Reason;
            var customerMessage = $"Revise tu ticket {incident.Number}. Por el tipo de caso, lo voy a derivar a soporte especializado. Motivo: {reason}";
            var workNote = $"AgentAI derivo el ticket {incident.Number} desde Telegram. Motivo: {reason}";
            var assignmentGroupSysId = _configuration["ServiceNow:EscalationAssignmentGroupSysId"];
            var updatedServiceNow = true;

            try
            {
                if (string.IsNullOrWhiteSpace(assignmentGroupSysId))
                {
                    await _serviceNow.AddWorkNoteAsync(incident.SysId, workNote, ct);
                    await _serviceNow.AddCustomerCommentAsync(incident.SysId, customerMessage, ct);
                }
                else
                {
                    await _serviceNow.EscalateIncidentAsync(
                        incident.SysId,
                        assignmentGroupSysId,
                        workNote,
                        customerMessage,
                        ct);
                }
            }
            catch (Exception ex)
            {
                updatedServiceNow = false;
                _logger.LogWarning(ex, "Could not write escalation decision to ServiceNow for ticket {TicketNumber}.", incident.Number);
            }

            var response = new StringBuilder();
            response.AppendLine(BuildTicketSummary(incident));
            response.AppendLine();
            response.AppendLine("Decision: ESCALAR");
            response.AppendLine($"Motivo: {reason}");
            response.AppendLine(updatedServiceNow
                ? "El ticket fue derivado a soporte especializado."
                : "No pude actualizar ServiceNow con la escalacion, pero la decision es escalar.");
            return response.ToString().Trim();
        }

        var articleCode = string.IsNullOrWhiteSpace(decision.ArticleCode) ? "KB" : decision.ArticleCode;
        var recommendedAction = decision.RecommendedAction ?? decision.Action ?? string.Empty;
        var continueMessage = $"Revise tu ticket {incident.Number} y AgenteEntrada encontro una solucion aplicable en la KB ({articleCode}). AgentAI puede continuar con el caso.";
        var continueNote = $"AgenteEntrada encontro KB aplicable para {incident.Number}: {articleCode}. Accion recomendada: {recommendedAction}";

        var markedInProgress = true;
        try
        {
            await _serviceNow.MarkInProgressAsync(incident.SysId, continueNote, continueMessage, ct);
        }
        catch (Exception ex)
        {
            markedInProgress = false;
            _logger.LogWarning(ex, "Could not write continue decision to ServiceNow for ticket {TicketNumber}.", incident.Number);
        }

        var builder = new StringBuilder();
        builder.AppendLine(BuildTicketSummary(incident));
        builder.AppendLine();
        builder.AppendLine(decision.Decision.Equals("ejecutar_accion", StringComparison.OrdinalIgnoreCase)
            ? "Decision: EJECUTAR_ACCION"
            : "Decision: CONTINUAR");
        builder.AppendLine($"KB: {articleCode}");
        builder.AppendLine($"Confianza: {decision.Confidence}");
        builder.AppendLine($"Accion recomendada: {recommendedAction}");

        if (!string.IsNullOrWhiteSpace(decision.SuggestedUserMessage))
            builder.AppendLine($"Mensaje sugerido: {decision.SuggestedUserMessage}");

        if (!markedInProgress)
            builder.AppendLine("Aviso: no pude actualizar ServiceNow con esta decision, pero el analisis de KB se completo.");

        if (decision.Decision.Equals("ejecutar_accion", StringComparison.OrdinalIgnoreCase))
        {
            var email = decision.User ?? ExtractEmail(incident);
            if (string.IsNullOrWhiteSpace(email))
            {
                builder.AppendLine();
                builder.AppendLine("Me falta el email registrado en la turnera para ejecutar el agente de acceso.");
                return builder.ToString().Trim();
            }

            var agent = string.IsNullOrWhiteSpace(decision.Agent) ? "AgenteAccionAcceso" : decision.Agent;
            var action = string.IsNullOrWhiteSpace(decision.Action) ? "resetear_acceso" : decision.Action;
            var diagnostic = $"DELEGAR_A: {agent} | TICKET: {incident.Number} | ACCION: {action} | USUARIO: {email}";
            var actionResult = await _agentActionInvoker.ExecuteAsync(diagnostic, ct);

            builder.AppendLine();
            builder.AppendLine("Ejecucion del agente:");
            builder.AppendLine(actionResult.Success
                ? ExtractAgentSummary(actionResult.Output)
                : $"No pude ejecutar AgenteAccion. Motivo: {actionResult.Error}");
        }

        return builder.ToString().Trim();
    }

    private static TicketIntakeState GetOrCreateIntakeState(string chatId, ServiceNowIncident incident)
    {
        var state = IntakeStates.AddOrUpdate(
            chatId,
            _ => TicketIntakeState.FromIncident(incident),
            (_, current) => current.Number.Equals(incident.Number, StringComparison.OrdinalIgnoreCase)
                ? current
                : TicketIntakeState.FromIncident(incident));

        return state;
    }

    private static MissingTicketField? GetMissingField(ServiceNowIncident incident, TicketIntakeState state)
    {
        var hasDescription = state.HasDescription || HasMeaningfulDescription(incident);
        var hasSystem = state.HasSystem || CanInferSystem(incident);
        var hasEmail = state.HasEmail || HasUsableEmail(incident);

        if (hasDescription && ContainsHighRiskSignal($"{incident.Title} {incident.Description}".ToLowerInvariant()))
            return null;

        if (hasDescription && hasSystem && hasEmail)
            return null;

        if (!hasDescription)
            return MissingTicketField.Description;

        if (!hasSystem)
            return MissingTicketField.System;

        if (!hasEmail)
            return MissingTicketField.Email;

        return null;
    }

    internal static bool HasMeaningfulDescription(ServiceNowIncident incident)
    {
        var text = $"{incident.Title} {incident.Description}".Trim();

        if (text.Length < 25)
            return false;

        var normalized = text.ToLowerInvariant();
        if (normalized is "prueba" or "test")
            return false;

        if (normalized.Contains("informacion agregada por telegram: sistema afectado") &&
            !normalized.Contains("descripcion del problema"))
        {
            return false;
        }

        return normalized.Contains("no puedo") ||
            normalized.Contains("error") ||
            normalized.Contains("problema") ||
            normalized.Contains("falla") ||
            normalized.Contains("bloque") ||
            normalized.Contains("rechaz") ||
            normalized.Contains("incorrect") ||
            normalized.Contains("pendiente") ||
            normalized.Contains("no funciona") ||
            normalized.Contains("descripcion del problema");
    }

    private static string InferSystem(ServiceNowIncident incident)
    {
        var text = NormalizeForMatching($"{incident.Title} {incident.Description}");

        if (text.Contains("turnera"))
            return "turnera";

        if (text.Contains("usuario") ||
            text.Contains("login") ||
            text.Contains("sesion") ||
            text.Contains("credencial") ||
            text.Contains("contrasena") ||
            text.Contains("password") ||
            text.Contains("acceso"))
            return "usuarios";
        if (text.Contains("pedido") || text.Contains("ord-"))
            return "pedidos";
        if (text.Contains("pago") || text.Contains("tarjeta") || text.Contains("debito") || text.Contains("cobro") || text.Contains("cargo"))
            return "pagos";
        if (text.Contains("catalogo") || text.Contains("precio"))
            return "catalogo";
        if (text.Contains("stock") || text.Contains("inventario"))
            return "stock";

        return string.Empty;
    }

    private static bool ShouldEscalate(
        ServiceNowIncident incident,
        KnowledgeBaseSearchResult? article,
        out string reason)
    {
        var text = $"{incident.Title} {incident.Description}".ToLowerInvariant();

        if (ContainsHighRiskSignal(text))
        {
            reason = "El caso contiene senales de riesgo, fraude, seguridad o impacto economico que requieren revision humana.";
            return true;
        }

        if (article is null)
        {
            reason = "No se encontro un articulo de KB aplicable para resolver el caso con seguridad.";
            return true;
        }

        if (article.Confidence.Equals("baja", StringComparison.OrdinalIgnoreCase))
        {
            reason = $"La KB encontrada ({article.ArticleCode}) tiene confianza baja.";
            return true;
        }

        if (string.IsNullOrWhiteSpace(article.RecommendedAction))
        {
            reason = $"La KB encontrada ({article.ArticleCode}) no tiene una accion recomendada clara.";
            return true;
        }

        if (EscalationCriteriaApplies(text, article.EscalationCriteria))
        {
            reason = $"La KB {article.ArticleCode} indica criterios de escalacion aplicables.";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static KnowledgeBaseSearchResult? TryBuildFallbackArticle(string query, string system)
    {
        var text = $"{system} {query}".ToLowerInvariant();

        if (text.Contains("login") ||
            text.Contains("sesion") ||
            text.Contains("contraseña") ||
            text.Contains("password") ||
            text.Contains("acceso"))
        {
            return new KnowledgeBaseSearchResult(
                ArticleId: 0,
                ArticleCode: "KB-FALLBACK-USUARIOS-001",
                System: "usuarios",
                SystemType: "usuarios",
                Tags: "login, acceso, contraseña",
                Actions: "resetear_acceso",
                Description: "Problemas comunes de acceso o contraseña no reconocida.",
                Symptoms: "El usuario no puede iniciar sesion o la contraseña no es reconocida.",
                ProbableCause: "Credenciales vencidas, bloqueadas o contraseña incorrecta.",
                RequiredData: "Usuario/email o identificador de cuenta.",
                Preconditions: "Caso sin señales de fraude o acceso no autorizado.",
                RecommendedAction: "Resetear acceso del usuario y enviar link de recuperacion.",
                Validation: "Confirmar que el usuario puede iniciar sesion luego del reseteo.",
                ExpectedResult: "Usuario recupera el acceso.",
                EscalationCriteria: "Escalar si hay indicios de cuenta comprometida, compras no reconocidas o datos modificados sin permiso.",
                SuggestedUserMessage: "Tu caso parece ser un problema de acceso. Vamos a resetear tu acceso y enviarte un link de recuperacion.",
                Confidence: "media");
        }

        return null;
    }

    private static bool ContainsHighRiskSignal(string text)
    {
        var signals = new[]
        {
            "fraude",
            "no reconozco",
            "no reconoci",
            "compra que no hice",
            "compras que no hice",
            "cargo desconocido",
            "cargos desconocidos",
            "doble cobro",
            "cobraron dos veces",
            "tarjeta robada",
            "accedio sin permiso",
            "accedieron sin permiso",
            "cuenta comprometida",
            "hackearon",
            "suplantacion",
            "datos sensibles",
            "denuncia"
        };

        return signals.Any(text.Contains);
    }

    private static bool EscalationCriteriaApplies(string text, string escalationCriteria)
    {
        if (string.IsNullOrWhiteSpace(escalationCriteria))
            return false;

        var criteria = escalationCriteria.ToLowerInvariant();
        return ContainsHighRiskSignal(text) ||
            criteria.Contains("siempre escalar") ||
            criteria.Contains("escalar siempre") ||
            criteria.Contains("requiere soporte humano");
    }

    internal static bool CanInferSystem(ServiceNowIncident incident)
    {
        var text = $"{incident.Title} {incident.Description}".ToLowerInvariant();
        return text.Contains("usuario") ||
            text.Contains("login") ||
            text.Contains("sesion") ||
            text.Contains("pedido") ||
            text.Contains("ord-") ||
            text.Contains("pago") ||
            text.Contains("tarjeta") ||
            text.Contains("catalogo") ||
            text.Contains("precio") ||
            text.Contains("sku") ||
            text.Contains("stock") ||
            text.Contains("inventario");
    }

    internal static bool HasUsableEmail(ServiceNowIncident incident)
        => !string.IsNullOrWhiteSpace(incident.CreatedByEmail) ||
            EmailRegex().IsMatch($"{incident.Title} {incident.Description}");

    private static string? ExtractEmail(ServiceNowIncident incident)
    {
        if (!string.IsNullOrWhiteSpace(incident.CreatedByEmail))
            return incident.CreatedByEmail.Trim();

        var match = EmailRegex().Match($"{incident.Title} {incident.Description}");
        return match.Success ? match.Value : null;
    }

    private static bool ShouldInvokeAccessAgent(KnowledgeBaseSearchResult article)
    {
        var text = $"{article.Actions} {article.RecommendedAction} {article.Tags} {article.System} {article.Description}".ToLowerInvariant();
        return text.Contains("resetear_acceso") ||
            (text.Contains("reset") && (text.Contains("acceso") || text.Contains("login") || text.Contains("sesion")));
    }

    private static string NormalizeForMatching(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string ExtractAgentSummary(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return "AgenteAccion termino sin devolver detalle.";

        var lines = output
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith("Conectando al MCP", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.StartsWith("===", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.StartsWith("Pega", StringComparison.OrdinalIgnoreCase) && !line.StartsWith("Peg", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.StartsWith("Diagn", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.StartsWith("Sesion", StringComparison.OrdinalIgnoreCase) && !line.StartsWith("Sesi", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return lines.Count == 0 ? output.Trim() : string.Join(Environment.NewLine, lines.TakeLast(6));
    }

    private static string BuildMissingFieldQuestion(string ticketNumber, MissingTicketField field)
        => field switch
        {
            MissingTicketField.Description => $"Encontre el ticket {ticketNumber}, pero falta una descripcion clara. Contame que problema tenes y que estabas intentando hacer.",
            MissingTicketField.System => $"Encontre el ticket {ticketNumber}. Para derivarlo bien, decime que sistema esta afectado: usuarios, pedidos, pagos, catalogo o stock.",
            MissingTicketField.Email => $"Encontre el ticket {ticketNumber}. Para operar sobre tu usuario de la turnera, decime el email con el que estas registrado.",
            _ => $"Encontre el ticket {ticketNumber}. Me falta un dato mas para poder derivarlo."
        };

    private static MissingTicketField? ParseMissingField(string? field)
    {
        if (string.IsNullOrWhiteSpace(field))
            return null;

        return field.Trim().ToLowerInvariant() switch
        {
            "description" or "descripcion" => MissingTicketField.Description,
            "system" or "sistema" => MissingTicketField.System,
            "email" or "usuario" or "user" => MissingTicketField.Email,
            _ => null
        };
    }

    private static bool IsFinalState(ServiceNowIncident incident)
        => incident.State is 4 or 5 or 6 ||
            incident.StateLabel.Equals("Resolved", StringComparison.OrdinalIgnoreCase) ||
            incident.StateLabel.Equals("Closed", StringComparison.OrdinalIgnoreCase) ||
            incident.StateLabel.Equals("Canceled", StringComparison.OrdinalIgnoreCase) ||
            incident.ResolvedAt is not null;

    private static string BuildFinalStateResponse(ServiceNowIncident incident)
    {
        var builder = new StringBuilder();
        builder.AppendLine(BuildTicketSummary(incident));
        builder.AppendLine();
        builder.AppendLine("Este ticket ya esta finalizado. No voy a iniciar nuevamente el flujo automatico ni ejecutar acciones sobre la turnera.");

        if (incident.ResolvedAt is not null)
            builder.AppendLine($"Fecha de resolucion: {incident.ResolvedAt:yyyy-MM-dd HH:mm} UTC");

        return builder.ToString().Trim();
    }

    private static string? ExtractTicketNumber(string text)
    {
        var match = TicketNumberRegex().Match(text);
        return match.Success
            ? string.Concat(match.Value.ToUpperInvariant().Where(char.IsLetterOrDigit))
            : null;
    }

    private static string BuildTicketSummary(ServiceNowIncident incident)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Ticket {incident.Number}");
        builder.AppendLine($"Estado: {incident.StateLabel}");
        builder.AppendLine($"Prioridad: {incident.PriorityLabel}");

        if (!string.IsNullOrWhiteSpace(incident.Title))
            builder.AppendLine($"Asunto: {incident.Title}");

        if (incident.UpdatedAt is not null)
            builder.AppendLine($"Ultima actualizacion: {incident.UpdatedAt:yyyy-MM-dd HH:mm} UTC");

        if (!string.IsNullOrWhiteSpace(incident.Description))
        {
            var description = incident.Description.Length > 500
                ? incident.Description[..500] + "..."
                : incident.Description;
            builder.AppendLine();
            builder.AppendLine(description);
        }

        return builder.ToString().Trim();
    }

    [GeneratedRegex(@"\bINC[\s-]*\d{4,12}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TicketNumberRegex();

    [GeneratedRegex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();
}

public sealed record PendingTicketQuestion(
    string Number,
    string SysId,
    MissingTicketField Field);

public enum MissingTicketField
{
    Description,
    System,
    Email
}

public sealed class TicketIntakeState
{
    public TicketIntakeState(string number, string sysId)
    {
        Number = number;
        SysId = sysId;
    }

    public string Number { get; }
    public string SysId { get; }
    public bool HasDescription { get; private set; }
    public bool HasSystem { get; private set; }
    public bool HasEmail { get; private set; }

    public static TicketIntakeState FromIncident(ServiceNowIncident incident)
    {
        var state = new TicketIntakeState(incident.Number, incident.SysId)
        {
            HasDescription = TelegramWebhookService.HasMeaningfulDescription(incident),
            HasSystem = TelegramWebhookService.CanInferSystem(incident),
            HasEmail = TelegramWebhookService.HasUsableEmail(incident)
        };

        return state;
    }

    public void MarkCollected(MissingTicketField field)
    {
        switch (field)
        {
            case MissingTicketField.Description:
                HasDescription = true;
                break;
            case MissingTicketField.System:
                HasSystem = true;
                break;
            case MissingTicketField.Email:
                HasEmail = true;
                break;
        }
    }
}
