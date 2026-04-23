namespace MibExplorer.Services.Scripting;

public interface IOfficialScriptUpdateService
{
    Task<string> UpdateOfficialScriptsAsync(string officialScriptsFolderPath, CancellationToken cancellationToken = default);
}