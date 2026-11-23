using System;
using System.Collections.Generic;

namespace MibExplorer.Models;

/// <summary>
/// Resolve a symbolic link as seen from the node that owns it, not from the
/// MMX SSH shell. Qnet does not automatically rebase absolute link targets:
/// /net/rcc/tmp -> /dev/shmem would otherwise open MMX /dev/shmem.
/// This helper never probes, changes, or creates anything on the MIB.
/// </summary>
public static class RemoteQnetLinkPath
{
    public static string Resolve(string linkPath, string target)
    {
        if (string.IsNullOrWhiteSpace(linkPath) || !linkPath.StartsWith("/", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(target))
            throw new ArgumentException("A link path and its target are required.");

        // /net/rcc, /net/rcc.mibhigh.net, /net/mmx, etc. All refer to a
        // distinct Qnet namespace. Do not treat /net itself as a node root.
        string? nodeRoot = GetNodeRoot(linkPath);
        string fullTarget;
        if (target.StartsWith("/net/", StringComparison.Ordinal))
        {
            // A link that explicitly names another Qnet node retains that node.
            fullTarget = target;
        }
        else if (target.StartsWith("/", StringComparison.Ordinal))
        {
            // Absolute target means the root of the node owning the link.
            fullTarget = (nodeRoot ?? string.Empty) + target;
        }
        else
        {
            int slash = linkPath.LastIndexOf('/');
            string parent = slash == 0 ? "/" : linkPath[..slash];
            fullTarget = parent.TrimEnd('/') + "/" + target;
        }

        return Normalize(fullTarget);
    }

    private static string? GetNodeRoot(string path)
    {
        if (!path.StartsWith("/net/", StringComparison.Ordinal))
            return null;

        int slash = path.IndexOf('/', "/net/".Length);
        string node = slash < 0 ? path : path[..slash];
        return node.Length > "/net/".Length ? node : null;
    }

    private static string Normalize(string path)
    {
        string? nodeRoot = GetNodeRoot(path);
        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        int minimumParts = nodeRoot is null ? 0 : 2; // Never escape /net/<node>.
        var resolved = new List<string>();
        foreach (string part in parts)
        {
            if (part == ".")
                continue;
            if (part == "..")
            {
                if (resolved.Count <= minimumParts)
                    throw new InvalidOperationException("Symbolic link would escape its filesystem root.");
                resolved.RemoveAt(resolved.Count - 1);
            }
            else
            {
                resolved.Add(part);
            }
        }
        return "/" + string.Join("/", resolved);
    }
}
