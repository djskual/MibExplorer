using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using MibExplorer.Models.Coding;
using MibExplorer.Services;

namespace MibExplorer.Services.Coding;

public sealed class CodingCenterService : ICodingCenterService
{
    private const string RemoteRoot = "/tmp";
    private const string ExitMarker = "__MIBEXPLORER_EXIT_CODE__:";
    private readonly IMibConnectionService _connectionService;

    private static readonly HashSet<string> TextLikeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".sh",
        ".txt",
        ".json",
        ".xml",
        ".cfg",
        ".conf",
        ".ini",
        ".csv",
        ".md",
        ".yml",
        ".yaml",
        ".log"
    };

    public CodingCenterService(IMibConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    public async Task<CodingReadResult> Read5FCodingAsync(
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        if (!_connectionService.IsConnected)
            throw new InvalidOperationException("No active MIB connection.");

        string timestamp = DateTimeOffset.UtcNow.ToString(
            "yyyyMMdd_HHmmss",
            CultureInfo.InvariantCulture);

        string localTemp = Path.Combine(
            Path.GetTempPath(),
            "MibExplorer",
            "CodingCenter",
            timestamp);

        string remoteName = $"{timestamp}_CodingCenter";
        string remoteRoot = $"{RemoteRoot}/{remoteName}";

        Directory.CreateDirectory(localTemp);

        try
        {
            ExtractResource("Payload.CodingCenter.Read.run.sh", Path.Combine(localTemp, "run.sh"));
            ExtractResource("Payload.CodingCenter.Read.dumb_persistence_reader", Path.Combine(localTemp, "dumb_persistence_reader"));

            await NormalizePackageScriptsAsync(localTemp, onOutput, cancellationToken);

            onOutput?.Invoke($"Remote workspace: {remoteRoot}");
            onOutput?.Invoke("Uploading Coding Center payload");

            await UploadPackageAsync(localTemp, remoteRoot, onOutput, cancellationToken);

            onOutput?.Invoke("Setting execute permissions on payload files");

            await SetPackagePermissionsAsync(remoteRoot, localTemp, cancellationToken);

            string command =
                $"cd {EscapeShellArg(remoteRoot)} && sh ./run.sh; echo {ExitMarker}$?";

            using var shell = await _connectionService.CreateShellSessionAsync(cancellationToken);
            await shell.StartAsync(cancellationToken);

            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var outputBuilder = new StringBuilder();
            var lineBuffer = new StringBuilder();

            shell.Closed += (_, _) =>
            {
                completion.TrySetException(
                    new InvalidOperationException("Remote shell closed before coding read completed."));
            };

            shell.TextReceived += (_, text) =>
            {
                if (string.IsNullOrEmpty(text))
                    return;

                lineBuffer.Append(text);

                while (TryReadNextLine(lineBuffer, out string line))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    outputBuilder.AppendLine(line);
                    onOutput?.Invoke(line);

                    if (line.StartsWith(ExitMarker, StringComparison.Ordinal))
                        completion.TrySetResult(outputBuilder.ToString());
                }
            };

            await shell.SendCommandAsync(command, cancellationToken);

            await using var registration = cancellationToken.Register(() =>
            {
                completion.TrySetCanceled(cancellationToken);
            });

            string output = await completion.Task;

            await CleanupRemoteAsync(remoteRoot, cancellationToken);

            return ParseReadResult(output);
        }
        finally
        {
            try
            {
                if (Directory.Exists(localTemp))
                    Directory.Delete(localTemp, recursive: true);
            }
            catch
            {
                // Best effort cleanup only.
            }

            try
            {
                await CleanupRemoteAsync(remoteRoot, cancellationToken);
            }
            catch
            {
                // Best effort cleanup only.
            }
        }
    }

    public async Task<CodingWriteResult> Write5FCodingAsync(
        string targetHex,
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        if (!_connectionService.IsConnected)
            throw new InvalidOperationException("No active MIB connection.");

        if (string.IsNullOrWhiteSpace(targetHex))
            throw new ArgumentException("Target coding HEX is empty.", nameof(targetHex));

        targetHex = targetHex.Trim().ToLowerInvariant();

        if (targetHex.Length != 50)
            throw new ArgumentException($"Target coding HEX must be 50 characters / 25 bytes. Actual length: {targetHex.Length}", nameof(targetHex));

        if (!targetHex.All(IsHexChar))
            throw new ArgumentException("Target coding HEX contains invalid characters.", nameof(targetHex));

        string timestamp = DateTimeOffset.UtcNow.ToString(
            "yyyyMMdd_HHmmss",
            CultureInfo.InvariantCulture);

        string localTemp = Path.Combine(
            Path.GetTempPath(),
            "MibExplorer",
            "CodingCenterWrite",
            timestamp);

        string remoteName = $"{timestamp}_CodingCenter_Write";
        string remoteRoot = $"{RemoteRoot}/{remoteName}";

        Directory.CreateDirectory(localTemp);

        try
        {
            ExtractResource("Payload.CodingCenter.Write.run.sh", Path.Combine(localTemp, "run.sh"));
            ExtractResource("Payload.CodingCenter.Write.pc", Path.Combine(localTemp, "pc"));
            ExtractResource("Payload.CodingCenter.Write.dumb_persistence_reader", Path.Combine(localTemp, "dumb_persistence_reader"));

            await NormalizePackageScriptsAsync(localTemp, onOutput, cancellationToken);

            onOutput?.Invoke($"Remote workspace: {remoteRoot}");
            onOutput?.Invoke("Uploading Coding Center write payload");

            await UploadPackageAsync(localTemp, remoteRoot, onOutput, cancellationToken);

            onOutput?.Invoke("Setting execute permissions on payload files");

            await SetPackagePermissionsAsync(remoteRoot, localTemp, cancellationToken);

            string command =
                $"cd {EscapeShellArg(remoteRoot)} && sh ./run.sh {EscapeShellArg(targetHex)}; echo {ExitMarker}$?";

            using var shell = await _connectionService.CreateShellSessionAsync(cancellationToken);
            await shell.StartAsync(cancellationToken);

            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var outputBuilder = new StringBuilder();
            var lineBuffer = new StringBuilder();

            shell.Closed += (_, _) =>
            {
                completion.TrySetException(
                    new InvalidOperationException("Remote shell closed before coding write completed."));
            };

            shell.TextReceived += (_, text) =>
            {
                if (string.IsNullOrEmpty(text))
                    return;

                lineBuffer.Append(text);

                while (TryReadNextLine(lineBuffer, out string line))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    outputBuilder.AppendLine(line);
                    onOutput?.Invoke(line);

                    if (line.StartsWith(ExitMarker, StringComparison.Ordinal))
                        completion.TrySetResult(outputBuilder.ToString());
                }
            };

            await shell.SendCommandAsync(command, cancellationToken);

            await using var registration = cancellationToken.Register(() =>
            {
                completion.TrySetCanceled(cancellationToken);
            });

            string output = await completion.Task;

            await CleanupRemoteAsync(remoteRoot, cancellationToken);

            return ParseWriteResult(output);
        }
        finally
        {
            try
            {
                if (Directory.Exists(localTemp))
                    Directory.Delete(localTemp, recursive: true);
            }
            catch
            {
                // Best effort cleanup only.
            }

            try
            {
                await CleanupRemoteAsync(remoteRoot, cancellationToken);
            }
            catch
            {
                // Best effort cleanup only.
            }
        }
    }

    private async Task UploadPackageAsync(
        string localPackageRoot,
        string remotePackageRoot,
        Action<string>? onOutput,
        CancellationToken cancellationToken)
    {
        foreach (string file in Directory.EnumerateFiles(localPackageRoot, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            string relativePath = Path.GetRelativePath(localPackageRoot, file)
                .Replace('\\', '/');

            string remotePath = $"{remotePackageRoot}/{relativePath}";

            onOutput?.Invoke($"Uploading payload file: {relativePath}");

            await UploadPackageFileAsync(
                file,
                remotePath,
                cancellationToken);
        }
    }

    private async Task UploadPackageFileAsync(
        string localPath,
        string remotePath,
        CancellationToken cancellationToken)
    {
        if (IsTextLikeFile(localPath))
        {
            await UploadTextFileViaShellAsync(localPath, remotePath, cancellationToken);
            return;
        }

        await UploadBinaryFileViaShellAsync(localPath, remotePath, cancellationToken);
    }

    private async Task UploadBinaryFileViaShellAsync(
        string localPath,
        string remotePath,
        CancellationToken cancellationToken)
    {
        await _connectionService.ExecuteCommandAsync(
            $": > {EscapeShellArg(remotePath)}",
            cancellationToken);

        await _connectionService.UploadFileWithoutMountAsync(
            localPath,
            remotePath,
            progress: null,
            cancellationToken: cancellationToken);
    }

    private async Task UploadTextFileViaShellAsync(
        string localPath,
        string remotePath,
        CancellationToken cancellationToken)
    {
        string content = await File.ReadAllTextAsync(localPath, cancellationToken);

        string marker = "__MIBEXPLORER_EOF__";
        while (content.Contains(marker, StringComparison.Ordinal))
            marker += "_X";

        string command =
            $"cat > {EscapeShellArg(remotePath)} <<'{marker}'\n" +
            content +
            $"\n{marker}";

        await _connectionService.ExecuteCommandAsync(command, cancellationToken);
    }

    private async Task SetPackagePermissionsAsync(
        string remotePackageRoot,
        string localPackageRoot,
        CancellationToken cancellationToken)
    {
        foreach (string file in Directory.EnumerateFiles(localPackageRoot, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(localPackageRoot, file)
                .Replace('\\', '/');

            string remotePath = $"{remotePackageRoot}/{relativePath}";

            await _connectionService.ExecuteCommandAsync(
                $"chmod 755 {EscapeShellArg(remotePath)}",
                cancellationToken);
        }
    }

    private static async Task NormalizePackageScriptsAsync(
        string packageRoot,
        Action<string>? onOutput,
        CancellationToken cancellationToken)
    {
        foreach (string file in Directory.EnumerateFiles(packageRoot, "*.sh", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            await NormalizeScriptFileAsync(file, onOutput, cancellationToken);
        }
    }

    private static async Task NormalizeScriptFileAsync(
        string localPath,
        Action<string>? onOutput,
        CancellationToken cancellationToken)
    {
        byte[] originalBytes = await File.ReadAllBytesAsync(localPath, cancellationToken);

        string text;
        using (var memoryStream = new MemoryStream(originalBytes, writable: false))
        using (var reader = new StreamReader(memoryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            text = await reader.ReadToEndAsync(cancellationToken);
        }

        string normalizedText = text.Replace("\r\n", "\n").Replace("\r", "\n");
        byte[] normalizedBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(normalizedText);

        if (originalBytes.SequenceEqual(normalizedBytes))
            return;

        await File.WriteAllBytesAsync(localPath, normalizedBytes, cancellationToken);
        onOutput?.Invoke($"Normalized script: {Path.GetFileName(localPath)}");
    }

    private static bool IsTextLikeFile(string localPath)
    {
        string extension = Path.GetExtension(localPath);
        return TextLikeExtensions.Contains(extension);
    }

    private static CodingReadResult ParseReadResult(string output)
    {
        string codingHex = string.Empty;
        int byteCount = 0;
        string vin = "UNKNOWN";

        foreach (string rawLine in output.Split('\n'))
        {
            string line = rawLine.Trim();

            if (line.StartsWith("MIBEXPLORER_CODING_HEX=", StringComparison.Ordinal))
            {
                codingHex = line["MIBEXPLORER_CODING_HEX=".Length..]
                    .Trim()
                    .ToUpperInvariant();
            }
            else if (line.StartsWith("MIBEXPLORER_BYTE_COUNT=", StringComparison.Ordinal))
            {
                _ = int.TryParse(
                    line["MIBEXPLORER_BYTE_COUNT=".Length..].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out byteCount);
            }
            else if (line.StartsWith("MIBEXPLORER_VIN=", StringComparison.Ordinal))
            {
                vin = line["MIBEXPLORER_VIN=".Length..].Trim();

                if (string.IsNullOrWhiteSpace(vin))
                    vin = "UNKNOWN";
            }
            else if (line.StartsWith("MIBEXPLORER_ERROR=", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    line["MIBEXPLORER_ERROR=".Length..].Trim());
            }
        }

        if (string.IsNullOrWhiteSpace(codingHex))
            throw new InvalidOperationException("Coding HEX was not returned by the MIB.");

        if (codingHex.Length % 2 != 0)
            throw new InvalidOperationException("Coding HEX length is invalid.");

        var bytes = new List<CodingByte>();

        for (int i = 0; i < codingHex.Length; i += 2)
        {
            bytes.Add(new CodingByte
            {
                Index = i / 2,
                Value = byte.Parse(
                    codingHex.Substring(i, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture)
            });
        }

        if (byteCount <= 0)
            byteCount = bytes.Count;

        return new CodingReadResult
        {
            CodingHex = codingHex,
            ByteCount = byteCount,
            Vin = vin,
            Bytes = bytes
        };
    }

    private static CodingWriteResult ParseWriteResult(string output)
    {
        string beforeHex = string.Empty;
        string afterHex = string.Empty;
        string message = string.Empty;
        bool success = false;

        foreach (string rawLine in output.Split('\n'))
        {
            string line = rawLine.Trim();

            if (line.StartsWith("MIBEXPLORER_BEFORE_HEX=", StringComparison.Ordinal))
            {
                beforeHex = line["MIBEXPLORER_BEFORE_HEX=".Length..]
                    .Trim()
                    .ToUpperInvariant();
            }
            else if (line.StartsWith("MIBEXPLORER_AFTER_HEX=", StringComparison.Ordinal))
            {
                afterHex = line["MIBEXPLORER_AFTER_HEX=".Length..]
                    .Trim()
                    .ToUpperInvariant();
            }
            else if (line.StartsWith("MIBEXPLORER_WRITE_RESULT=", StringComparison.Ordinal))
            {
                string result = line["MIBEXPLORER_WRITE_RESULT=".Length..].Trim();
                success = string.Equals(result, "OK", StringComparison.OrdinalIgnoreCase);
                message = result;
            }
            else if (line.StartsWith("MIBEXPLORER_ERROR=", StringComparison.Ordinal))
            {
                message = line["MIBEXPLORER_ERROR=".Length..].Trim();
                success = false;
            }
        }

        return new CodingWriteResult
        {
            BeforeHex = beforeHex,
            AfterHex = afterHex,
            Success = success,
            Message = string.IsNullOrWhiteSpace(message)
                ? success ? "Write completed." : "Write failed."
                : message
        };
    }

    private static bool IsHexChar(char value)
    {
        return value is >= '0' and <= '9'
            or >= 'a' and <= 'f'
            or >= 'A' and <= 'F';
    }

    private static void ExtractResource(string resourceSuffix, string destinationPath)
    {
        Assembly assembly = typeof(CodingCenterService).Assembly;

        string? resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name =>
                name.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            string available = string.Join(
                Environment.NewLine,
                assembly.GetManifestResourceNames());

            throw new FileNotFoundException(
                $"Embedded resource not found. Expected suffix: {resourceSuffix}{Environment.NewLine}{available}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
            throw new FileNotFoundException($"Unable to open embedded resource: {resourceName}");

        using FileStream output = File.Create(destinationPath);
        stream.CopyTo(output);
    }

    private async Task CleanupRemoteAsync(string remotePath, CancellationToken cancellationToken)
    {
        try
        {
            await _connectionService.ExecuteCommandAsync(
                $"rm -rf {EscapeShellArg(remotePath)}",
                cancellationToken);
        }
        catch
        {
            // Best effort cleanup only.
        }
    }

    private static bool TryReadNextLine(StringBuilder buffer, out string line)
    {
        line = string.Empty;

        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] == '\n')
            {
                string rawLine = buffer.ToString(0, i + 1);
                buffer.Remove(0, i + 1);
                line = rawLine.TrimEnd('\r', '\n');
                return true;
            }
        }

        return false;
    }

    private static string EscapeShellArg(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'") + "'";
    }
}