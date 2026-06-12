using MibExplorer.Core;
using System.Collections.ObjectModel;

namespace MibExplorer.Models.Adaptations;

public sealed class AdaptationItemView : ObservableObject
{
    private string _rawValue = string.Empty;
    private string _currentValue = string.Empty;
    private string _storageWarning = string.Empty;
    private string? _editValue;

    public event EventHandler? PendingChangeChanged;

    public string Id { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
    public string FunctionLabel { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string RawLabel { get; init; } = string.Empty;
    public string HtmlLabel { get; init; } = string.Empty;
    public string Partition { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public IReadOnlyList<PhysicalStorageKey> PhysicalKeys { get; init; } = Array.Empty<PhysicalStorageKey>();
    public bool IsMultiStorage =>
        PhysicalKeys.Count > 1
        || string.Equals(StorageMode, "multiStorage", StringComparison.OrdinalIgnoreCase);
    public string StorageMode { get; init; } = string.Empty;
    public int? Mask { get; init; }
    public int? Shift { get; init; }
    public int? BitWidth { get; init; }
    public int? ByteIndex { get; init; }
    public int? BitIndex { get; init; }
    public string ValueType { get; init; } = string.Empty;
    public string Control { get; init; } = string.Empty;
    public string Confidence { get; init; } = string.Empty;
    public string Safety { get; init; } = string.Empty;
    public bool Writable { get; init; }
    public string Notes { get; init; } = string.Empty;
    public bool IsStorageMapped { get; init; }
    public int? ByteLength { get; init; }

    public string StorageMappedToolTip =>
        IsStorageMapped
        ? $"Mapped: {KeyDisplay} ({Type})"
        : "Storage unmapped";

    public string DiagnosticSummary
    {
        get
        {
            string extra = StorageMode.Equals("packedFlags", StringComparison.OrdinalIgnoreCase)
                ? $" mask={MaskDisplay} shift={Shift?.ToString() ?? "-"}"
                : StorageMode.Equals("blobBit", StringComparison.OrdinalIgnoreCase)
                    ? $" byte={ByteIndex?.ToString() ?? "-"} bit={BitIndex?.ToString() ?? "-"}"
                    : string.Empty;

            string storage = IsStorageMapped
                ? $"mapped {KeyDisplay} ({Type}) mode={StorageMode}{extra}"
                : "storage unmapped";

            string bounds = MinValue is null && MaxValue is null
                ? string.Empty
                : $" | range={MinValue?.ToString() ?? "-"}..{MaxValue?.ToString() ?? "-"}";

            string bytes = ByteLength is null
                ? string.Empty
                : $" | bytes={ByteLength}";

            string warning = string.IsNullOrWhiteSpace(StorageWarning)
                ? string.Empty
                : $" | warning={StorageWarning}";

            return $"id={Id} | rawLabel={RawLabel} | rawValue={RawValue} | type={ValueType} | editor={Control} | {storage}{bounds}{bytes}{warning}";
        }
    }

    public string RawValue
    {
        get => _rawValue;
        set => SetProperty(ref _rawValue, value);
    }

    public string CurrentValue
    {
        get => _currentValue;
        set
        {
            if (SetProperty(ref _currentValue, value))
            {
                OnPropertyChanged(nameof(HasPendingChange));
                OnPropertyChanged(nameof(IsModified));
                PendingChangeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string StorageWarning
    {
        get => _storageWarning;
        set
        {
            if (SetProperty(ref _storageWarning, value))
                OnPropertyChanged(nameof(HasStorageWarning));
        }
    }

    public bool HasStorageWarning => !string.IsNullOrWhiteSpace(StorageWarning);

    public string CacheKey
    {
        get
        {
            PhysicalStorageKey? firstKey = PhysicalKeys.FirstOrDefault();

            if (firstKey is not null)
            {
                return AdaptationReadValue.BuildCacheKey(
                    firstKey.Partition,
                    firstKey.Key,
                    firstKey.Type);
            }

            return AdaptationReadValue.BuildCacheKey(Partition, Key, Type);
        }
    }

    public IReadOnlyList<string> CacheKeys =>
        PhysicalKeys.Count > 0
            ? PhysicalKeys
                .Select(k => AdaptationReadValue.BuildCacheKey(k.Partition, k.Key, k.Type))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new[] { CacheKey };

    public string KeyDisplay => string.IsNullOrWhiteSpace(Key)
        ? "-"
        : $"{Partition}:{Key}";

    public string WritableDisplay => Writable ? "Yes" : "No";

    public string MaskDisplay => Mask is null ? "-" : $"0x{Mask.Value:X}";

    public string? EditValue
    {
        get => _editValue;
        set
        {
            string? normalized = string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();

            if (IsSameAsCurrentValue(normalized))
                normalized = null;

            if (SetProperty(ref _editValue, normalized))
            {
                OnPropertyChanged(nameof(HasPendingChange));
                OnPropertyChanged(nameof(IsModified));
                PendingChangeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private bool IsSameAsCurrentValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (string.Equals(value, CurrentValue, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(value, RawValue, StringComparison.OrdinalIgnoreCase))
            return true;

        AdaptationEditOption? option = EditOptions.FirstOrDefault(o =>
            string.Equals(o.Label, value, StringComparison.OrdinalIgnoreCase)
            || string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase));

        if (option is null)
            return false;

        return string.Equals(option.Label, CurrentValue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(option.Value, CurrentValue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(option.Label, RawValue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(option.Value, RawValue, StringComparison.OrdinalIgnoreCase);
    }

    public bool HasPendingChange =>
        !string.IsNullOrWhiteSpace(EditValue)
        && !string.Equals(EditValue, CurrentValue, StringComparison.OrdinalIgnoreCase);

    public bool IsModified => HasPendingChange;

    public ObservableCollection<AdaptationEditOption> EditOptions { get; } = new();

    public ObservableCollection<AdaptationEditOption> StorageOptions { get; } = new();

    public double? MinValue { get; init; }
    public double? MaxValue { get; init; }
    public string Unit { get; init; } = string.Empty;
    public bool DangerousWrite { get; init; }
    public bool LimitedWriteCount { get; init; }
    public bool NoDumpNeeded { get; init; }
    public bool BlockedFreeInput { get; init; }

    public bool IsComboEditorVisible =>
        string.Equals(Control, "combo", StringComparison.OrdinalIgnoreCase)
        && EditOptions.Count > 0;

    public bool IsNumericEditorVisible =>
        string.Equals(Control, "numeric", StringComparison.OrdinalIgnoreCase);

    public bool IsTextEditorVisible =>
        string.Equals(Control, "text", StringComparison.OrdinalIgnoreCase);

    public bool IsHexBytesEditorVisible =>
        string.Equals(Control, "hexBytes", StringComparison.OrdinalIgnoreCase);

    public bool IsReadOnlyEditorVisible =>
        !IsComboEditorVisible
        && !IsNumericEditorVisible
        && !IsTextEditorVisible
        && !IsHexBytesEditorVisible;

    public bool IsIntegerNumeric =>
        string.Equals(ValueType, "int", StringComparison.OrdinalIgnoreCase)
        || string.Equals(ValueType, "uint", StringComparison.OrdinalIgnoreCase);

    public string EditRawValue
    {
        get
        {
            if (string.IsNullOrWhiteSpace(EditValue))
                return string.Empty;

            AdaptationEditOption? option = EditOptions.FirstOrDefault(o =>
                string.Equals(o.Label, EditValue, StringComparison.OrdinalIgnoreCase)
                || string.Equals(o.Value, EditValue, StringComparison.OrdinalIgnoreCase));

            return option?.Value ?? EditValue;
        }
    }
}
