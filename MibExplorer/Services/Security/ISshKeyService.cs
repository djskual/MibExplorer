using MibExplorer.Models;

namespace MibExplorer.Services;

public interface ISshKeyService
{
    Task<SshKeyGenerationResult> GenerateRsaKeyPairAsync(
        string outputDirectory,
        string keyName,
        string comment,
        CancellationToken cancellationToken = default);
}
