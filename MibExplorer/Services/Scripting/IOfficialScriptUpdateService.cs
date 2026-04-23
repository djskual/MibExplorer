using MibExplorer.Models.Scripting;

namespace MibExplorer.Services.Scripting;

public interface IOfficialScriptUpdateService
{
    Task<bool> CheckForOfficialUpdatesAsync(string officialScriptsFolderPath, CancellationToken cancellationToken = default);

    Task<string> UpdateOfficialScriptsAsync(string officialScriptsFolderPath, CancellationToken cancellationToken = default);

    OfficialScriptsManifest? LoadLocalManifest(string officialScriptsFolderPath);
}