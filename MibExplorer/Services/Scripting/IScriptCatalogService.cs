using MibExplorer.Models.Scripting;

namespace MibExplorer.Services.Scripting;

public interface IScriptCatalogService
{
    string ScriptsFolderPath { get; }

    void EnsureScriptsFolderExists();

    IReadOnlyList<ScriptItem> GetScripts();
}