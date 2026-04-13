namespace MibExplorer.Services;

public sealed partial class SshMibConnectionService
{
    private static string NormalizeRemotePath(string remotePath)
    {
        if (string.IsNullOrWhiteSpace(remotePath))
            return "/";

        return remotePath.Trim();
    }

    private static string BuildTemporaryRemotePath(string remotePath)
    {
        return remotePath + ".__mibexplorer_tmp__";
    }

    private static string BuildBackupRemotePath(string remotePath)
    {
        return remotePath + ".__mibexplorer_bak__";
    }

    private static string EscapeShellArg(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return "''";

        return "'" + arg.Replace("'", "'\"'\"'") + "'";
    }
}
