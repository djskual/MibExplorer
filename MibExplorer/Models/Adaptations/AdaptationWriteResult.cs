namespace MibExplorer.Models.Adaptations;

public sealed class AdaptationWriteResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public List<AdaptationPhysicalWriteResult> PhysicalResults { get; } = new();
}

public sealed class AdaptationPhysicalWriteResult
{
    public PhysicalStorageKey PhysicalKey { get; init; } =
        new(string.Empty, string.Empty, string.Empty);

    public string ExpectedOriginalRawValue { get; init; } = string.Empty;

    public string TargetRawValue { get; init; } = string.Empty;

    public string ReadbackRawValue { get; init; } = string.Empty;

    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;
}