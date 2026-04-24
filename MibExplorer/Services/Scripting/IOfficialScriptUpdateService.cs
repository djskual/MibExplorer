using MibExplorer.Models.Scripting;

namespace MibExplorer.Services.Scripting;

public interface IOfficialScriptUpdateService
{
    Task<bool> CheckForOfficialUpdatesAsync(string officialScriptsFolderPath, CancellationToken cancellationToken = default);

    Task<string> UpdateOfficialScriptsAsync(string officialScriptsFolderPath, CancellationToken cancellationToken = default);

    Task<string> RestoreOfficialScriptAsync(string officialScriptsFolderPath, ScriptItem script, CancellationToken cancellationToken = default);

    OfficialScriptsManifest? LoadLocalManifest(string officialScriptsFolderPath);

    string? GetExpectedSha256(string officialScriptsFolderPath, ScriptItem script);
}