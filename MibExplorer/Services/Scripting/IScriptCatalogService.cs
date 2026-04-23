using MibExplorer.Models.Scripting;

namespace MibExplorer.Services.Scripting;

public interface IScriptCatalogService
{
    string ScriptsFolderPath { get; }

    string OfficialScriptsFolderPath { get; }

    string CustomScriptsFolderPath { get; }

    void EnsureScriptsFolderExists();

    IReadOnlyList<ScriptItem> GetScripts();
}