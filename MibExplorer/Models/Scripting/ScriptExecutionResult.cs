namespace MibExplorer.Models.Scripting;

public sealed class ScriptExecutionResult
{
    public bool Success { get; init; }

    public int ExitCode { get; init; }

    public string RemoteScriptPath { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;
}