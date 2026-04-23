using MibExplorer.Services.Scripting;
using System.IO;

namespace MibExplorer.Services.Design;

public sealed class DesignOfficialScriptUpdateService : IOfficialScriptUpdateService
{
    public Task<string> UpdateOfficialScriptsAsync(string officialScriptsFolderPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(officialScriptsFolderPath);
        return Task.FromResult("Design mode: official scripts update simulated.");
    }
}