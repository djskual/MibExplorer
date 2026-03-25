namespace MibExplorer.Models;

public sealed class ConnectionSettings
{
    public string Host { get; set; } = "192.168.1.10";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "root";
    public string Password { get; set; } = string.Empty;
}
