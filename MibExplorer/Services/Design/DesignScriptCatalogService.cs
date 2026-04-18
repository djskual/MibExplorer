using MibExplorer.Models.Scripting;
using MibExplorer.Services.Scripting;
using MibExplorer.Settings;
using System.IO;

namespace MibExplorer.Services.Design;

public sealed class DesignScriptCatalogService : IScriptCatalogService
{
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
# Dump current variant information
echo ""[Design] dump_variant""
echo ""Reading variant...""
echo ""Done""");

        string packageRoot = Path.Combine(root, "PackageDemo");
        Directory.CreateDirectory(packageRoot);
        Directory.CreateDirectory(Path.Combine(packageRoot, "scripts"));
        Directory.CreateDirectory(Path.Combine(packageRoot, "data"));

        CreateIfMissing(Path.Combine(packageRoot, "run.sh"),
@"#!/bin/sh
# Design package demo
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
                Description = ReadDescription(Path.Combine(ScriptsFolderPath, "dump_variant.sh")),
                Category = "Custom",
                IsUserScript = true,
                IsRunnable = true,
                Extension = ".sh",
                IsPackage = false,
                PackageRootPath = string.Empty
            },
            new ScriptItem
            {
                Name = "PackageDemo",
                FileName = "run.sh",
                LocalPath = Path.Combine(ScriptsFolderPath, "PackageDemo", "run.sh"),
                RelativePath = @"PackageDemo\run.sh",
                Description = ReadDescription(Path.Combine(ScriptsFolderPath, "PackageDemo", "run.sh")),
                Category = "Custom",
                IsUserScript = true,
                IsRunnable = true,
                Extension = ".sh",
                IsPackage = true,
                PackageRootPath = Path.Combine(ScriptsFolderPath, "PackageDemo")
            }
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