namespace MibExplorer.Models.Adaptations;

public enum PhysicalWriteTransactionStatus
{
    Pending,
    PreflightVerified,
    PreflightFailed,
    AppliedLocally,
    Writing,
    Written,
    ReadbackVerified,
    Failed,
    RollbackNeeded,
    RolledBack
}

public sealed class PhysicalWriteTransaction
{
    public PhysicalStorageKey PhysicalKey { get; init; } =
        new(string.Empty, string.Empty, string.Empty);

    public string OriginalRawValue { get; init; } = string.Empty;

    public string MergedRawValue { get; init; } = string.Empty;

    public string RollbackRawValue => OriginalRawValue;

    public string ReadbackRawValue { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    public PhysicalWriteTransactionStatus Status { get; set; } =
        PhysicalWriteTransactionStatus.Pending;

    public DateTime? CompletedAtUtc { get; set; }

    public List<PendingAdaptationChange> Changes { get; } = new();

    public List<string> ValidationErrors { get; } = new();

    public bool RequiresReadbackVerification { get; init; } = true;

    public bool RequiresRollbackSnapshot { get; init; } = true;

    public bool IsDirty =>
        !string.Equals(
            OriginalRawValue,
            MergedRawValue,
            StringComparison.OrdinalIgnoreCase);

    public bool IsValid => ValidationErrors.Count == 0;

    public string TransactionDisplay =>
        $"{PhysicalKey} : {OriginalRawValue} -> {MergedRawValue}";
}