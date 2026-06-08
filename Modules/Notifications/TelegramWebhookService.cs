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
using AgentAI.Modules.Tickets.Dto;
using AgentAI.Modules.AgentRuns;
using AgentAI.Modules.AgentRuns.Dto;
using AgentAI.Modules.AgentSteps;
using AgentAI.Modules.AgentSteps.Dto;

namespace AgentAI.Modules.Notifications;

public sealed partial class TelegramWebhookService : ITelegramWebhookService
{
    private static readonly ConcurrentDictionary<string, PendingTicketQuestion> PendingQuestions = new();
    private static readonly ConcurrentDictionary<string, PendingTicketClosure> PendingClosures = new();
    private static readonly ConcurrentDictionary<string, ActiveTicketFlow> ActiveTicketFlows = new();
    private static readonly ConcurrentDictionary<string, TicketIntakeState> IntakeStates = new();

    private readonly IServiceNowConnector _serviceNow;
    private readonly IKnowledgeBaseService _knowledgeBase;
    private readonly IConfiguration _configuration;
    private readonly ITelegramMessageSender _messageSender;
    private readonly IIncomingMessageService _incomingMessageService;
    private readonly ITicketService _ticketService;
    private readonly IAgentIntakeInvoker _agentIntakeInvoker;
    private readonly IAgentActionInvoker _agentActionInvoker;
    private readonly IAgentRunService _agentRunService;
    private readonly IAgentStepService _agentStepService;
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
        IAgentRunService agentRunService,
        IAgentStepService agentStepService,
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
        _agentRunService = agentRunService;
        _agentStepService = agentStepService;
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

        if (PendingClosures.TryGetValue(chatId, out var pendingClosure))
        {
            await HandleClosureConfirmationAsync(chatId, pendingClosure, text, ct);
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

        if (ActiveTicketFlows.TryGetValue(chatId, out var activeFlow) &&
            !activeFlow.Number.Equals(ticketNumber, StringComparison.OrdinalIgnoreCase))
        {
            await _messageSender.SendToChatAsync(
                chatId,
                $"Todavia tengo en curso el ticket {activeFlow.Number}. Primero cerremos ese flujo: responde la informacion pendiente, SI para cerrar, o NO si sigue fallando. Despues vemos {ticketNumber}.",
                ct: ct);
            return;
        }

        try
        {
            var incident = await _serviceNow.GetIncidentByNumberAsync(ticketNumber, ct);
            var response = incident is null
                ? $"No encontre el ticket {ticketNumber}. Verifica el numero e intenta nuevamente."
                : ShouldOnlyReportStatus(incident)
                    ? BuildStatusOnlyResponse(incident)
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

    private async Task HandleClosureConfirmationAsync(
        string chatId,
        PendingTicketClosure pending,
        string answer,
        CancellationToken ct)
    {
        var normalized = NormalizeForMatching(answer);

        if (IsPositiveConfirmation(normalized))
        {
            try
            {
                var closeNotes = BuildTelegramCloseNotes(pending);
                var workNote = $"AgentAI cierra {pending.Number} despues de confirmacion explicita del usuario por Telegram.";
                await _serviceNow.ResolveIncidentAsync(pending.SysId, closeNotes, workNote, ct: ct);
                await MarkLocalTicketResolvedAsync(pending, ct);
                PendingClosures.TryRemove(chatId, out _);
                ActiveTicketFlows.TryRemove(chatId, out _);
                IntakeStates.TryRemove(chatId, out _);
                await _messageSender.SendToChatAsync(chatId, $"Gracias por confirmar. Cerre el ticket {pending.Number} como resuelto.", ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not close ticket {TicketNumber} after Telegram confirmation.", pending.Number);
                await _messageSender.SendToChatAsync(chatId, $"Recibi tu confirmacion, pero no pude cerrar el ticket {pending.Number} en ServiceNow. Intenta nuevamente mas tarde.", ct: ct);
            }

            return;
        }

        if (IsNegativeConfirmation(normalized))
        {
            var escalated = false;
            var reason = $"El usuario indico por Telegram que el problema del ticket {pending.Number} persiste despues de la accion automatica. Se deriva a soporte especializado.";
            try
            {
                var customerMessage = "Indicaste por Telegram que el problema sigue. Derivamos el ticket a soporte especializado para seguimiento.";
                var assignmentGroupSysId = _configuration["ServiceNow:EscalationAssignmentGroupSysId"];

                if (string.IsNullOrWhiteSpace(assignmentGroupSysId))
                {
                    await _serviceNow.MarkInProgressAsync(pending.SysId, reason, customerMessage, ct);
                }
                else
                {
                    await _serviceNow.EscalateIncidentAsync(
                        pending.SysId,
                        assignmentGroupSysId,
                        reason,
                        customerMessage,
                        ct);
                    await MarkLocalTicketEscalatedAsync(pending.SysId, reason, ct);
                    escalated = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not write negative closure confirmation for ticket {TicketNumber}.", pending.Number);
            }

            PendingClosures.TryRemove(chatId, out _);
            ActiveTicketFlows.TryRemove(chatId, out _);
            await _messageSender.SendToChatAsync(
                chatId,
                escalated
                    ? $"Entendido. Derive el ticket {pending.Number} a soporte especializado para que revisen el caso."
                    : $"Entendido. Dejo el ticket {pending.Number} abierto para seguimiento, pero no pude asignarlo automaticamente al grupo especializado.",
                ct: ct);
            return;
        }

        await _messageSender.SendToChatAsync(chatId, $"Tengo pendiente confirmar si el ticket {pending.Number} quedo resuelto. Respondeme SI para cerrarlo o NO si el problema sigue.", ct: ct);
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
                PendingClosures.TryRemove(chatId, out _);
                ActiveTicketFlows.TryRemove(chatId, out _);
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
            state.MarkCollected(pending.Field, answer);

            PendingQuestions.TryRemove(chatId, out _);

            var response = $"Gracias. Guarde la informacion en el ticket {pending.Number}.";

            if (enrichedIncident is not null)
            {
                try
                {
                    response = await BuildNextStepAsync(chatId, state.Enrich(enrichedIncident), ct);
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
            PendingClosures.TryRemove(chatId, out _);
            ActiveTicketFlows.TryRemove(chatId, out _);
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

        return await AnalyzeAndRouteAsync(chatId, incident, ticket.Id, ct);
    }

    private async Task<string> AnalyzeAndRouteAsync(string chatId, ServiceNowIncident incident, int ticketId, CancellationToken ct)
    {
        ActiveTicketFlows[chatId] = new ActiveTicketFlow(incident.Number, incident.SysId);
        var run = await _agentRunService.CreateAsync(new CreateAgentRunRequest(ticketId), ct);
        var intakeResult = await _agentIntakeInvoker.AnalyzeAsync(incident, ct);
        var intakeStepStatus = intakeResult.Succeeded && intakeResult.Decision is not null
            ? (intakeResult.Decision.Decision.Equals("escalar", StringComparison.OrdinalIgnoreCase) ? "failed" : "completed")
            : "failed";
        await RecordAgentStepAsync(
            run.Id,
            "AgenteEntrada",
            $"{incident.Number} | {incident.Title}",
            "Analizar ticket, sistema afectado y KB",
            intakeStepStatus,
            intakeResult.Succeeded
                ? intakeResult.Output
                : intakeResult.Error ?? "AgenteEntrada fallo sin detalle.",
            ct);

        if (!intakeResult.Succeeded || intakeResult.Decision is null)
        {
            var error = intakeResult.Error ?? "AgenteEntrada no devolvio una decision valida.";
            _logger.LogWarning("AgenteEntrada failed for ticket {TicketNumber}. Error={Error}", incident.Number, error);
            await _agentRunService.UpdateStatusAsync(run.Id, new UpdateAgentRunStatusRequest("failed"), ct);
            return $"{BuildTicketSummary(incident)}\n\nDecision: ESCALAR\nMotivo: No pude ejecutar AgenteEntrada para validar el caso con KB. Detalle: {error}";
        }

        var decision = intakeResult.Decision;
        await PersistAffectedSystemAsync(incident.SysId, decision.System, ct);

        if (decision.Decision.Equals("pedir_info", StringComparison.OrdinalIgnoreCase))
        {
            var missingField = ParseMissingField(decision.MissingField);
            if (missingField is null)
                return $"{BuildTicketSummary(incident)}\n\nDecision: ESCALAR\nMotivo: AgenteEntrada pidio informacion, pero no indico que campo falta.";

            var intakeState = GetOrCreateIntakeState(chatId, incident);
            if (intakeState.HasCollected(missingField.Value))
            {
                var reason = $"AgenteEntrada volvio a pedir {missingField.Value}, pero ese dato ya fue informado por Telegram. Se escala para revision humana.";
                var serviceNowUpdated = await EscalateInServiceNowAsync(
                    incident.SysId,
                    incident.Number,
                    reason,
                    $"No pude completar el analisis automatico del ticket {incident.Number}. Lo derivo a soporte especializado para revision.",
                    ct);

                await _agentRunService.UpdateStatusAsync(run.Id, new UpdateAgentRunStatusRequest("failed"), ct);
                await MarkLocalTicketEscalatedAsync(incident.SysId, reason, ct);
                PendingQuestions.TryRemove(chatId, out _);
                PendingClosures.TryRemove(chatId, out _);
                ActiveTicketFlows.TryRemove(chatId, out _);

                return serviceNowUpdated
                    ? $"{BuildTicketSummary(incident)}\n\nDecision: ESCALAR\nMotivo: {reason}\nEl ticket fue derivado a soporte especializado."
                    : $"{BuildTicketSummary(incident)}\n\nDecision: ESCALAR\nMotivo: {reason}\nNo pude actualizar ServiceNow con la escalacion, pero la decision es escalar.";
            }

            PendingQuestions[chatId] = new PendingTicketQuestion(incident.Number, incident.SysId, missingField.Value);
            try
            {
                await _serviceNow.MarkOnHoldAsync(
                    incident.SysId,
                    $"AgentAI dejo {incident.Number} en espera porque necesita informacion del usuario por Telegram. Campo requerido: {missingField.Value}.",
                    "Necesitamos una informacion adicional para continuar con el analisis automatico del ticket.",
                    ct);
                await UpdateLocalTicketStateAsync(incident.SysId, 3, "En espera", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not mark ticket {TicketNumber} as On Hold while waiting for missing information.", incident.Number);
            }

            await _agentRunService.UpdateStatusAsync(run.Id, new UpdateAgentRunStatusRequest("completed"), ct);
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
                    await MarkLocalTicketEscalatedAsync(incident.SysId, reason, ct);
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
            ActiveTicketFlows.TryRemove(chatId, out _);
            PendingClosures.TryRemove(chatId, out _);
            PendingQuestions.TryRemove(chatId, out _);
            await _agentRunService.UpdateStatusAsync(run.Id, new UpdateAgentRunStatusRequest("failed"), ct);
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
            await UpdateLocalTicketStateAsync(incident.SysId, 2, "En proceso Nivel 1", ct);
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
                PendingQuestions[chatId] = new PendingTicketQuestion(incident.Number, incident.SysId, MissingTicketField.Email);
                builder.AppendLine();
                builder.AppendLine(BuildMissingEmailForActionMessage(incident.Number, decision.Action, decision.Agent));
                await _agentRunService.UpdateStatusAsync(run.Id, new UpdateAgentRunStatusRequest("completed"), ct);
                return builder.ToString().Trim();
            }

            var agent = string.IsNullOrWhiteSpace(decision.Agent) ? "AgenteAccionAcceso" : decision.Agent;
            var action = string.IsNullOrWhiteSpace(decision.Action) ? "resetear_acceso" : decision.Action;
            var detail = Truncate($"{incident.Title} {incident.Description}".Trim(), 1000);
            var diagnostic = $"DELEGAR_A: {agent} | TICKET: {incident.Number} | ACCION: {action} | USUARIO: {email} | DETALLE: {detail}";
            var actionResult = await _agentActionInvoker.ExecuteAsync(diagnostic, ct);
            await RecordAgentStepAsync(
                run.Id,
                agent,
                diagnostic,
                $"Ejecutar accion {action}",
                actionResult.Success ? "completed" : "failed",
                actionResult.Success
                    ? actionResult.Output
                    : actionResult.Error ?? "AgenteAccion fallo sin detalle.",
                ct);

            builder.AppendLine();
            builder.AppendLine("Ejecucion del agente:");
            var agentSummary = actionResult.Success
                ? ExtractAgentSummary(actionResult.Output)
                : $"No pude ejecutar AgenteAccion. Motivo: {actionResult.Error}";
            builder.AppendLine(agentSummary);

            if (actionResult.Success && ShouldAskForClosureConfirmation(agentSummary))
            {
                PendingClosures[chatId] = new PendingTicketClosure(
                    incident.Number,
                    incident.SysId,
                    BuildClosureActionSummary(agentSummary));

                try
                {
                    await _serviceNow.MarkOnHoldAsync(
                        incident.SysId,
                        $"AgentAI ejecuto la accion automatica para {incident.Number}. Queda esperando confirmacion del usuario por Telegram antes de cerrar.",
                        "Ejecutamos una accion automatica sobre tu caso. Queda pendiente tu confirmacion por Telegram para cerrar el ticket.",
                        ct);
                    await UpdateLocalTicketStateAsync(incident.SysId, 3, "En espera", ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not write pending closure note for ticket {TicketNumber}.", incident.Number);
                }

                builder.AppendLine();
                builder.AppendLine("Pudiste resolver el problema? Respondeme SI para cerrar el ticket o NO si sigue fallando.");
            }
        }

        if (!PendingClosures.ContainsKey(chatId) && !PendingQuestions.ContainsKey(chatId))
            ActiveTicketFlows.TryRemove(chatId, out _);

        await _agentRunService.UpdateStatusAsync(run.Id, new UpdateAgentRunStatusRequest("completed"), ct);
        return builder.ToString().Trim();
    }

    private async Task RecordAgentStepAsync(
        int runId,
        string agentType,
        string input,
        string prompt,
        string status,
        string output,
        CancellationToken ct)
    {
        try
        {
            var step = await _agentStepService.CreateAsync(new CreateAgentStepRequest(
                AgentRunId: runId,
                AgentType: agentType,
                InputData: Truncate(input, 2000),
                Prompt: Truncate(prompt, 1000)), ct);

            await _agentStepService.UpdateAsync(step.Id, new UpdateAgentStepRequest(
                Status: status,
                OutputData: Truncate(output, 4000)), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist agent step for run {RunId} and agent {AgentType}.", runId, agentType);
        }
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private async Task MarkLocalTicketResolvedAsync(PendingTicketClosure pending, CancellationToken ct)
    {
        var ticket = await _ticketService.GetBySysIdAsync(pending.SysId, ct);
        if (ticket is null)
            return;

        var resolvedAt = DateTime.UtcNow;
        await _ticketService.SyncIncidentAsync(new ServiceNowIncident(
            ticket.SysId,
            ticket.Number,
            ticket.Title,
            ticket.Description,
            State: 4,
            StateLabel: "Resuelto",
            ticket.Priority,
            ticket.PriorityLabel,
            ticket.CreatedByName,
            ticket.CreatedByEmail,
            ticket.AssignmentGroup,
            ticket.OpenedAt,
            UpdatedAt: resolvedAt,
            ResolvedAt: resolvedAt), ct);
    }

    private async Task<bool> EscalateInServiceNowAsync(
        string sysId,
        string ticketNumber,
        string reason,
        string customerMessage,
        CancellationToken ct)
    {
        try
        {
            var assignmentGroupSysId = _configuration["ServiceNow:EscalationAssignmentGroupSysId"];
            var workNote = $"AgentAI derivo el ticket {ticketNumber} desde Telegram. Motivo: {reason}";

            if (string.IsNullOrWhiteSpace(assignmentGroupSysId))
            {
                await _serviceNow.AddWorkNoteAsync(sysId, workNote, ct);
                await _serviceNow.AddCustomerCommentAsync(sysId, customerMessage, ct);
            }
            else
            {
                await _serviceNow.EscalateIncidentAsync(
                    sysId,
                    assignmentGroupSysId,
                    workNote,
                    customerMessage,
                    ct);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not escalate ServiceNow ticket {TicketNumber}.", ticketNumber);
            return false;
        }
    }

    private async Task MarkLocalTicketEscalatedAsync(string sysId, string reason, CancellationToken ct)
    {
        var ticket = await _ticketService.GetBySysIdAsync(sysId, ct);
        if (ticket is null)
            return;

        var escalatedGroup = _configuration["Metrics:EscalatedAssignmentGroup"] ?? "Soporte Nivel 2";
        await _ticketService.UpdateAsync(ticket.Id, new UpdateTicketRequest(
            Title: null,
            Description: string.IsNullOrWhiteSpace(ticket.Description)
                ? reason
                : ticket.Description,
            State: 2,
            StateLabel: "En proceso Nivel 2",
            Priority: null,
            PriorityLabel: null,
            AssignedTo: null,
            AssignmentGroup: escalatedGroup,
            AffectedSystem: null,
            ResolvedAt: null), ct);
    }

    private async Task UpdateLocalTicketStateAsync(string sysId, int state, string stateLabel, CancellationToken ct)
    {
        try
        {
            var ticket = await _ticketService.GetBySysIdAsync(sysId, ct);
            if (ticket is null)
                return;

            await _ticketService.UpdateAsync(ticket.Id, new UpdateTicketRequest(
                Title: null,
                Description: null,
                State: state,
                StateLabel: stateLabel,
                Priority: null,
                PriorityLabel: null,
                AssignedTo: null,
                AssignmentGroup: null,
                AffectedSystem: null,
                ResolvedAt: null), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not update local ticket state for SysId {SysId} to {StateLabel}.", sysId, stateLabel);
        }
    }

    private async Task PersistAffectedSystemAsync(string sysId, string? system, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(system))
            return;

        try
        {
            var ticket = await _ticketService.GetBySysIdAsync(sysId, ct);
            if (ticket is null)
                return;

            await _ticketService.UpdateAsync(ticket.Id, new UpdateTicketRequest(
                Title: null,
                Description: null,
                State: null,
                StateLabel: null,
                Priority: null,
                PriorityLabel: null,
                AssignedTo: null,
                AssignmentGroup: null,
                AffectedSystem: system,
                ResolvedAt: null), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist affected system for ticket sys_id {SysId}.", sysId);
        }
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
        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["usuarios"] = ScoreSystem(text,
                ("credenciales invalidas", 16), ("credencial", 12), ("login", 12), ("iniciar sesion", 12),
                ("sesion", 10), ("contrasena", 10), ("password", 10), ("acceso", 9), ("usuario", 5)),
            ["turnera"] = ScoreSystem(text,
                ("reserva", 10), ("reservas", 10), ("turno", 10), ("turnos", 10), ("turnera", 1)),
            ["pagos"] = ScoreSystem(text,
                ("pago", 12), ("pague", 14), ("abone", 14), ("me dieron", 12), ("me cargaron", 12),
                ("menos clases", 12), ("tarjeta", 7), ("debito", 7), ("cobro", 9), ("cargo", 9), ("credito", 10)),
            ["pedidos"] = ScoreSystem(text,
                ("pedido", 10), ("ord-", 10)),
            ["catalogo"] = ScoreSystem(text,
                ("catalogo", 10), ("precio", 10)),
            ["stock"] = ScoreSystem(text,
                ("stock", 10), ("inventario", 10))
        };

        var best = scores
            .Where(item => item.Value > 0)
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key)
            .FirstOrDefault();

        return best.Value >= 6 ? best.Key : string.Empty;
    }

    private static int ScoreSystem(string text, params (string Token, int Weight)[] weightedTokens)
        => weightedTokens.Where(item => text.Contains(item.Token)).Sum(item => item.Weight);

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
        var normalized = NormalizeForMatching(text);
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
            "alguien entro a mi cuenta",
            "entraron a mi cuenta",
            "ingresaron a mi cuenta",
            "ingreso a mi cuenta",
            "sin autorizacion",
            "sin mi autorizacion",
            "cuenta comprometida",
            "cuenta hackeada",
            "me hackearon",
            "hackearon",
            "suplantacion",
            "datos sensibles",
            "denuncia"
        };

        return signals.Any(normalized.Contains);
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

    private static string BuildTelegramCloseNotes(PendingTicketClosure pending)
    {
        var summary = string.IsNullOrWhiteSpace(pending.Summary)
            ? "Accion automatica ejecutada correctamente."
            : pending.Summary;

        return $"Usuario confirmo por Telegram que el ticket {pending.Number} quedo resuelto. {summary}";
    }

    private static string BuildClosureActionSummary(string agentSummary)
    {
        if (string.IsNullOrWhiteSpace(agentSummary))
            return "Accion automatica ejecutada correctamente.";

        var lines = agentSummary
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith("[AgenteAccion]", StringComparison.OrdinalIgnoreCase))
            .Select(SanitizeClosureLine)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        return lines.Count == 0
            ? "Accion automatica ejecutada correctamente."
            : string.Join(" ", lines);
    }

    private static string SanitizeClosureLine(string line)
    {
        var cleaned = Regex.Replace(line, @"^\[[^\]]+\]\s*", string.Empty).Trim();
        cleaned = Regex.Replace(
            cleaned,
            @"Password temporal:\s*\S+\.?",
            "Password temporal generada.",
            RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(
            cleaned,
            @"\s*No cierro el ticket;?\s*queda pendiente de confirmacion del usuario\.?",
            string.Empty,
            RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(
            cleaned,
            @"\s*Queda pendiente de confirmacion del usuario\.?",
            string.Empty,
            RegexOptions.IgnoreCase);

        return cleaned.Trim();
    }

    private static string BuildMissingFieldQuestion(string ticketNumber, MissingTicketField field)
        => field switch
        {
            MissingTicketField.Description => $"Encontre el ticket {ticketNumber}, pero falta una descripcion clara. Contame que problema tenes y que estabas intentando hacer.",
            MissingTicketField.System => $"Encontre el ticket {ticketNumber}. Para derivarlo bien, decime que modulo esta afectado: acceso, socios, turnos, profesores, pagos, disponibilidad o clases.",
            MissingTicketField.Email => $"Encontre el ticket {ticketNumber}. Para operar sobre tu usuario de la turnera, decime el email con el que estas registrado.",
            _ => $"Encontre el ticket {ticketNumber}. Me falta un dato mas para poder derivarlo."
        };

    private static string BuildMissingEmailForActionMessage(string ticketNumber, string? action, string? agent)
    {
        var normalized = NormalizeForMatching($"{action} {agent}");
        var operation = normalized switch
        {
            var text when text.Contains("pago") => "consultar tus pagos y creditos",
            var text when text.Contains("turno") => "consultar tus turnos",
            var text when text.Contains("disponibilidad") => "consultar la disponibilidad",
            var text when text.Contains("acceso") || text.Contains("resetear") => "operar sobre tu usuario de la turnera",
            _ => "continuar con la accion automatica"
        };

        return $"Encontre el ticket {ticketNumber}. Para {operation}, decime el email con el que estas registrado.";
    }

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

    private bool ShouldOnlyReportStatus(ServiceNowIncident incident)
        => IsFinalState(incident) || IsEscalatedToSecondLevel(incident);

    private bool IsEscalatedToSecondLevel(ServiceNowIncident incident)
    {
        var expectedGroup = _configuration["Metrics:EscalatedAssignmentGroup"] ?? "Soporte Nivel 2";
        return !string.IsNullOrWhiteSpace(incident.AssignmentGroup) &&
            !string.IsNullOrWhiteSpace(expectedGroup) &&
            NormalizeForMatching(incident.AssignmentGroup).Equals(NormalizeForMatching(expectedGroup), StringComparison.OrdinalIgnoreCase);
    }

    private string BuildStatusOnlyResponse(ServiceNowIncident incident)
    {
        var builder = new StringBuilder();
        builder.AppendLine(BuildTicketSummary(incident));
        builder.AppendLine();

        if (IsEscalatedToSecondLevel(incident))
        {
            builder.AppendLine($"Este ticket ya fue derivado a {incident.AssignmentGroup}. No voy a iniciar nuevamente el flujo automatico ni ejecutar acciones sobre la turnera.");
        }
        else
        {
            builder.AppendLine("Este ticket ya esta finalizado. No voy a iniciar nuevamente el flujo automatico ni ejecutar acciones sobre la turnera.");
        }

        if (incident.ResolvedAt is not null)
            builder.AppendLine($"Fecha de resolucion: {incident.ResolvedAt:yyyy-MM-dd HH:mm} UTC");

        return builder.ToString().Trim();
    }

    private static bool ShouldAskForClosureConfirmation(string agentSummary)
    {
        var normalized = NormalizeForMatching(agentSummary);
        return !normalized.Contains("no pude") &&
            !normalized.Contains("requiere") &&
            !normalized.Contains("no hay pagos registrados") &&
            !normalized.Contains("no tiene creditos disponibles") &&
            !normalized.Contains("faltan datos");
    }

    private static bool IsPositiveConfirmation(string normalized)
    {
        if (normalized is "si" or "s" or "ok")
            return true;

        var positives = new[]
        {
            "confirmo",
            "resuelto",
            "funciono",
            "ya pude",
            "pude entrar",
            "pude ingresar",
            "solucionado"
        };

        return positives.Any(normalized.Contains);
    }

    private static bool IsNegativeConfirmation(string normalized)
    {
        var negatives = new[]
        {
            "no",
            "nop",
            "sigue",
            "no funciona",
            "fallo",
            "no pude",
            "sigue fallando",
            "no se resolvio",
            "persiste"
        };

        return negatives.Any(value => normalized.Equals(value, StringComparison.OrdinalIgnoreCase) || normalized.Contains(value));
    }

    private string BuildFinalStateResponse(ServiceNowIncident incident)
    {
        return BuildStatusOnlyResponse(incident);
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

public sealed record PendingTicketClosure(
    string Number,
    string SysId,
    string Summary);

public sealed record ActiveTicketFlow(
    string Number,
    string SysId);

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
    public string? DescriptionAnswer { get; private set; }
    public string? SystemAnswer { get; private set; }
    public string? EmailAnswer { get; private set; }

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

    public void MarkCollected(MissingTicketField field, string answer)
    {
        switch (field)
        {
            case MissingTicketField.Description:
                HasDescription = true;
                DescriptionAnswer = answer;
                break;
            case MissingTicketField.System:
                HasSystem = true;
                SystemAnswer = answer;
                break;
            case MissingTicketField.Email:
                HasEmail = true;
                EmailAnswer = answer;
                break;
        }
    }

    public ServiceNowIncident Enrich(ServiceNowIncident incident)
    {
        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(DescriptionAnswer))
            details.Add($"Informacion agregada por Telegram: descripcion del problema: {DescriptionAnswer}");
        if (!string.IsNullOrWhiteSpace(SystemAnswer))
            details.Add($"Informacion agregada por Telegram: sistema afectado: {SystemAnswer}");
        if (!string.IsNullOrWhiteSpace(EmailAnswer))
            details.Add($"Informacion agregada por Telegram: email/contacto del usuario: {EmailAnswer}");

        if (details.Count == 0)
            return incident;

        var description = $"{incident.Description}\n\n{string.Join("\n", details)}".Trim();
        return incident with
        {
            Description = description,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public bool HasCollected(MissingTicketField field)
        => field switch
        {
            MissingTicketField.Description => HasDescription,
            MissingTicketField.System => HasSystem,
            MissingTicketField.Email => HasEmail,
            _ => false
        };
}
