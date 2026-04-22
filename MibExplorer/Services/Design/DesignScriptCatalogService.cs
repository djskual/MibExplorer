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

        Directory.CreateDirectory(root);

        CreateIfMissing(Path.Combine(root, "dump_variant.sh"),
@"#!/bin/sh
# Type: ReadOnly
# Dump current variant information
# Design mode sample script
echo ""[Design] dump_variant""
echo ""Reading variant...""
echo ""Done""");

        string packageRoot = Path.Combine(root, "PackageDemo");
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

        return new List<ScriptItem>
        {
            new ScriptItem
            {
                Name = "dump_variant",
                FileName = "dump_variant.sh",
                LocalPath = Path.Combine(ScriptsFolderPath, "dump_variant.sh"),
                RelativePath = "dump_variant.sh",
                Description = ReadHeaderInfo(Path.Combine(ScriptsFolderPath, "dump_variant.sh")).Description,
                ScriptType = ReadHeaderInfo(Path.Combine(ScriptsFolderPath, "dump_variant.sh")).ScriptType,
                IsPackage = false,
                PackageRootPath = string.Empty
            },
            new ScriptItem
            {
                Name = "PackageDemo",
                FileName = "run.sh",
                LocalPath = Path.Combine(ScriptsFolderPath, "PackageDemo", "run.sh"),
                RelativePath = @"PackageDemo\run.sh",
                Description = ReadHeaderInfo(Path.Combine(ScriptsFolderPath, "PackageDemo", "run.sh")).Description,
                ScriptType = ReadHeaderInfo(Path.Combine(ScriptsFolderPath, "PackageDemo", "run.sh")).ScriptType,
                IsPackage = true,
                PackageRootPath = Path.Combine(ScriptsFolderPath, "PackageDemo")
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