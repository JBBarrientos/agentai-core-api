using System.Diagnostics;
using System.Text;

namespace AgentAI.Modules.AgentActions;

public interface IAgentActionInvoker
{
    Task<AgentActionResult> ExecuteAsync(string diagnostic, CancellationToken ct = default);
}

public sealed class AgentActionInvoker : IAgentActionInvoker
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgentActionInvoker> _logger;

    public AgentActionInvoker(IConfiguration configuration, ILogger<AgentActionInvoker> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AgentActionResult> ExecuteAsync(string diagnostic, CancellationToken ct = default)
    {
        var projectPath = GetAgentActionProjectPath();
        if (!Directory.Exists(projectPath))
            return AgentActionResult.Fail($"No existe el proyecto AgenteAccion en {projectPath}.");

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
                return AgentActionResult.Fail("No pude iniciar AgenteAccion.");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.StandardInput.WriteLineAsync(diagnostic);
            await process.StandardInput.WriteLineAsync("salir");
            process.StandardInput.Close();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_configuration.GetValue("AgentAction:TimeoutSeconds", 90)));
            await process.WaitForExitAsync(timeoutCts.Token);

            var text = output.ToString().Trim();
            var err = error.ToString().Trim();

            if (process.ExitCode != 0)
                return AgentActionResult.Fail(string.IsNullOrWhiteSpace(err) ? text : err);

            return new AgentActionResult(true, text, err);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return AgentActionResult.Fail("AgenteAccion excedio el tiempo maximo de ejecucion.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not execute AgenteAccion.");
            return AgentActionResult.Fail(ex.Message);
        }
    }

    private string GetAgentActionProjectPath()
    {
        var configured = _configuration["AgentAction:ProjectPath"];
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured);

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ProyectoFinal", "AgenteAccion"));
    }

    private void SetAgentEnvironment(ProcessStartInfo psi)
    {
        SetIfPresent(psi, "GROQ_API_KEY", _configuration["Groq:ApiKey"]
            ?? Environment.GetEnvironmentVariable("GROQ_API_KEY")
            ?? Environment.GetEnvironmentVariable("GROQ_API_KEY", EnvironmentVariableTarget.User));

        SetIfPresent(psi, "AGENTAI_API_URL", _configuration["AgentAI:ApiUrl"]
            ?? Environment.GetEnvironmentVariable("AGENTAI_API_URL")
            ?? "http://localhost:5038");

        SetIfPresent(psi, "TURNERA_API_URL", _configuration["Turnera:ApiUrl"]
            ?? Environment.GetEnvironmentVariable("TURNERA_API_URL")
            ?? "http://localhost:3000");

        SetIfPresent(psi, "AGENT_API_KEY", _configuration["Turnera:AgentApiKey"]
            ?? Environment.GetEnvironmentVariable("AGENT_API_KEY")
            ?? TryReadTurneraEnv("AGENT_API_KEY"));
    }

    private static void SetIfPresent(ProcessStartInfo psi, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            psi.Environment[key] = value;
    }

    private static string? TryReadTurneraEnv(string key)
    {
        try
        {
            var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Turnera-Pilates", ".env"));
            if (!File.Exists(path))
                return null;

            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                    continue;

                var separator = trimmed.IndexOf('=');
                if (separator <= 0)
                    continue;

                if (trimmed[..separator].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    return trimmed[(separator + 1)..].Trim().Trim('"');
            }
        }
        catch
        {
            return null;
        }

        return null;
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

public sealed record AgentActionResult(bool Success, string Output, string? Error)
{
    public static AgentActionResult Fail(string error) => new(false, string.Empty, error);
}
