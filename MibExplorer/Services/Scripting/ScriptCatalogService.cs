using MibExplorer.Models.Scripting;
using MibExplorer.Settings;
using System.IO;

namespace MibExplorer.Services.Scripting;

public sealed class ScriptCatalogService : IScriptCatalogService
{
    public string ScriptsFolderPath => ResolveScriptsFolderPath();

    public ScriptCatalogService()
    {
    }

    private static string ResolveScriptsFolderPath()
    {
        var configuredPath = AppSettingsStore.Current.ScriptsFolderPath;

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        return Path.Combine(AppContext.BaseDirectory, "Scripts");
    }

    public void EnsureScriptsFolderExists()
    {
        Directory.CreateDirectory(ScriptsFolderPath);
    }

    public IReadOnlyList<ScriptItem> GetScripts()
    {
        EnsureScriptsFolderExists();

        var items = new List<ScriptItem>();

        foreach (var file in Directory.EnumerateFiles(ScriptsFolderPath, "*.sh", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            items.Add(CreateSimpleScriptItem(file));
        }

        foreach (var directory in Directory.EnumerateDirectories(ScriptsFolderPath, "*", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            string runPath = Path.Combine(directory, "run.sh");
            if (!File.Exists(runPath))
                continue;

            items.Add(CreatePackageScriptItem(directory, runPath));
        }

        return items;
    }

    private ScriptItem CreateSimpleScriptItem(string file)
    {
        string fileName = Path.GetFileName(file);
        string name = Path.GetFileNameWithoutExtension(file);
        string relativePath = Path.GetRelativePath(ScriptsFolderPath, file);
        string extension = Path.GetExtension(file);

        return new ScriptItem
        {
            Name = name,
            FileName = fileName,
            LocalPath = file,
            RelativePath = relativePath,
            Description = ReadDescription(file),
            Category = "Custom",
            IsUserScript = true,
            IsRunnable = true,
            Extension = extension,
            IsPackage = false,
            PackageRootPath = string.Empty
        };
    }

    private ScriptItem CreatePackageScriptItem(string directory, string runPath)
    {
        string name = Path.GetFileName(directory);
        string relativePath = Path.GetRelativePath(ScriptsFolderPath, runPath);

        return new ScriptItem
        {
            Name = name,
            FileName = "run.sh",
            LocalPath = runPath,
            RelativePath = relativePath,
            Description = ReadDescription(runPath),
            Category = "Custom",
            IsUserScript = true,
            IsRunnable = true,
            Extension = ".sh",
            IsPackage = true,
            PackageRootPath = directory
        };
    }

    private static string ReadDescription(string path)
    {
        try
        {
            var descriptionLines = new List<string>();

            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line))
                {
                    if (descriptionLines.Count > 0)
                        break;

                    continue;
                }

                if (line.StartsWith("#!"))
                    continue;

                if (line.StartsWith("#"))
                {
                    var text = line.TrimStart('#').Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        descriptionLines.Add(text);

                        if (descriptionLines.Count >= 3)
                            break;

                        continue;
                    }
                }

                break;
            }

            if (descriptionLines.Count > 0)
                return string.Join(Environment.NewLine, descriptionLines);
        }
        catch
        {
        }

        return string.Empty;
    }
}