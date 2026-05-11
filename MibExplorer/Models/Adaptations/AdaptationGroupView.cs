using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using MibExplorer.Core;

namespace MibExplorer.Models.Adaptations;

public sealed class AdaptationGroupView : ObservableObject
{
    private bool _isExpanded;
    private bool _isLoading;
    private bool _isLoaded;
    private string _status = "Not loaded";

    public AdaptationGroupView()
    {
        AdaptationsView = CollectionViewSource.GetDefaultView(Adaptations);

        Adaptations.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(VisibleCount));
            OnPropertyChanged(nameof(HeaderText));
            OnPropertyChanged(nameof(HasChildren));
            OnPropertyChanged(nameof(ExpandGlyph));
        };
    }

    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Order { get; init; }

    public ObservableCollection<AdaptationItemView> Adaptations { get; } = new();
    public ICollectionView AdaptationsView { get; }
    public List<AdaptationDefinition> ReadDefinitions { get; } = new();

    public Action<AdaptationGroupView, bool>? ExpansionChanged { get; set; }

    public bool HasChildren => Adaptations.Count > 0;

    public string ExpandGlyph
    {
        get
        {
            if (!HasChildren)
                return "";

            return IsExpanded ? "▼" : "▶";
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(ExpandGlyph));
                ExpansionChanged?.Invoke(this, value);
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsLoaded
    {
        get => _isLoaded;
        set => SetProperty(ref _isLoaded, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public int VisibleCount => Adaptations.Count;

    public string HeaderText => $"{Label} ({VisibleCount})";
}
