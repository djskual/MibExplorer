namespace MibExplorer.Models;

public sealed class SshKeyGenerationResult
{
    public string PrivateKeyPath { get; init; } = string.Empty;
    public string PublicKeyPath { get; init; } = string.Empty;
    public string PublicKeyOpenSsh { get; init; } = string.Empty;
}
