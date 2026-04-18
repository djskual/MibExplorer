namespace MibExplorer.Settings;

public sealed class AppSettings
{
    public bool AutoCheckUpdatesOnStartup { get; set; } = true;
    public bool IncludePrereleaseVersionsInUpdateCheck { get; set; } = false;
    public bool RememberWindowSizeAndPosition { get; set; } = true;

    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }

    public string? LastHost { get; set; }
    public string? LastPort { get; set; }
    public string? LastUsername { get; set; }

    public bool UsePrivateKey { get; set; } = true;
    public string? LastPrivateKeyPath { get; set; }
    public string? LastWorkspaceFolder { get; set; }
    public string? LastPublicKeyExportPath { get; set; }

    public string? ScriptsFolderPath { get; set; }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            AutoCheckUpdatesOnStartup = AutoCheckUpdatesOnStartup,
            IncludePrereleaseVersionsInUpdateCheck = IncludePrereleaseVersionsInUpdateCheck,
            RememberWindowSizeAndPosition = RememberWindowSizeAndPosition,
            WindowWidth = WindowWidth,
            WindowHeight = WindowHeight,
            WindowLeft = WindowLeft,
            WindowTop = WindowTop,
            LastHost = LastHost,
            LastPort = LastPort,
            LastUsername = LastUsername,
            UsePrivateKey = UsePrivateKey,
            LastPrivateKeyPath = LastPrivateKeyPath,
            LastWorkspaceFolder = LastWorkspaceFolder,
            LastPublicKeyExportPath = LastPublicKeyExportPath,
            ScriptsFolderPath = ScriptsFolderPath
        };
    }

    public void Normalize()
    {
        LastHost = string.IsNullOrWhiteSpace(LastHost) ? null : LastHost.Trim();
        LastPort = string.IsNullOrWhiteSpace(LastPort) ? null : LastPort.Trim();
        LastUsername = string.IsNullOrWhiteSpace(LastUsername) ? null : LastUsername.Trim();
        LastPrivateKeyPath = string.IsNullOrWhiteSpace(LastPrivateKeyPath) ? null : LastPrivateKeyPath.Trim();
        LastWorkspaceFolder = string.IsNullOrWhiteSpace(LastWorkspaceFolder) ? null : LastWorkspaceFolder.Trim();
        LastPublicKeyExportPath = string.IsNullOrWhiteSpace(LastPublicKeyExportPath) ? null : LastPublicKeyExportPath.Trim();
        ScriptsFolderPath = string.IsNullOrWhiteSpace(ScriptsFolderPath) ? null : ScriptsFolderPath.Trim();
    }
}