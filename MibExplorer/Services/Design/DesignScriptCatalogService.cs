using MibExplorer.Models.Scripting;
using MibExplorer.Services.Scripting;
using MibExplorer.Settings;
using System.IO;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace MibExplorer.Services.Design;

public sealed class DesignScriptCatalogService : IScriptCatalogService
{
    private sealed record ScriptHeaderInfo(string ScriptType, string Description); 
    public string ScriptsFolderPath => ResolveScriptsFolderPath();
    public string OfficialScriptsFolderPath => Path.Combine(ScriptsFolderPath, "Official");
    public string CustomScriptsFolderPath => Path.Combine(ScriptsFolderPath, "Custom");

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
        var root = ScriptsFolderPath;
        var official = OfficialScriptsFolderPath;
        var custom = CustomScriptsFolderPath;

        Directory.CreateDirectory(root);
        Directory.CreateDirectory(official);
        Directory.CreateDirectory(custom);

        CreateIfMissing(Path.Combine(official, "dump_variant.sh"),
@"#!/bin/sh
# Type: ReadOnly
# Dump current variant information
# Design mode sample script
echo ""[Design] dump_variant""
echo ""Reading variant...""
echo ""Done""");

        string packageRoot = Path.Combine(custom, "PackageDemo");
        Directory.CreateDirectory(packageRoot);
        Directory.CreateDirectory(Path.Combine(packageRoot, "scripts"));
        Directory.CreateDirectory(Path.Combine(packageRoot, "data"));

        CreateIfMissing(Path.Combine(packageRoot, "run.sh"),
@"#!/bin/sh
# Type: Apply
# Design package demo
# Demonstrates package execution in design mode
echo ""[Design] package demo start""
sh ./scripts/helper.sh
echo ""Data file contents:""
cat ./data/sample.txt
echo ""[Design] package demo end""");

        CreateIfMissing(Path.Combine(packageRoot, "scripts", "helper.sh"),
@"#!/bin/sh
echo ""[Design] helper script executed""");

        CreateIfMissing(Path.Combine(packageRoot, "data", "sample.txt"),
@"hello from package data");
    }

    private static void CreateIfMissing(string path, string content)
    {
        if (File.Exists(path))
            return;

        File.WriteAllText(path, content);
    }

    public IReadOnlyList<ScriptItem> GetScripts()
    {
        EnsureScriptsFolderExists();

        string officialScript = Path.Combine(OfficialScriptsFolderPath, "dump_variant.sh");
        string customPackageRun = Path.Combine(CustomScriptsFolderPath, "PackageDemo", "run.sh");

        return new List<ScriptItem>
        {
            new ScriptItem
            {
                Name = "dump_variant",
                FileName = "dump_variant.sh",
                LocalPath = officialScript,
                RelativePath = Path.Combine("Official", "dump_variant.sh"),
                Description = ReadHeaderInfo(officialScript).Description,
                ScriptType = ReadHeaderInfo(officialScript).ScriptType,
                IsPackage = false,
                PackageRootPath = string.Empty,
                IsOfficial = true
            },
            new ScriptItem
            {
                Name = "PackageDemo",
                FileName = "run.sh",
                LocalPath = customPackageRun,
                RelativePath = Path.Combine("Custom", "PackageDemo", "run.sh"),
                Description = ReadHeaderInfo(customPackageRun).Description,
                ScriptType = ReadHeaderInfo(customPackageRun).ScriptType,
                IsPackage = true,
                PackageRootPath = Path.Combine(CustomScriptsFolderPath, "PackageDemo"),
                IsOfficial = false
            }
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