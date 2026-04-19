using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MibExplorer.Models;
using MibExplorer.Models.Scripting;

namespace MibExplorer.Services.Scripting;

public sealed class ScriptExecutionService : IScriptExecutionService
{
    private const string RemoteScriptsFolder = "/tmp";

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

    private readonly IMibConnectionService _connectionService;

    public ScriptExecutionService(IMibConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    public async Task<ScriptExecutionResult> ExecuteAsync(
        ScriptItem script,
        Action<string>? onOutput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(script);

        if (string.IsNullOrWhiteSpace(script.LocalPath) || !File.Exists(script.LocalPath))
        {
            return new ScriptExecutionResult
            {
                Success = false,
                ExitCode = -1,
                ErrorMessage = "Local script file was not found."
            };
        }

        if (!_connectionService.IsConnected)
        {
            return new ScriptExecutionResult
            {
                Success = false,
                ExitCode = -1,
                ErrorMessage = "No active MIB connection."
            };
        }

        string remoteEntryPath = string.Empty;
        string cleanupTargetPath = string.Empty;
        string runCommand = string.Empty;

        try
        {
            onOutput?.Invoke($"Using remote temp path: {RemoteScriptsFolder}");

            if (script.IsPackage)
            {
                if (string.IsNullOrWhiteSpace(script.PackageRootPath) || !Directory.Exists(script.PackageRootPath))
                {
                    return new ScriptExecutionResult
                    {
                        Success = false,
                        ExitCode = -1,
                        ErrorMessage = "Local package folder was not found."
                    };
                }

                await NormalizePackageScriptsAsync(script.PackageRootPath, onOutput, cancellationToken);

                string remotePackageName = BuildRemoteDirectoryName(script.Name);
                string remotePackageRoot = $"{RemoteScriptsFolder}/{remotePackageName}";
                cleanupTargetPath = remotePackageRoot;

                onOutput?.Invoke($"Remote workspace: {remotePackageRoot}");
                onOutput?.Invoke($"Uploading package: {script.Name}");
                await UploadPackageAsync(script.PackageRootPath, remotePackageRoot, onOutput, cancellationToken);

                remoteEntryPath = $"{remotePackageRoot}/run.sh";

                onOutput?.Invoke("Setting execute permissions on package files");
                await SetPackagePermissionsAsync(remotePackageRoot, script.PackageRootPath, cancellationToken);

                onOutput?.Invoke($"Running package: {remoteEntryPath}");
                runCommand = $"cd {EscapeShellArg(remotePackageRoot)} && sh ./run.sh; echo __MIBEXPLORER_EXIT_CODE__:$?";
            }
            else
            {
                string remoteFileName = BuildRemoteFileName(script.FileName);
                remoteEntryPath = $"{RemoteScriptsFolder}/{remoteFileName}";
                cleanupTargetPath = remoteEntryPath;

                onOutput?.Invoke($"Remote workspace: {remoteEntryPath}");
                await NormalizeScriptFileAsync(script.LocalPath, onOutput, cancellationToken);

                onOutput?.Invoke($"Uploading script: {script.FileName}");
                await UploadTextFileViaShellAsync(
                    script.LocalPath,
                    remoteEntryPath,
                    cancellationToken);

                onOutput?.Invoke("Setting execute permission");
                await _connectionService.ExecuteCommandAsync(
                    $"chmod 755 {EscapeShellArg(remoteEntryPath)}",
                    cancellationToken);

                onOutput?.Invoke($"Running script: {remoteEntryPath}");
                runCommand = $"sh {EscapeShellArg(remoteEntryPath)}; echo __MIBEXPLORER_EXIT_CODE__:$?";
            }

            using var shellSession = await _connectionService.CreateShellSessionAsync(cancellationToken);
            await shellSession.StartAsync(cancellationToken);

            var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var buffer = new StringBuilder();

            shellSession.Closed += (_, _) =>
            {
                completion.TrySetException(
                    new InvalidOperationException("Remote shell session closed before exit code was received."));
            };

            shellSession.TextReceived += (_, text) =>
            {
                try
                {
                    if (string.IsNullOrEmpty(text))
                    {
                        return;
                    }

                    buffer.Append(text);
                    ProcessBufferedText(buffer, onOutput, completion);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(
                        new InvalidOperationException($"Shell output processing failed: {ex.Message}", ex));
                }
            };

            await shellSession.SendCommandAsync(runCommand, cancellationToken);

            using var registration = cancellationToken.Register(() =>
            {
                completion.TrySetCanceled(cancellationToken);
            });

            var finalExitCode = await completion.Task;

            await CleanupRemoteAsync(cleanupTargetPath, cancellationToken);

            return new ScriptExecutionResult
            {
                Success = finalExitCode == 0,
                ExitCode = finalExitCode,
                RemoteScriptPath = remoteEntryPath,
                ErrorMessage = finalExitCode == 0
                    ? string.Empty
                    : $"Script exited with code {finalExitCode}."
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(cleanupTargetPath))
                {
                    onOutput?.Invoke($"Cleaning remote workspace after failure: {cleanupTargetPath}");
                    await CleanupRemoteAsync(cleanupTargetPath, cancellationToken);
                }
            }
            catch
            {
                // Best effort cleanup only.
            }

            return new ScriptExecutionResult
            {
                Success = false,
                ExitCode = -1,
                ErrorMessage = ex.Message
            };
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

            onOutput?.Invoke($"Uploading package file: {relativePath}");
            await UploadPackageFileAsync(
                file,
                remotePath,
                cancellationToken);
        }
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

    private static string BuildRemoteFileName(string fileName)
    {
        string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        return $"{timestamp}_{fileName}";
    }

    private static string BuildRemoteDirectoryName(string name)
    {
        string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string safeName = new string(name.Select(ch =>
            char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_').ToArray());

        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "package";
        }

        return $"{timestamp}_{safeName}";
    }

    private static void ProcessBufferedText(
        StringBuilder buffer,
        Action<string>? onOutput,
        TaskCompletionSource<int> completion)
    {
        while (TryReadNextLine(buffer, out var line))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            onOutput?.Invoke(line);

            if (TryReadExitCode(line, out var exitCode))
            {
                completion.TrySetResult(exitCode);
            }
        }
    }

    private static bool TryReadNextLine(StringBuilder buffer, out string line)
    {
        line = string.Empty;

        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] == '\n')
            {
                var rawLine = buffer.ToString(0, i + 1);
                buffer.Remove(0, i + 1);
                line = rawLine.TrimEnd('\r', '\n');
                return true;
            }
        }

        return false;
    }

    private static bool TryReadExitCode(string line, out int exitCode)
    {
        const string marker = "__MIBEXPLORER_EXIT_CODE__:";

        exitCode = -1;

        if (!line.StartsWith(marker, StringComparison.Ordinal))
        {
            return false;
        }

        var value = line[marker.Length..];

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out exitCode);
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

    private static bool IsTextLikeFile(string localPath)
    {
        string extension = Path.GetExtension(localPath);
        return TextLikeExtensions.Contains(extension);
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

    private static string EscapeShellArg(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'") + "'";
    }

    private async Task UploadTextFileViaShellAsync(
        string localPath,
        string remotePath,
        CancellationToken cancellationToken)
    {
        string content = await File.ReadAllTextAsync(localPath, cancellationToken);

        string marker = "__MIBEXPLORER_EOF__";
        while (content.Contains(marker, StringComparison.Ordinal))
        {
            marker += "_X";
        }

        string command =
            $"cat > {EscapeShellArg(remotePath)} <<'{marker}'\n" +
            content +
            $"\n{marker}";

        await _connectionService.ExecuteCommandAsync(command, cancellationToken);
    }
}