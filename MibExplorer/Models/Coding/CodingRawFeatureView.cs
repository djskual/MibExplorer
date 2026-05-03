using System.Collections.ObjectModel;
using MibExplorer.Core;

namespace MibExplorer.Models.Coding;

public sealed class CodingRawFeatureView : ObservableObject
{
    private readonly CodingFeatureValue _feature;

    public CodingRawFeatureView(CodingFeatureValue feature)
    {
        _feature = feature;
    }

    public string Label => _feature.Label;
    public int Byte => _feature.Byte;
    public int Mask => _feature.Mask;
    public int Shift => _feature.Shift;
    public int BitLength => _feature.BitLength;

    public bool IsSingleBit => BitLength == 1;
    public bool IsMultiBit => BitLength > 1;

    public string CurrentRawValue =>
        _feature.IsModified ? _feature.SelectedRawValue : _feature.RawValue;

    public string CurrentValue =>
        _feature.IsModified ? _feature.SelectedValue : _feature.Value;

    public bool IsChecked
    {
        get => CurrentRawValue == "1";
        set => ApplyRawValue(value ? "1" : "0");
    }

    public string BitLabel => BitLength == 1
        ? $"Bit {Shift}"
        : $"Bits {Shift}-{Shift + BitLength - 1}";

    public ObservableCollection<CodingFeatureOption> Options => _feature.Options;

    public CodingFeatureOption? CurrentOption
    {
        get => Options.FirstOrDefault(o =>
            string.Equals(o.Raw, CurrentRawValue, StringComparison.OrdinalIgnoreCase));
        set
        {
            if (value is null)
            {
                _feature.SelectedOption = null;
                NotifyAll();
                return;
            }

            ApplyRawValue(value.Raw);
        }
    }

    private void ApplyRawValue(string raw)
    {
        if (string.Equals(raw, _feature.RawValue, StringComparison.OrdinalIgnoreCase))
        {
            _feature.SelectedOption = null;
            NotifyAll();
            return;
        }

        CodingFeatureOption? option = _feature.Options.FirstOrDefault(o =>
            string.Equals(o.Raw, raw, StringComparison.OrdinalIgnoreCase));

        if (option is null)
        {
            option = new CodingFeatureOption
            {
                Raw = raw,
                Label = $"Unknown ({raw})"
            };

            _feature.Options.Add(option);
        }

        _feature.SelectedOption = option;
        NotifyAll();
    }

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(CurrentRawValue));
        OnPropertyChanged(nameof(CurrentValue));
        OnPropertyChanged(nameof(IsChecked));
        OnPropertyChanged(nameof(CurrentOption));
    }
}