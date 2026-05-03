namespace MibExplorer.Models.Coding;

public sealed class CodingWriteResult
{
    public string BeforeHex { get; init; } = string.Empty;

    public string AfterHex { get; init; } = string.Empty;

    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;
}