using MibExplorer.Models.Scripting;

namespace MibExplorer.Services.Scripting;

public interface IScriptExecutionService
{
    Task<ScriptExecutionResult> ExecuteAsync(
        ScriptItem script,
        Action<string>? onOutput,
        CancellationToken cancellationToken = default);
}