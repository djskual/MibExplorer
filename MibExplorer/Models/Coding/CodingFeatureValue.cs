using System.Collections.ObjectModel;
using MibExplorer.Core;

namespace MibExplorer.Models.Coding;

public sealed class CodingFeatureValue : ObservableObject
{
    private CodingFeatureOption? _selectedOption;

    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
    public string RawValue { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;

    public int Byte { get; set; }
    public int Mask { get; set; }
    public int Shift { get; set; }
    public int BitLength { get; set; }

    public ObservableCollection<CodingFeatureOption> Options { get; } = new();

    public CodingFeatureOption? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (SetProperty(ref _selectedOption, value))
            {
                OnPropertyChanged(nameof(SelectedRawValue));
                OnPropertyChanged(nameof(SelectedValue));
                OnPropertyChanged(nameof(IsModified));
            }
        }
    }

    public string SelectedRawValue => SelectedOption?.Raw ?? string.Empty;

    public string SelectedValue => SelectedOption?.Label ?? string.Empty;

    public bool IsModified => SelectedOption is not null;
}