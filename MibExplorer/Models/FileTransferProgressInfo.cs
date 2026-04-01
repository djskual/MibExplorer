namespace MibExplorer.Models;

public sealed class FileTransferProgressInfo
{
    public string Operation { get; init; } = "Transfer";
    public string SourcePath { get; init; } = string.Empty;
    public string DestinationPath { get; init; } = string.Empty;

    public ulong BytesTransferred { get; init; }
    public ulong? TotalBytes { get; init; }

    public bool HasKnownLength => TotalBytes.HasValue && TotalBytes.Value > 0;

    public double Percentage =>
        HasKnownLength
            ? Math.Clamp(BytesTransferred * 100d / TotalBytes!.Value, 0d, 100d)
            : 0d;
}
