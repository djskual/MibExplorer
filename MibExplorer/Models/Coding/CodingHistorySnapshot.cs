namespace MibExplorer.Models.Coding;

public sealed class CodingHistorySnapshot
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string Reason { get; set; } = string.Empty;

    public string CodingHex { get; set; } = string.Empty;

    public int ByteCount { get; set; }

    public int ChangesCount { get; set; }

    public string Comment { get; set; } = string.Empty;

    public List<CodingPendingChange> PendingChanges { get; set; } = new();

    public string CreatedAtDisplay => CreatedAt.ToString("dd/MM/yyyy HH:mm:ss");

    public string ChangesDisplay => ChangesCount == 0
        ? "Snapshot"
        : $"{ChangesCount} change(s)";

    public string ShortCoding => CodingHex.Length <= 24
        ? CodingHex
        : $"{CodingHex[..24]}...";
}