using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using MibExplorer.Models.Adaptations;
using MibExplorer.Services;

namespace MibExplorer.Services.Adaptations;

public sealed class AdaptationsCenterService : IAdaptationsCenterService
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

    public AdaptationsCenterService(IMibConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    public async Task<string> ReadVinAsync(
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
            "AdaptationsCenter",
            "Vin",
            timestamp);

        string remoteName = $"{timestamp}_AdaptationsCenter_Vin";
        string remoteRoot = $"{RemoteRoot}/{remoteName}";

        Directory.CreateDirectory(localTemp);

        try
        {
            ExtractResource("Payload.AdaptationsCenter.Vin.run.sh", Path.Combine(localTemp, "run.sh"));

            await NormalizePackageScriptsAsync(localTemp, onOutput, cancellationToken);

            onOutput?.Invoke($"Remote workspace: {remoteRoot}");
            onOutput?.Invoke("Uploading Adaptations Center VIN payload");

            await UploadPackageAsync(localTemp, remoteRoot, onOutput, cancellationToken);

            onOutput?.Invoke("Setting execute permissions on VIN payload");

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
                    new InvalidOperationException("Remote shell closed before VIN read completed."));
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

            return ParseVin(output);
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

    public async Task<IReadOnlyList<AdaptationReadValue>> ReadAdaptationsAsync(
        IReadOnlyCollection<AdaptationDefinition> definitions,
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
            "AdaptationsCenter",
            timestamp);

        string remoteName = $"{timestamp}_AdaptationsCenter_Read";
        string remoteRoot = $"{RemoteRoot}/{remoteName}";

        Directory.CreateDirectory(localTemp);

        try
        {
            ExtractResource("Payload.AdaptationsCenter.Read.run.sh", Path.Combine(localTemp, "run.sh"));
            ExtractResource("Payload.AdaptationsCenter.Read.pc", Path.Combine(localTemp, "pc"));

            string keysFile = Path.Combine(localTemp, "keys.txt");
            await WriteKeysFileAsync(keysFile, definitions, cancellationToken);

            await NormalizePackageScriptsAsync(localTemp, onOutput, cancellationToken);

            onOutput?.Invoke($"Remote workspace: {remoteRoot}");
            onOutput?.Invoke("Uploading Adaptations Center payload");

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
                    new InvalidOperationException("Remote shell closed before adaptations read completed."));
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

            return ParseReadValues(output);
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

    public Task<AdaptationWriteResult> PreflightWriteAdaptationsAsync(
        IReadOnlyCollection<PhysicalWriteTransaction> transactions,
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWritePayloadAsync(
            transactions,
            mode: "preflight",
            onOutput,
            cancellationToken);
    }

    public Task<AdaptationWriteResult> WriteAdaptationsAsync(
        IReadOnlyCollection<PhysicalWriteTransaction> transactions,
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWritePayloadAsync(
            transactions,
            mode: "write",
            onOutput,
            cancellationToken);
    }

    private async Task<AdaptationWriteResult> ExecuteWritePayloadAsync(
        IReadOnlyCollection<PhysicalWriteTransaction> transactions,
        string mode,
        Action<string>? onOutput,
        CancellationToken cancellationToken)
    {
        if (!_connectionService.IsConnected)
            throw new InvalidOperationException("No active MIB connection.");

        if (!string.Equals(mode, "write", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mode, "preflight", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid adaptation write mode.");
        }

        List<PhysicalWriteTransaction> writableTransactions = transactions
            .Where(t => t.IsDirty && t.IsValid)
            .ToList();

        if (writableTransactions.Count == 0)
        {
            return new AdaptationWriteResult
            {
                Success = false,
                Message = "No valid adaptation write transaction."
            };
        }

        string timestamp = DateTimeOffset.UtcNow.ToString(
            "yyyyMMdd_HHmmss",
            CultureInfo.InvariantCulture);

        string localTemp = Path.Combine(
            Path.GetTempPath(),
            "MibExplorer",
            "AdaptationsCenter",
            "Write",
            timestamp);

        string remoteName = $"{timestamp}_AdaptationsCenter_Write";
        string remoteRoot = $"{RemoteRoot}/{remoteName}";

        Directory.CreateDirectory(localTemp);

        try
        {
            ExtractResource("Payload.AdaptationsCenter.Write.run.sh", Path.Combine(localTemp, "run.sh"));
            ExtractResource("Payload.AdaptationsCenter.Write.pc", Path.Combine(localTemp, "pc"));

            await File.WriteAllTextAsync(
                Path.Combine(localTemp, "mode.txt"),
                mode.ToLowerInvariant(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);

            await WriteTransactionsFileAsync(
                Path.Combine(localTemp, "writes.txt"),
                writableTransactions,
                cancellationToken);

            await NormalizePackageScriptsAsync(localTemp, onOutput, cancellationToken);

            onOutput?.Invoke($"Remote workspace: {remoteRoot}");
            onOutput?.Invoke($"Uploading Adaptations Center {mode} payload");

            await UploadPackageAsync(localTemp, remoteRoot, onOutput, cancellationToken);

            onOutput?.Invoke($"Setting execute permissions on {mode} payload");

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
                    new InvalidOperationException("Remote shell closed before adaptations write payload completed."));
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
            }

            try
            {
                await CleanupRemoteAsync(remoteRoot, cancellationToken);
            }
            catch
            {
            }
        }
    }

    private static async Task WriteKeysFileAsync(
        string destinationPath,
        IReadOnlyCollection<AdaptationDefinition> definitions,
        CancellationToken cancellationToken)
    {
        IEnumerable<PhysicalStorageKey> uniqueKeys = definitions
            .SelectMany(GetPhysicalKeys)
            .GroupBy(k => AdaptationReadValue.BuildCacheKey(k.Partition, k.Key, k.Type))
            .Select(g => g.First())
            .OrderBy(k => k.Partition, StringComparer.OrdinalIgnoreCase)
            .ThenBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(k => k.Type, StringComparer.OrdinalIgnoreCase);

        var builder = new StringBuilder();

        foreach (PhysicalStorageKey key in uniqueKeys)
        {
            builder.Append(key.Partition.Trim());
            builder.Append(';');
            builder.Append(key.Key.Trim());
            builder.Append(';');
            builder.Append(key.Type.Trim());
            builder.Append('\n');
        }

        await File.WriteAllTextAsync(
            destinationPath,
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    private static IEnumerable<PhysicalStorageKey> GetPhysicalKeys(AdaptationDefinition definition)
    {
        if (definition.PhysicalKeys.Count > 0)
            return definition.PhysicalKeys;

        if (!string.IsNullOrWhiteSpace(definition.Persistence.Partition) &&
            !string.IsNullOrWhiteSpace(definition.Persistence.Key) &&
            !string.IsNullOrWhiteSpace(definition.Persistence.Type))
        {
            return new[]
            {
            new PhysicalStorageKey(
                definition.Persistence.Partition,
                definition.Persistence.Key,
                definition.Persistence.Type)
        };
        }

        return Array.Empty<PhysicalStorageKey>();
    }

    private async Task UploadPackageAsync(
        string localPackageRoot,
        string remotePackageRoot,
        Action<string>? onOutput,
        CancellationToken cancellationToken)
    {
        List<string> files = Directory
            .EnumerateFiles(localPackageRoot, "*", SearchOption.AllDirectories)
            .OrderBy(path => IsTextLikeFile(path) ? 1 : 0)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string file in files)
        {
            string relativePath = Path.GetRelativePath(localPackageRoot, file)
                .Replace('\\', '/');

            string remotePath = $"{remotePackageRoot}/{relativePath}";

            onOutput?.Invoke($"Uploading payload file: {relativePath}");

            await UploadPackageFileAsync(file, remotePath, cancellationToken);
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

    private static string ParseVin(string output)
    {
        string vin = "UNKNOWN";

        foreach (string rawLine in output.Split('\n'))
        {
            string line = rawLine.Trim();

            if (line.StartsWith("MIBEXPLORER_ERROR=", StringComparison.Ordinal))
                throw new InvalidOperationException(line["MIBEXPLORER_ERROR=".Length..].Trim());

            if (!line.StartsWith("MIBEXPLORER_VIN=", StringComparison.Ordinal))
                continue;

            vin = line["MIBEXPLORER_VIN=".Length..].Trim();

            if (string.IsNullOrWhiteSpace(vin))
                vin = "UNKNOWN";
        }

        return vin;
    }

    private static async Task WriteTransactionsFileAsync(
        string destinationPath,
        IReadOnlyCollection<PhysicalWriteTransaction> transactions,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();

        foreach (PhysicalWriteTransaction transaction in transactions)
        {
            builder.Append(transaction.PhysicalKey.Partition.Trim());
            builder.Append(';');
            builder.Append(transaction.PhysicalKey.Key.Trim());
            builder.Append(';');
            builder.Append(transaction.PhysicalKey.Type.Trim());
            builder.Append(';');
            builder.Append(transaction.OriginalRawValue.Trim());
            builder.Append(';');
            builder.Append(transaction.MergedRawValue.Trim());
            builder.Append('\n');
        }

        await File.WriteAllTextAsync(
            destinationPath,
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    private static AdaptationWriteResult ParseWriteResult(string output)
    {
        var result = new AdaptationWriteResult();
        var physicalResults = new Dictionary<string, AdaptationPhysicalWriteResult>(StringComparer.OrdinalIgnoreCase);

        bool globalSuccess = false;
        string message = string.Empty;

        foreach (string rawLine in output.Split('\n'))
        {
            string line = rawLine.Trim();

            if (line.StartsWith("MIBEXPLORER_ERROR=", StringComparison.Ordinal))
            {
                message = line["MIBEXPLORER_ERROR=".Length..].Trim();
                globalSuccess = false;
                continue;
            }

            if (line.StartsWith("MIBEXPLORER_WRITE_RESULT=", StringComparison.Ordinal))
            {
                string value = line["MIBEXPLORER_WRITE_RESULT=".Length..].Trim();
                globalSuccess = string.Equals(value, "OK", StringComparison.OrdinalIgnoreCase);
                message = value;
                continue;
            }

            if (line.StartsWith("MIBEXPLORER_CURRENT;", StringComparison.Ordinal))
            {
                string[] parts = line.Split(';', 5);

                if (parts.Length < 5)
                    continue;

                var key = new PhysicalStorageKey(parts[1].Trim(), parts[2].Trim(), parts[3].Trim());
                string cacheKey = AdaptationReadValue.BuildCacheKey(key.Partition, key.Key, key.Type);

                physicalResults[cacheKey] = new AdaptationPhysicalWriteResult
                {
                    PhysicalKey = key,
                    ReadbackRawValue = parts[4].Trim(),
                    Success = false,
                    Message = "Current value read."
                };

                continue;
            }

            if (line.StartsWith("MIBEXPLORER_EXPECTED;", StringComparison.Ordinal))
            {
                string[] parts = line.Split(';', 5);

                if (parts.Length < 5)
                    continue;

                var key = new PhysicalStorageKey(parts[1].Trim(), parts[2].Trim(), parts[3].Trim());
                string cacheKey = AdaptationReadValue.BuildCacheKey(key.Partition, key.Key, key.Type);

                AdaptationPhysicalWriteResult? existing = physicalResults.TryGetValue(cacheKey, out AdaptationPhysicalWriteResult? found)
                    ? found
                    : null;

                physicalResults[cacheKey] = new AdaptationPhysicalWriteResult
                {
                    PhysicalKey = key,
                    ExpectedOriginalRawValue = parts[4].Trim(),
                    TargetRawValue = existing?.TargetRawValue ?? string.Empty,
                    ReadbackRawValue = existing?.ReadbackRawValue ?? string.Empty,
                    Success = existing?.Success ?? false,
                    Message = existing?.Message ?? "Expected value read."
                };

                continue;
            }

            if (line.StartsWith("MIBEXPLORER_TARGET;", StringComparison.Ordinal))
            {
                string[] parts = line.Split(';', 5);

                if (parts.Length < 5)
                    continue;

                var key = new PhysicalStorageKey(parts[1].Trim(), parts[2].Trim(), parts[3].Trim());
                string cacheKey = AdaptationReadValue.BuildCacheKey(key.Partition, key.Key, key.Type);

                AdaptationPhysicalWriteResult? existing = physicalResults.TryGetValue(cacheKey, out AdaptationPhysicalWriteResult? found)
                    ? found
                    : null;

                physicalResults[cacheKey] = new AdaptationPhysicalWriteResult
                {
                    PhysicalKey = key,
                    ExpectedOriginalRawValue = existing?.ExpectedOriginalRawValue ?? string.Empty,
                    TargetRawValue = parts[4].Trim(),
                    ReadbackRawValue = existing?.ReadbackRawValue ?? string.Empty,
                    Success = existing?.Success ?? false,
                    Message = existing?.Message ?? "Target value read."
                };

                continue;
            }

            if (line.StartsWith("MIBEXPLORER_READBACK;", StringComparison.Ordinal))
            {
                string[] parts = line.Split(';', 5);

                if (parts.Length < 5)
                    continue;

                var key = new PhysicalStorageKey(parts[1].Trim(), parts[2].Trim(), parts[3].Trim());
                string cacheKey = AdaptationReadValue.BuildCacheKey(key.Partition, key.Key, key.Type);

                AdaptationPhysicalWriteResult? existing = physicalResults.TryGetValue(cacheKey, out AdaptationPhysicalWriteResult? found)
                    ? found
                    : null;

                physicalResults[cacheKey] = new AdaptationPhysicalWriteResult
                {
                    PhysicalKey = key,
                    ExpectedOriginalRawValue = existing?.ExpectedOriginalRawValue ?? string.Empty,
                    TargetRawValue = existing?.TargetRawValue ?? string.Empty,
                    ReadbackRawValue = parts[4].Trim(),
                    Success = existing?.Success ?? false,
                    Message = existing?.Message ?? "Readback received."
                };

                continue;
            }

            if (line.StartsWith("MIBEXPLORER_WRITE_OK;", StringComparison.Ordinal))
            {
                string[] parts = line.Split(';', 4);

                if (parts.Length < 4)
                    continue;

                var key = new PhysicalStorageKey(parts[1].Trim(), parts[2].Trim(), parts[3].Trim());
                string cacheKey = AdaptationReadValue.BuildCacheKey(key.Partition, key.Key, key.Type);

                AdaptationPhysicalWriteResult? existing = physicalResults.TryGetValue(cacheKey, out AdaptationPhysicalWriteResult? found)
                    ? found
                    : null;

                physicalResults[cacheKey] = new AdaptationPhysicalWriteResult
                {
                    PhysicalKey = key,
                    ExpectedOriginalRawValue = existing?.ExpectedOriginalRawValue ?? string.Empty,
                    TargetRawValue = existing?.TargetRawValue ?? string.Empty,
                    ReadbackRawValue = existing?.ReadbackRawValue ?? string.Empty,
                    Success = true,
                    Message = "Write verified."
                };

                continue;
            }

            if (line.StartsWith("MIBEXPLORER_WRITE_FAILED;", StringComparison.Ordinal))
            {
                string[] parts = line.Split(';', 5);

                if (parts.Length < 5)
                    continue;

                var key = new PhysicalStorageKey(parts[1].Trim(), parts[2].Trim(), parts[3].Trim());
                string cacheKey = AdaptationReadValue.BuildCacheKey(key.Partition, key.Key, key.Type);

                AdaptationPhysicalWriteResult? existing = physicalResults.TryGetValue(cacheKey, out AdaptationPhysicalWriteResult? found)
                    ? found
                    : null;

                physicalResults[cacheKey] = new AdaptationPhysicalWriteResult
                {
                    PhysicalKey = key,
                    ExpectedOriginalRawValue = existing?.ExpectedOriginalRawValue ?? string.Empty,
                    TargetRawValue = existing?.TargetRawValue ?? string.Empty,
                    ReadbackRawValue = existing?.ReadbackRawValue ?? string.Empty,
                    Success = false,
                    Message = parts[4].Trim()
                };
            }
        }

        var finalResult = new AdaptationWriteResult
        {
            Success = globalSuccess && physicalResults.Values.All(r => r.Success),
            Message = string.IsNullOrWhiteSpace(message)
                ? globalSuccess ? "Write completed." : "Write failed."
                : message
        };

        foreach (AdaptationPhysicalWriteResult physicalResult in physicalResults.Values)
            finalResult.PhysicalResults.Add(physicalResult);

        return finalResult;
    }

    private static IReadOnlyList<AdaptationReadValue> ParseReadValues(string output)
    {
        var values = new List<AdaptationReadValue>();

        foreach (string rawLine in output.Split('\n'))
        {
            string line = rawLine.Trim();

            if (line.StartsWith("MIBEXPLORER_ERROR=", StringComparison.Ordinal))
                throw new InvalidOperationException(line["MIBEXPLORER_ERROR=".Length..].Trim());

            if (!line.StartsWith("MIBEXPLORER_ADAPT;", StringComparison.Ordinal))
                continue;

            string[] parts = line.Split(';', 5);

            if (parts.Length < 5)
                continue;

            values.Add(new AdaptationReadValue
            {
                Partition = parts[1].Trim(),
                Key = parts[2].Trim(),
                Type = parts[3].Trim(),
                Value = parts[4].Trim()
            });
        }

        return values;
    }

    private static void ExtractResource(string resourceSuffix, string destinationPath)
    {
        Assembly assembly = typeof(AdaptationsCenterService).Assembly;

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