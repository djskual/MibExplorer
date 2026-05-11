using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows;
using MibExplorer.Core;
using MibExplorer.Models.Adaptations;
using MibExplorer.Services.Adaptations;
using MibExplorer.Views.Dialogs;

namespace MibExplorer.ViewModels;

public sealed class AdaptationsCenterViewModel : ObservableObject
{
    private readonly AdaptationCatalogService _catalogService;
    private readonly IAdaptationsCenterService _adaptationsService;
    private AdaptationCatalog? _catalog;
    private AdaptationGroupView? _expandedGroup;
    private AdaptationItemView? _selectedAdaptation;
    private string _searchText = string.Empty;
    private string _statusText = "Ready.";
    private bool _isLoading;
    private bool _showDiagnostics;

    private bool IsDesignMode => _adaptationsService is DesignAdaptationsCenterService;

    public AdaptationsCenterViewModel()
        : this(new AdaptationCatalogService(), new DesignAdaptationsCenterService())
    {
    }

    public AdaptationsCenterViewModel(
        AdaptationCatalogService catalogService,
        IAdaptationsCenterService adaptationsService)
    {
        _catalogService = catalogService;
        _adaptationsService = adaptationsService;

        ReloadCommand = new RelayCommand(
            _ => ReloadExpandedGroup(),
            _ => !IsLoading && _expandedGroup is not null);
        ApplyCommand = new RelayCommand(
            _ => ApplyExpandedGroup(),
            _ => _expandedGroup is not null && HasPendingChanges(_expandedGroup));
        DiscardCommand = new RelayCommand(
            _ => DiscardExpandedGroupWithConfirmation(),
            _ => _expandedGroup is not null && HasPendingChanges(_expandedGroup));
        ClearSearchCommand = new RelayCommand(
            _ => SearchText = string.Empty,
            _ => !string.IsNullOrWhiteSpace(SearchText));

        GroupsView = CollectionViewSource.GetDefaultView(Groups);
        GroupsView.Filter = FilterGroup;

        LoadCatalog();
    }

    public ObservableCollection<AdaptationGroupView> Groups { get; } = new();

    public ICollectionView GroupsView { get; }

    public AdaptationItemView? SelectedAdaptation
    {
        get => _selectedAdaptation;
        set => SetProperty(ref _selectedAdaptation, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshAllGroupFilters();
                ClearSearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }


    public bool ShowDiagnostics
    {
        get => _showDiagnostics;
        set => SetProperty(ref _showDiagnostics, value);
    }

    public RelayCommand ReloadCommand { get; }
    public RelayCommand ApplyCommand { get; }
    public RelayCommand DiscardCommand { get; }
    public RelayCommand ClearSearchCommand { get; }

    private static bool ShouldShowGroup(AdaptationCatalogGroup group)
    {
        if (!group.DefaultVisibleInMibExplorer)
            return false;

        return group.Adaptations.Any(a => a.Ui.VisibleByDefault);
    }

    private void LoadCatalog()
    {
        _catalog = _catalogService.Load();

        Groups.Clear();

        if (!_catalog.IsV4)
            throw new InvalidOperationException("Adaptations Center expects catalog V4 groups[].");

        foreach (AdaptationCatalogGroup catalogGroup in _catalog.Groups
                     .Where(ShouldShowGroup)
                     .OrderBy(g => g.Order)
                     .ThenBy(g => g.Label))
        {
            var groupView = new AdaptationGroupView
            {
                Id = catalogGroup.Id,
                Label = BuildGroupLabel(catalogGroup),
                Order = catalogGroup.Order,
                Status = "Collapsed"
            };

            groupView.AdaptationsView.Filter = FilterAdaptation;
            groupView.ExpansionChanged = OnGroupExpansionChanged;

            LoadGroupItems(catalogGroup, groupView);

            Groups.Add(groupView);
        }

        int visibleCount = Groups.Sum(g => g.Adaptations.Count);
        StatusText = $"{Groups.Count} ODIS groups loaded. {visibleCount} adaptations available from catalog V4. Expand a group to inspect values.";
    }

    private static string BuildGroupLabel(AdaptationCatalogGroup group)
    {
        string label = FirstNonEmpty(group.OdisOriginalGroupLabel, group.Label, group.OdisRawGroupLabel, group.HtmlRawGroupLabel);

        return string.IsNullOrWhiteSpace(group.Rdid)
            ? label
            : $"{group.Rdid} - {label}";
    }

    private void LoadGroupItems(
        AdaptationCatalogGroup catalogGroup,
        AdaptationGroupView groupView)
    {
        foreach (AdaptationCatalogItem catalogItem in catalogGroup.Adaptations
                     .Where(a => a.Ui.VisibleByDefault)
                     .OrderBy(a => a.Order)
                     .ThenBy(a => a.Label))
        {
            if (catalogItem.Storage.Mapped
                && !string.IsNullOrWhiteSpace(catalogItem.Storage.Partition)
                && !string.IsNullOrWhiteSpace(catalogItem.Storage.Key)
                && !string.IsNullOrWhiteSpace(catalogItem.Storage.Type))
            {
                groupView.ReadDefinitions.Add(CreateReadDefinition(catalogGroup, catalogItem));
            }

            string currentValue = FirstNonEmpty(catalogItem.CurrentValue, catalogItem.CurrentValueRaw, "-");
            string editor = NormalizeEditor(catalogItem.Ui.Editor, catalogItem.ValueType, catalogItem.Ui.Values.Count);

            var item = new AdaptationItemView
            {
                Id = catalogItem.Id,
                Group = groupView.Label,
                FunctionLabel = string.Empty,
                Label = BuildAdaptationLabel(catalogItem),
                RawLabel = catalogItem.RawLabel,
                HtmlLabel = catalogItem.HtmlLabel,
                RawValue = FirstNonEmpty(catalogItem.CurrentValueRaw, catalogItem.CurrentValue, "-"),
                CurrentValue = string.IsNullOrWhiteSpace(currentValue) ? "-" : currentValue,
                Partition = catalogItem.Storage.Partition ?? string.Empty,
                Key = catalogItem.Storage.Key ?? string.Empty,
                Type = catalogItem.Storage.Type ?? string.Empty,
                StorageMode = catalogItem.Storage.Mode,
                ValueType = catalogItem.ValueType,
                Control = editor,
                Confidence = catalogItem.Storage.Mapped ? FirstNonEmpty(catalogItem.Storage.MappingSource, "mapped") : "ODIS only",
                Safety = catalogItem.Ui.Editable ? "review" : "read_only",
                Writable = catalogItem.Ui.Editable && catalogItem.Storage.Mapped,
                Notes = BuildV4Notes(catalogItem),
                MinValue = catalogItem.Ui.Min,
                MaxValue = catalogItem.Ui.Max,
                Unit = catalogItem.Unit,
                DangerousWrite = catalogItem.Ui.Dangerous,
                LimitedWriteCount = catalogItem.Ui.LimitedWriteCount,
                ByteLength = catalogItem.Ui.ByteLength,
                IsStorageMapped = catalogItem.Storage.Mapped
            };

            PopulateEditOptions(item, catalogItem);
            item.CurrentValue = DecodeCatalogValue(item, item.CurrentValue);
            item.EditValue = null;

            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AdaptationItemView.EditValue)
                    || e.PropertyName == nameof(AdaptationItemView.HasPendingChange))
                {
                    ApplyCommand.RaiseCanExecuteChanged();
                    DiscardCommand.RaiseCanExecuteChanged();
                }
            };

            groupView.Adaptations.Add(item);
        }
    }

    private static string BuildAdaptationLabel(AdaptationCatalogItem item)
    {
        string label = FirstNonEmpty(item.HtmlLabel, item.Label, item.RawLabel, item.Id);
        return string.Equals(label, "---", StringComparison.OrdinalIgnoreCase)
            ? FirstNonEmpty(item.Label, item.RawLabel, item.Id)
            : label;
    }

    private static string NormalizeEditor(string editor, string valueType, int enumValueCount)
    {
        if (!string.IsNullOrWhiteSpace(editor)
            && !string.Equals(editor, "readOnlyUntilStorageMapping", StringComparison.OrdinalIgnoreCase))
            return editor.Trim();

        if (enumValueCount > 0)
            return "combo";

        if (string.Equals(valueType, "int", StringComparison.OrdinalIgnoreCase)
            || string.Equals(valueType, "uint", StringComparison.OrdinalIgnoreCase)
            || string.Equals(valueType, "float", StringComparison.OrdinalIgnoreCase)
            || string.Equals(valueType, "double", StringComparison.OrdinalIgnoreCase)
            || string.Equals(valueType, "decimal", StringComparison.OrdinalIgnoreCase))
            return "numeric";

        if (string.Equals(valueType, "bytes", StringComparison.OrdinalIgnoreCase))
            return "hexBytes";

        if (string.Equals(valueType, "string", StringComparison.OrdinalIgnoreCase))
            return "text";

        return "readOnly";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static void PopulateEditOptions(
        AdaptationItemView item,
        AdaptationCatalogItem catalogItem)
    {
        foreach (AdaptationCatalogEnumValue value in catalogItem.Ui.Values)
        {
            if (string.Equals(value.Label, "Reset selection", StringComparison.OrdinalIgnoreCase))
                continue;

            item.EditOptions.Add(new AdaptationEditOption
            {
                Value = string.IsNullOrWhiteSpace(value.Raw) ? value.Label : value.Raw,
                Label = value.Label
            });
        }
    }

    private static string DecodeCatalogValue(AdaptationItemView item, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "-";

        AdaptationEditOption? option = item.EditOptions.FirstOrDefault(o =>
            string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase)
            || string.Equals(o.Label, value, StringComparison.OrdinalIgnoreCase));

        return option?.Label ?? value;
    }

    public async Task ToggleGroupAsync(AdaptationGroupView group)
    {
        if (IsLoading)
            return;

        if (group.IsExpanded)
        {
            if (!ConfirmLosePendingChanges(group))
                return;

            DiscardGroupChanges(group);
            group.IsExpanded = false;
            return;
        }

        if (_expandedGroup is not null && !ReferenceEquals(_expandedGroup, group))
        {
            if (!ConfirmLosePendingChanges(_expandedGroup))
                return;

            DiscardGroupChanges(_expandedGroup);
            _expandedGroup.IsExpanded = false;
        }

        _expandedGroup = group;
        SelectedAdaptation = null;
        RefreshActionCommands();

        if (!group.IsLoaded)
            await ReadGroupAsync(group);

        group.IsExpanded = true;
        RefreshActionCommands();
    }

    private void OnGroupExpansionChanged(AdaptationGroupView group, bool isExpanded)
    {
        if (!isExpanded)
        {
            if (ReferenceEquals(_expandedGroup, group))
                _expandedGroup = null;

            group.Status = group.IsLoaded ? "Loaded" : "Collapsed";
            RefreshActionCommands();
            return;
        }

        _expandedGroup = group;
        SelectedAdaptation = null;
        RefreshActionCommands();
    }

    private async void ReloadExpandedGroup()
    {
        if (_expandedGroup is null)
            return;

        await ReadGroupAsync(_expandedGroup);
    }

    private bool ConfirmLosePendingChanges(AdaptationGroupView group)
    {
        if (!HasPendingChanges(group))
            return true;

        MessageBoxResult result = AppMessageBox.Show(
            $"The group \"{group.Label}\" contains unapplied changes.\n\n" +
            "If you close this group or open another one, these changes will be lost.\n\n" +
            "Discard changes?",
            "Unsaved adaptation changes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    private async void DiscardExpandedGroupWithConfirmation()
    {
        if (_expandedGroup is null || !HasPendingChanges(_expandedGroup))
            return;

        MessageBoxResult result = AppMessageBox.Show(
            $"Discard all pending changes in \"{_expandedGroup.Label}\"?",
            "Discard changes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        AdaptationGroupView group = _expandedGroup;

        try
        {
            IsLoading = true;
            group.IsLoading = true;
            RefreshActionCommands();

            StatusText = $"{group.Label}: discarding changes...";

            await Task.Yield();
            await Task.Delay(300); // petit délai pour laisser le throbber visible

            foreach (AdaptationItemView adaptation in group.Adaptations)
                adaptation.EditValue = null;

            group.AdaptationsView.Refresh();

            group.Status = "Changes discarded";
            StatusText = $"{group.Label}: pending changes discarded.";
        }
        finally
        {
            group.IsLoading = false;
            IsLoading = false;
            RefreshActionCommands();
        }
    }

    private void DiscardGroupChanges(AdaptationGroupView group)
    {
        foreach (AdaptationItemView adaptation in group.Adaptations)
            adaptation.EditValue = null;

        group.AdaptationsView.Refresh();
        RefreshActionCommands();
    }

    private async void ApplyExpandedGroup()
    {
        if (_expandedGroup is null || !HasPendingChanges(_expandedGroup))
            return;

        AdaptationWritePlan plan = BuildWritePlan(_expandedGroup);

        if (!plan.HasChanges)
        {
            AppMessageBox.Show(
                "No valid write plan could be built from the pending changes.",
                "Apply adaptations",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        string preview = BuildWritePlanPreview(plan);

        MessageBoxResult result = AppMessageBox.Show(
            $"Apply pending changes in \"{_expandedGroup.Label}\"?\n\n{preview}",
            "Apply adaptations - preview",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        if (IsDesignMode)
            await FakeApplyAsync();
        else
            await FakeApplyAsync(); // temporaire
    }

    private async Task FakeApplyAsync()
    {
        AdaptationGroupView? group = _expandedGroup;
        if (group is null)
            return;

        try
        {
            IsLoading = true;
            group.IsLoading = true;
            RefreshActionCommands();

            StatusText = $"{group.Label}: applying (fake)...";

            await Task.Yield();
            await Task.Delay(600);

            foreach (AdaptationItemView adaptation in group.Adaptations)
            {
                if (!adaptation.HasPendingChange)
                    continue;

                adaptation.CurrentValue = adaptation.EditValue ?? adaptation.CurrentValue;
                adaptation.EditValue = null;
            }

            group.Status = "Applied locally";
            StatusText = $"{group.Label}: fake apply completed.";
        }
        finally
        {
            group.IsLoading = false;
            IsLoading = false;
            RefreshActionCommands();
        }
    }

    private static bool HasPendingChanges(AdaptationGroupView group)
    {
        return group.Adaptations.Any(a => a.HasPendingChange);
    }

    private static AdaptationWritePlan BuildWritePlan(AdaptationGroupView group)
    {
        var plan = new AdaptationWritePlan();

        foreach (IGrouping<string, AdaptationItemView> keyGroup in group.Adaptations
                     .Where(a => a.HasPendingChange)
                     .GroupBy(a => a.CacheKey))
        {
            AdaptationItemView first = keyGroup.First();

            if (!int.TryParse(first.RawValue, CultureInfo.InvariantCulture, out int currentRaw))
                continue;

            int newRaw = currentRaw;

            foreach (AdaptationItemView adaptation in keyGroup)
            {
                if (!string.Equals(adaptation.StorageMode, "packedFlags", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (adaptation.Mask is not int mask)
                    continue;

                int bitValue = string.Equals(adaptation.EditValue, "activated", StringComparison.OrdinalIgnoreCase)
                    ? mask
                    : 0;

                newRaw &= ~mask;
                newRaw |= bitValue;
            }

            if (newRaw == currentRaw)
                continue;

            var keyPlan = new AdaptationWriteKeyPlan
            {
                Partition = first.Partition,
                Key = first.Key,
                Type = first.Type,
                CurrentRawValue = currentRaw.ToString(CultureInfo.InvariantCulture),
                NewRawValue = newRaw.ToString(CultureInfo.InvariantCulture)
            };

            foreach (AdaptationItemView adaptation in keyGroup)
            {
                keyPlan.Fields.Add(new AdaptationWriteFieldPlan
                {
                    Label = adaptation.Label,
                    CurrentValue = adaptation.CurrentValue,
                    NewValue = adaptation.EditValue ?? string.Empty,
                    Mask = adaptation.Mask,
                    Shift = adaptation.Shift
                });
            }

            plan.Keys.Add(keyPlan);
        }

        return plan;
    }

    private static string BuildWritePlanPreview(AdaptationWritePlan plan)
    {
        if (!plan.HasChanges)
            return "No writable changes detected.";

        var lines = new List<string>();

        foreach (AdaptationWriteKeyPlan key in plan.Keys)
        {
            lines.Add($"{key.KeyDisplay}");
            lines.Add($"  raw: {key.CurrentRawValue} -> {key.NewRawValue}");

            foreach (AdaptationWriteFieldPlan field in key.Fields)
                lines.Add($"  - {field.Label}: {field.CurrentValue} -> {field.NewValue}");

            lines.Add(string.Empty);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void RefreshActionCommands()
    {
        ReloadCommand.RaiseCanExecuteChanged();
        ApplyCommand.RaiseCanExecuteChanged();
        DiscardCommand.RaiseCanExecuteChanged();
    }

    private async Task ReadGroupAsync(AdaptationGroupView group)
    {
        if (IsLoading)
            return;

        DateTime loadingStartedAt = DateTime.UtcNow;

        try
        {
            IsLoading = true;
            group.IsLoading = true;
            group.Status = "Reading from MIB...";
            ReloadCommand.RaiseCanExecuteChanged();

            await Task.Yield();
            await Task.Delay(150);

            IReadOnlyList<AdaptationReadValue> readValues =
                await _adaptationsService.ReadAdaptationsAsync(
                    group.ReadDefinitions,
                    cancellationToken: CancellationToken.None);

            ApplyReadValues(group, readValues);

            group.IsLoaded = true;
            group.Status = $"{readValues.Count} keys read";
            StatusText = $"{group.Label}: {readValues.Count} unique keys read. {group.Adaptations.Count} adaptations updated.";
        }
        catch (Exception ex)
        {
            group.Status = "Read failed";
            StatusText = $"{group.Label}: read failed: {ex.Message}";
        }
        finally
        {
            TimeSpan elapsed = DateTime.UtcNow - loadingStartedAt;
            if (elapsed < TimeSpan.FromMilliseconds(800))
                await Task.Delay(TimeSpan.FromMilliseconds(800) - elapsed);

            group.IsLoading = false;
            IsLoading = false;
            RefreshActionCommands();
        }
    }

    private static void ApplyReadValues(
        AdaptationGroupView group,
        IReadOnlyList<AdaptationReadValue> readValues)
    {
        Dictionary<string, string> valuesByKey = readValues
            .GroupBy(v => v.CacheKey)
            .ToDictionary(g => g.Key, g => g.First().Value);

        foreach (AdaptationItemView adaptation in group.Adaptations)
        {
            if (!valuesByKey.TryGetValue(adaptation.CacheKey, out string? rawValue))
                continue;

            adaptation.RawValue = rawValue;
            adaptation.CurrentValue = DecodeCurrentValue(adaptation, rawValue);
            adaptation.EditValue = null;
        }

        group.AdaptationsView.Refresh();
    }

    private bool FilterGroup(object item)
    {
        if (item is not AdaptationGroupView group)
            return false;

        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        string search = SearchText.Trim();

        if (group.Label.Contains(search, StringComparison.OrdinalIgnoreCase))
            return true;

        return group.Adaptations.Any(adaptation => MatchesSearch(adaptation, search));
    }

    private bool FilterAdaptation(object item)
    {
        if (item is not AdaptationItemView adaptation)
            return false;

        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        return MatchesSearch(adaptation, SearchText.Trim());
    }

    private static bool MatchesSearch(AdaptationItemView adaptation, string search)
    {
        return adaptation.Label.Contains(search, StringComparison.OrdinalIgnoreCase)
            || adaptation.Group.Contains(search, StringComparison.OrdinalIgnoreCase)
            || adaptation.CurrentValue.Contains(search, StringComparison.OrdinalIgnoreCase)
            || adaptation.StorageMode.Contains(search, StringComparison.OrdinalIgnoreCase)
            || adaptation.Confidence.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshAllGroupFilters()
    {
        foreach (AdaptationGroupView group in Groups)
            group.AdaptationsView.Refresh();

        GroupsView.Refresh();
    }

    private static AdaptationDefinition CreateReadDefinition(
        AdaptationCatalogGroup group,
        AdaptationCatalogItem item)
    {
        return new AdaptationDefinition
        {
            Id = item.Id,
            Ecu = "5F",
            Group = group.Label,
            Label = item.Label,
            CurrentValueFromDump = item.CurrentValueRaw,
            Persistence = new AdaptationPersistence
            {
                Partition = item.Storage.Partition ?? string.Empty,
                Key = item.Storage.Key ?? string.Empty,
                Type = item.Storage.Type ?? string.Empty
            },
            Ui = new AdaptationUi
            {
                Control = item.Ui.Editor
            },
            Confidence = item.Storage.MappingSource ?? string.Empty,
            Safety = item.Ui.Editable ? "review" : "read_only",
            Writable = item.Ui.Editable && item.Storage.Mapped,
            Notes = item.Storage.Mode
        };
    }

    private static string BuildV4Notes(AdaptationCatalogItem item)
    {
        string mapped = item.Storage.Mapped ? "mapped" : "not mapped";
        string storage = item.Storage.Mapped
            ? $"{item.Storage.Partition}:{item.Storage.Key}:{item.Storage.Type}"
            : "storage pending";

        string byteLength = item.Ui.ByteLength is int length
            ? $"; byteLength={length}"
            : string.Empty;

        string reason = string.IsNullOrWhiteSpace(item.Ui.Reason)
            ? string.Empty
            : $"; reason={item.Ui.Reason}";

        return $"{mapped}; {storage}; editor={item.Ui.Editor}; valueType={item.ValueType}{byteLength}{reason}";
    }

    private static string DecodeCurrentValue(AdaptationItemView adaptation, string rawValue)
    {
        if (!adaptation.IsStorageMapped)
            return DecodeCatalogValue(adaptation, rawValue);

        return DecodeCatalogValue(adaptation, rawValue);
    }
}
