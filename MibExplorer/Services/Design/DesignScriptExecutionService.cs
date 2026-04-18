using MibExplorer.Models.Scripting;
using MibExplorer.Services.Scripting;
using System.IO;
using System.Text.RegularExpressions;

namespace MibExplorer.Services.Design;

public sealed class DesignScriptExecutionService : IScriptExecutionService
{
    private static readonly Regex EchoRegex = new(
        "^echo\\s+\"(?<text>.*)\"\\s*$",
        RegexOptions.Compiled);

    private static readonly Regex ShRegex = new(
        "^sh\\s+(?<path>.+)$",
        RegexOptions.Compiled);

    private static readonly Regex CatRegex = new(
        "^cat\\s+(?<path>.+)$",
        RegexOptions.Compiled);

    public async Task<ScriptExecutionResult> ExecuteAsync(
        ScriptItem script,
        Action<string>? onOutput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(script);

        onOutput?.Invoke($"[design] Preparing script: {script.FileName}");
        await Task.Delay(150, cancellationToken);

        if (script.IsPackage)
        {
            onOutput?.Invoke($"[design] Uploading package to /tmp/{script.Name}");
            await Task.Delay(150, cancellationToken);

            onOutput?.Invoke($"[design] Running: cd /tmp/{script.Name} && sh ./run.sh");
            await Task.Delay(150, cancellationToken);

            if (string.IsNullOrWhiteSpace(script.PackageRootPath) || !Directory.Exists(script.PackageRootPath))
            {
                return new ScriptExecutionResult
                {
                    Success = false,
                    ExitCode = 1,
                    RemoteScriptPath = $"/tmp/{script.Name}/run.sh",
                    ErrorMessage = "Design package folder not found."
                };
            }

            string runPath = Path.Combine(script.PackageRootPath, "run.sh");
            bool ok = await SimulateScriptAsync(runPath, script.PackageRootPath, onOutput, cancellationToken);

            if (!ok)
            {
                return new ScriptExecutionResult
                {
                    Success = false,
                    ExitCode = 1,
                    RemoteScriptPath = $"/tmp/{script.Name}/run.sh",
                    ErrorMessage = "Design package execution failed."
                };
            }

            onOutput?.Invoke("[design] Script completed successfully");

            return new ScriptExecutionResult
            {
                Success = true,
                ExitCode = 0,
                RemoteScriptPath = $"/tmp/{script.Name}/run.sh",
                ErrorMessage = string.Empty
            };
        }

        onOutput?.Invoke("[design] Uploading script to /tmp");
        await Task.Delay(150, cancellationToken);

        onOutput?.Invoke($"[design] Running: sh /tmp/{script.FileName}");
        await Task.Delay(150, cancellationToken);

        if (!File.Exists(script.LocalPath))
        {
            onOutput?.Invoke("[design] Script file not found on disk.");
            await Task.Delay(150, cancellationToken);

            return new ScriptExecutionResult
            {
                Success = false,
                ExitCode = 1,
                RemoteScriptPath = $"/tmp/{script.FileName}",
                ErrorMessage = "Design script file not found."
            };
        }

        bool simpleOk = await SimulateScriptAsync(
            script.LocalPath,
            Path.GetDirectoryName(script.LocalPath) ?? string.Empty,
            onOutput,
            cancellationToken);

        if (!simpleOk)
        {
            return new ScriptExecutionResult
            {
                Success = false,
                ExitCode = 1,
                RemoteScriptPath = $"/tmp/{script.FileName}",
                ErrorMessage = "Design script execution failed."
            };
        }

        onOutput?.Invoke("[design] Script completed successfully");

        return new ScriptExecutionResult
        {
            Success = true,
            ExitCode = 0,
            RemoteScriptPath = $"/tmp/{script.FileName}",
            ErrorMessage = string.Empty
        };
    }

    private static async Task<bool> SimulateScriptAsync(
        string scriptPath,
        string workingDirectory,
        Action<string>? onOutput,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(scriptPath))
        {
            onOutput?.Invoke($"[design] Missing script: {scriptPath}");
            return false;
        }

        foreach (var rawLine in File.ReadLines(scriptPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("#"))
                continue;

            var echoMatch = EchoRegex.Match(line);
            if (echoMatch.Success)
            {
                onOutput?.Invoke(echoMatch.Groups["text"].Value);
                await Task.Delay(150, cancellationToken);
                continue;
            }

            var shMatch = ShRegex.Match(line);
            if (shMatch.Success)
            {
                string childPath = NormalizeRelativePath(shMatch.Groups["path"].Value);
                string fullChildPath = Path.GetFullPath(Path.Combine(workingDirectory, childPath));

                bool ok = await SimulateScriptAsync(fullChildPath, Path.GetDirectoryName(fullChildPath) ?? workingDirectory, onOutput, cancellationToken);
                if (!ok)
                    return false;

                continue;
            }

            var catMatch = CatRegex.Match(line);
            if (catMatch.Success)
            {
                string filePath = NormalizeRelativePath(catMatch.Groups["path"].Value);
                string fullFilePath = Path.GetFullPath(Path.Combine(workingDirectory, filePath));

                if (!File.Exists(fullFilePath))
                {
                    onOutput?.Invoke($"[design] Missing file: {filePath}");
                    return false;
                }

                foreach (var fileLine in File.ReadAllLines(fullFilePath))
                {
                    onOutput?.Invoke(fileLine);
                }

                await Task.Delay(150, cancellationToken);
                continue;
            }
        }

        return true;
    }

    private static string NormalizeRelativePath(string path)
    {
        string trimmed = path.Trim();

        if (trimmed.StartsWith("\"") && trimmed.EndsWith("\"") && trimmed.Length >= 2)
        {
            trimmed = trimmed[1..^1];
        }

        if (trimmed.StartsWith("./", StringComparison.Ordinal))
        {
            trimmed = trimmed[2..];
        }

        return trimmed.Replace('/', Path.DirectorySeparatorChar);
    }
}