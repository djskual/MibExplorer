using MibExplorer.Models;
using System.Diagnostics;
using System.IO;

namespace MibExplorer.Services;

public sealed class SshKeyService : ISshKeyService
{
    public async Task<SshKeyGenerationResult> GenerateRsaKeyPairAsync(
        string outputDirectory,
        string keyName,
        string comment,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("Output directory is required.");

        Directory.CreateDirectory(outputDirectory);

        string privateKeyPath = Path.Combine(outputDirectory, keyName);
        string publicKeyPath = privateKeyPath + ".pub";

        if (File.Exists(privateKeyPath))
            File.Delete(privateKeyPath);

        if (File.Exists(publicKeyPath))
            File.Delete(publicKeyPath);

        var psi = new ProcessStartInfo
        {
            FileName = "ssh-keygen",
            Arguments = $"-t rsa -b 2048 -f \"{privateKeyPath}\" -N \"\" -C \"{comment}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start ssh-keygen process.");

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            string error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"ssh-keygen failed: {error}");
        }

        string publicKey = await File.ReadAllTextAsync(publicKeyPath, cancellationToken);

        return new SshKeyGenerationResult
        {
            PrivateKeyPath = privateKeyPath,
            PublicKeyPath = publicKeyPath,
            PublicKeyOpenSsh = publicKey
        };
    }
}
