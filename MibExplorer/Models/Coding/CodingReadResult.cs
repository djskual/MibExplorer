namespace MibExplorer.Models.Coding;

public sealed class CodingReadResult
{
    public string CodingHex { get; init; } = string.Empty;

    public int ByteCount { get; init; }

    public string Vin { get; init; } = "UNKNOWN";

    public IReadOnlyList<CodingByte> Bytes { get; init; } = Array.Empty<CodingByte>();
}