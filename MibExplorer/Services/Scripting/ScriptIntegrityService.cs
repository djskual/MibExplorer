using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MibExplorer.Services.Scripting;

public static class ScriptIntegrityService
{
    public static string ComputeSingleScriptSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);

        byte[] hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    public static string ComputePackageSha256(string packagePath)
    {
        using var sha256 = SHA256.Create();

        var files = Directory
            .EnumerateFiles(packagePath, "*", SearchOption.AllDirectories)
            .OrderBy(path => GetRelativePath(packagePath, path), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string file in files)
        {
            string relativePath = GetRelativePath(packagePath, file).Replace('\\', '/');

            byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath + "\n");
            sha256.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

            byte[] contentBytes = File.ReadAllBytes(file);
            sha256.TransformBlock(contentBytes, 0, contentBytes.Length, contentBytes, 0);

            byte[] separatorBytes = Encoding.UTF8.GetBytes("\n");
            sha256.TransformBlock(separatorBytes, 0, separatorBytes.Length, separatorBytes, 0);
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>());
    }

    private static string GetRelativePath(string rootPath, string filePath)
    {
        return Path.GetRelativePath(rootPath, filePath);
    }
}