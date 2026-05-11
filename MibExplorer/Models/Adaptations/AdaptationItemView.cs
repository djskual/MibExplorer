using MibExplorer.Core;
using System.Collections.ObjectModel;

namespace MibExplorer.Models.Adaptations;

public sealed class AdaptationItemView : ObservableObject
{
    private string _rawValue = string.Empty;
    private string _currentValue = string.Empty;
    private string? _editValue;

    public string Id { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
    public string FunctionLabel { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string RawLabel { get; init; } = string.Empty;
    public string HtmlLabel { get; init; } = string.Empty;
    public string Partition { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string StorageMode { get; init; } = string.Empty;
    public int? Mask { get; init; }
    public int? Shift { get; init; }
    public string ValueType { get; init; } = string.Empty;
    public string Control { get; init; } = string.Empty;
    public string Confidence { get; init; } = string.Empty;
    public string Safety { get; init; } = string.Empty;
    public bool Writable { get; init; }
    public string Notes { get; init; } = string.Empty;
    public bool IsStorageMapped { get; init; }
    public int? ByteLength { get; init; }

    public string DiagnosticSummary
    {
        get
        {
            string storage = IsStorageMapped
                ? $"mapped {KeyDisplay} ({Type}) mode={StorageMode}"
                : "storage unmapped";

            string bounds = MinValue is null && MaxValue is null
                ? string.Empty
                : $" | range={MinValue?.ToString() ?? "-"}..{MaxValue?.ToString() ?? "-"}";

            string bytes = ByteLength is null
                ? string.Empty
                : $" | bytes={ByteLength}";

            return $"id={Id} | rawLabel={RawLabel} | rawValue={RawValue} | type={ValueType} | editor={Control} | {storage}{bounds}{bytes}";
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
                OnPropertyChanged(nameof(HasPendingChange));
        }
    }

    public string CacheKey => AdaptationReadValue.BuildCacheKey(Partition, Key, Type);

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

            if (!string.IsNullOrWhiteSpace(normalized)
                && string.Equals(normalized, CurrentValue, StringComparison.OrdinalIgnoreCase))
            {
                normalized = null;
            }

            if (SetProperty(ref _editValue, normalized))
            {
                OnPropertyChanged(nameof(HasPendingChange));
                OnPropertyChanged(nameof(SelectedEditOption));
            }
        }
    }

    public bool HasPendingChange =>
        !string.IsNullOrWhiteSpace(EditValue)
        && !string.Equals(EditValue, CurrentValue, StringComparison.OrdinalIgnoreCase);

    public ObservableCollection<AdaptationEditOption> EditOptions { get; } = new();

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

    public AdaptationEditOption? SelectedEditOption
    {
        get => EditOptions.FirstOrDefault(o =>
            string.Equals(o.Label, EditValue, StringComparison.OrdinalIgnoreCase));

        set => EditValue = value?.Label;
    }

    public string EditRawValue
    {
        get
        {
            AdaptationEditOption? option = EditOptions.FirstOrDefault(o =>
                string.Equals(o.Label, EditValue, StringComparison.OrdinalIgnoreCase));

            return option?.Value ?? EditValue ?? string.Empty;
        }
    }
}
