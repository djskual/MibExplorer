namespace MibExplorer.Models.Scripting;

public sealed class OfficialScriptsManifest
{
    public List<OfficialScriptEntry> PackagesScripts { get; set; } = new();

    public List<OfficialScriptEntry> SingleScripts { get; set; } = new();
}

public sealed class OfficialScriptEntry
{
    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;
}