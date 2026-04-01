namespace MibExplorer.Models;

public sealed class ConnectionSettings
{
    public string Host { get; set; } = "192.168.1.10";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "root";
    public string Password { get; set; } = string.Empty;

    public bool UsePrivateKey { get; set; } = true;
    public string PrivateKeyPath { get; set; } = string.Empty;
    public string Passphrase { get; set; } = string.Empty;

    public string WorkspaceFolder { get; set; } = string.Empty;
    public string PublicKeyExportPath { get; set; } = string.Empty;
}
