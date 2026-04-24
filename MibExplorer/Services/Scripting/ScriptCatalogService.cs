using MibExplorer.Models.Scripting;
using MibExplorer.Settings;
using System.IO;

namespace MibExplorer.Services.Scripting;

public sealed class ScriptCatalogService : IScriptCatalogService
{
    private sealed record ScriptHeaderInfo(string ScriptType, string Version, string Author, string Description);
    public string ScriptsFolderPath => ResolveScriptsFolderPath();
    public string OfficialScriptsFolderPath => Path.Combine(ScriptsFolderPath, "Official");
    public string CustomScriptsFolderPath => Path.Combine(ScriptsFolderPath, "Custom");

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
        Directory.CreateDirectory(OfficialScriptsFolderPath);
        Directory.CreateDirectory(CustomScriptsFolderPath);
    }

    public IReadOnlyList<ScriptItem> GetScripts()
    {
        EnsureScriptsFolderExists();

        var items = new List<ScriptItem>();

        LoadScriptsFromFolder(OfficialScriptsFolderPath, items, true);
        LoadScriptsFromFolder(CustomScriptsFolderPath, items, false);

        return items
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void LoadScriptsFromFolder(string folderPath, List<ScriptItem> items, bool isOfficial)
    {
        foreach (var file in Directory.EnumerateFiles(folderPath, "*.sh", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            items.Add(CreateSimpleScriptItem(file, folderPath, isOfficial));
        }

        foreach (var directory in Directory.EnumerateDirectories(folderPath, "*", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            string runPath = Path.Combine(directory, "run.sh");
            if (!File.Exists(runPath))
                continue;

            items.Add(CreatePackageScriptItem(directory, runPath, folderPath, isOfficial));
        }
    }

    private ScriptItem CreateSimpleScriptItem(string file, string baseFolder, bool isOfficial)
    {
        string fileName = Path.GetFileName(file);
        string name = Path.GetFileNameWithoutExtension(file);
        string relativePath = Path.GetRelativePath(ScriptsFolderPath, file);
        ScriptHeaderInfo header = ReadHeaderInfo(file);

        return new ScriptItem
        {
            Name = name,
            FileName = fileName,
            LocalPath = file,
            RelativePath = relativePath,
            Description = header.Description,
            ScriptType = header.ScriptType,
            Version = header.Version,
            Author = header.Author,
            IsPackage = false,
            PackageRootPath = string.Empty,
            IsOfficial = isOfficial
        };
    }

    private ScriptItem CreatePackageScriptItem(string directory, string runPath, string baseFolder, bool isOfficial)
    {
        string name = Path.GetFileName(directory);
        string relativePath = Path.GetRelativePath(ScriptsFolderPath, runPath);
        ScriptHeaderInfo header = ReadHeaderInfo(runPath);

        return new ScriptItem
        {
            Name = name,
            FileName = "run.sh",
            LocalPath = runPath,
            RelativePath = relativePath,
            Description = header.Description,
            ScriptType = header.ScriptType,
            Version = header.Version,
            Author = header.Author,
            IsPackage = true,
            PackageRootPath = directory,
            IsOfficial = isOfficial
        };
    }

    private static ScriptHeaderInfo ReadHeaderInfo(string path)
    {
        try
        {
            var commentLines = new List<string>();

            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line))
                {
                    if (commentLines.Count > 0)
                        continue;

                    continue;
                }

                if (line.StartsWith("#!"))
                    continue;

                if (line.StartsWith("#"))
                {
                    var text = line.TrimStart('#').Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        commentLines.Add(text);

                        if (commentLines.Count >= 6)
                            break;

                        continue;
                    }
                }

                break;
            }

            if (commentLines.Count == 0)
                return new ScriptHeaderInfo("Unknown", string.Empty, string.Empty, string.Empty);

            string scriptType = "Unknown";
            string version = string.Empty;
            string author = string.Empty;

            foreach (var line in commentLines)
            {
                if (line.StartsWith("Type:", StringComparison.OrdinalIgnoreCase))
                {
                    var parsedType = line.Substring("Type:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(parsedType))
                        scriptType = parsedType;
                }
                else if (line.StartsWith("Version:", StringComparison.OrdinalIgnoreCase))
                {
                    var parsedVersion = line.Substring("Version:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(parsedVersion))
                        version = parsedVersion;
                }
                else if (line.StartsWith("Author:", StringComparison.OrdinalIgnoreCase))
                {
                    var parsedAuthor = line.Substring("Author:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(parsedAuthor))
                        author = parsedAuthor;
                }
            }

            var descriptionLines = commentLines
                .Where(l => !l.StartsWith("Type:", StringComparison.OrdinalIgnoreCase)
                         && !l.StartsWith("Version:", StringComparison.OrdinalIgnoreCase)
                         && !l.StartsWith("Author:", StringComparison.OrdinalIgnoreCase))
                .Take(3);

            string description = string.Join(Environment.NewLine, descriptionLines);

            return new ScriptHeaderInfo(scriptType, version, author, description);
        }
        catch
        {
            return new ScriptHeaderInfo("Unknown", string.Empty, string.Empty, string.Empty);
        }
    }
}