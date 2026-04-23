namespace MibExplorer.Models.Scripting;

public sealed class OfficialScriptsManifest
{
    public List<string> PackagesScripts { get; set; } = new();

    public List<string> SingleScripts { get; set; } = new();
}