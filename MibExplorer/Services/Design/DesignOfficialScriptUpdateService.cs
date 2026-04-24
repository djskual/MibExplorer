using MibExplorer.Models.Scripting;
using MibExplorer.Services.Scripting;
using System.IO;

namespace MibExplorer.Services.Design;

public sealed class DesignOfficialScriptUpdateService : IOfficialScriptUpdateService
{
    public Task<bool> CheckForOfficialUpdatesAsync(string officialScriptsFolderPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(officialScriptsFolderPath);
        return Task.FromResult(true);
    }

    public OfficialScriptsManifest? LoadLocalManifest(string officialScriptsFolderPath)
    {
        return null;
    }

    public Task<string> UpdateOfficialScriptsAsync(string officialScriptsFolderPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(officialScriptsFolderPath);
        return Task.FromResult("Design mode: official scripts update simulated.");
    }

    public string? GetExpectedSha256(string officialScriptsFolderPath, ScriptItem script)
    {
        return null;
    }

    public Task<string> RestoreOfficialScriptAsync(
        string officialScriptsFolderPath,
        ScriptItem script,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"Design mode: restore simulated for {script.Name}.");
    }
}