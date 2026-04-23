using MibExplorer.Models.Scripting;
using MibExplorer.Settings;
using System.IO;

namespace MibExplorer.Services.Scripting;

public sealed class ScriptCatalogService : IScriptCatalogService
{
    private sealed record ScriptHeaderInfo(string ScriptType, string Description);
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

                        if (commentLines.Count >= 4)
                            break;

                        continue;
                    }
                }

                break;
            }

            if (commentLines.Count == 0)
                return new ScriptHeaderInfo("Unknown", string.Empty);

            string scriptType = "Unknown";
            int descriptionStartIndex = 0;

            string firstLine = commentLines[0];
            if (firstLine.StartsWith("Type:", StringComparison.OrdinalIgnoreCase))
            {
                string parsedType = firstLine.Substring("Type:".Length).Trim();
                if (!string.IsNullOrWhiteSpace(parsedType))
                {
                    scriptType = parsedType;
                }

                descriptionStartIndex = 1;
            }

            string description = string.Join(
                Environment.NewLine,
                commentLines.Skip(descriptionStartIndex).Take(3));

            return new ScriptHeaderInfo(scriptType, description);
        }
        catch
        {
            return new ScriptHeaderInfo("Unknown", string.Empty);
        }
    }
}