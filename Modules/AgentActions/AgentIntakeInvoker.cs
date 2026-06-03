using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentAI.Modules.ServiceNow;

namespace AgentAI.Modules.AgentActions;

public interface IAgentIntakeInvoker
{
    Task<AgentIntakeResult> AnalyzeAsync(ServiceNowIncident incident, CancellationToken ct = default);
}

public sealed class AgentIntakeInvoker : IAgentIntakeInvoker
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly IConfiguration _configuration;
    private readonly ILogger<AgentIntakeInvoker> _logger;

    public AgentIntakeInvoker(IConfiguration configuration, ILogger<AgentIntakeInvoker> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AgentIntakeResult> AnalyzeAsync(ServiceNowIncident incident, CancellationToken ct = default)
    {
        var projectPath = GetAgentEntradaProjectPath();
        if (!Directory.Exists(projectPath))
            return AgentIntakeResult.Fail($"No existe el proyecto AgenteEntrada en {projectPath}.");

        var request = new AgentIntakeRequest(
            incident.Number,
            incident.SysId,
            incident.Title,
            incident.Description,
            incident.CreatedByEmail);

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = projectPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--no-build");
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add("--telegram-intake");
        SetAgentEnvironment(psi);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null) output.AppendLine(args.Data);
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null) error.AppendLine(args.Data);
        };

        try
        {
            if (!process.Start())
                return AgentIntakeResult.Fail("No pude iniciar AgenteEntrada.");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions));
            process.StandardInput.Close();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_configuration.GetValue("AgentIntake:TimeoutSeconds", 45)));
            await process.WaitForExitAsync(timeoutCts.Token);

            var text = output.ToString().Trim();
            var err = error.ToString().Trim();

            if (process.ExitCode != 0)
                return AgentIntakeResult.Fail(string.IsNullOrWhiteSpace(err) ? text : err);

            var jsonLine = text
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault(line => line.TrimStart().StartsWith('{'));

            if (string.IsNullOrWhiteSpace(jsonLine))
                return AgentIntakeResult.Fail("AgenteEntrada no devolvio una decision JSON.");

            var decision = JsonSerializer.Deserialize<AgentIntakeDecision>(jsonLine, JsonOptions);
            return decision is null
                ? AgentIntakeResult.Fail("No pude interpretar la decision de AgenteEntrada.")
                : AgentIntakeResult.Success(decision, text, err);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return AgentIntakeResult.Fail("AgenteEntrada excedio el tiempo maximo de ejecucion.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not execute AgenteEntrada.");
            return AgentIntakeResult.Fail(ex.Message);
        }
    }

    private string GetAgentEntradaProjectPath()
    {
        var configured = _configuration["AgentIntake:ProjectPath"];
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured);

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ProyectoFinal", "AgenteEntrada"));
    }

    private void SetAgentEnvironment(ProcessStartInfo psi)
    {
        SetIfPresent(psi, "AGENTAI_API_URL", _configuration["AgentAI:ApiUrl"]
            ?? Environment.GetEnvironmentVariable("AGENTAI_API_URL")
            ?? "http://localhost:5038");
    }

    private static void SetIfPresent(ProcessStartInfo psi, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            psi.Environment[key] = value;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }
}

public sealed record AgentIntakeRequest(
    string Number,
    string SysId,
    string Title,
    string Description,
    string CreatedByEmail);

public sealed record AgentIntakeDecision(
    string Decision,
    string? MissingField,
    string? Question,
    string? System,
    string? ArticleCode,
    string? Confidence,
    string? Action,
    string? Agent,
    string? User,
    string? Reason,
    string? RecommendedAction,
    string? SuggestedUserMessage);

public sealed record AgentIntakeResult(
    bool Succeeded,
    AgentIntakeDecision? Decision,
    string Output,
    string? Error)
{
    public static AgentIntakeResult Success(AgentIntakeDecision decision, string output, string? error)
        => new(true, decision, output, error);

    public static AgentIntakeResult Fail(string error)
        => new(false, null, string.Empty, error);
}
