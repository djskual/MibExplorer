using MibExplorer.Core;
using MibExplorer.Models.Adaptations;
using MibExplorer.Services.Adaptations;
using MibExplorer.Views.Dialogs;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;

namespace MibExplorer.ViewModels;

public sealed class AdaptationsCenterViewModel : ObservableObject
{
    private readonly AdaptationCatalogService _catalogService;
    private readonly IAdaptationsCenterService _adaptationsService;
    private readonly AdaptationHistoryService _historyService = new();
    private AdaptationHistoryEntry? _selectedHistoryEntry;
    private AdaptationCatalog? _catalog;
    private AdaptationGroupView? _expandedGroup;
    private readonly Dictionary<string, RuntimeStorageValue> _runtimeCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PhysicalWriteTransaction> _transactionHistory = new();
    private readonly Dictionary<string, PendingAdaptationChange> _pendingChanges = new(StringComparer.OrdinalIgnoreCase);
    private AdaptationItemView? _selectedAdaptation;
    private string _searchText = string.Empty;
    private string _statusText = "Ready.";
    private string _currentVin = "UNKNOWN";
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
            async parameter =>
            {
                if (parameter is AdaptationGroupView group)
                    await ReloadGroupAsync(group);
            },
            parameter => !IsLoading
                && parameter is AdaptationGroupView group
                && group.IsExpanded
                && group.IsLoaded);
        ApplyCommand = new RelayCommand(
            _ => ApplyPendingGroups(),
            _ => !IsLoading && GetPendingGroups().Count > 0);
        DiscardCommand = new RelayCommand(
            _ => DiscardPendingGroupsWithConfirmation(),
            _ => !IsLoading && GetPendingGroups().Count > 0);
        RestoreHistoryCommand = new RelayCommand(
            _ => RestoreSelectedHistoryEntry(),
            _ => !IsLoading && SelectedHistoryEntry is not null);
        ClearSearchCommand = new RelayCommand(
            _ => SearchText = string.Empty,
            _ => !string.IsNullOrWhiteSpace(SearchText));

        GroupsView = CollectionViewSource.GetDefaultView(Groups);
        GroupsView.Filter = FilterGroup;

        LoadCatalog();
        _ = InitializeRuntimeContextAsync();
    }

    public ObservableCollection<AdaptationGroupView> Groups { get; } = new();

    public ObservableCollection<AdaptationHistoryEntry> HistoryEntries { get; } = new();

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

    public string CurrentVin
    {
        get => _currentVin;
        private set => SetProperty(ref _currentVin, value);
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

    public int DirtyPhysicalKeyCount =>
        _runtimeCache.Values.Count(v => v.IsDirty);

    public int PendingGroupCount =>
        _pendingChanges.Values
        .Select(c => c.Adaptation.Group)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public int PendingAdaptationCount => _pendingChanges.Count;

    public int TransactionHistoryCount => _transactionHistory.Count;

    public bool HasHistoryEntries => HistoryEntries.Count > 0;

    public int HistoryEntryCount => HistoryEntries.Count;

    public AdaptationHistoryEntry? SelectedHistoryEntry
    {
        get => _selectedHistoryEntry;
        set
        {
            if (SetProperty(ref _selectedHistoryEntry, value))
            {
                OnPropertyChanged(nameof(HasSelectedHistoryEntry));
                RestoreHistoryCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedHistoryEntry => SelectedHistoryEntry is not null;

    public RelayCommand ReloadCommand { get; }
    public RelayCommand ApplyCommand { get; }
    public RelayCommand DiscardCommand { get; }
    public RelayCommand ClearSearchCommand { get; }
    public RelayCommand RestoreHistoryCommand { get; }

    private IReadOnlyList<RuntimeStorageValue> GetDirtyPhysicalValues()
    {
        return _runtimeCache.Values
            .Where(v => v.IsDirty)
            .OrderBy(v => v.Key.Partition, StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.Key.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.Key.Type, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool TryGetCurrentRuntimeRawValue(
        AdaptationItemView adaptation,
        out string rawValue)
    {
        rawValue = string.Empty;

        foreach (PhysicalStorageKey key in adaptation.PhysicalKeys)
        {
            string cacheKey = AdaptationReadValue.BuildCacheKey(
                key.Partition,
                key.Key,
                key.Type);

            if (_runtimeCache.TryGetValue(cacheKey, out RuntimeStorageValue? cached))
            {
                rawValue = cached.RawValue;
                return true;
            }
        }

        return false;
    }

    private static PhysicalStorageKey GetPrimaryPhysicalKey(AdaptationItemView adaptation)
    {
        return adaptation.PhysicalKeys.FirstOrDefault()
            ?? new PhysicalStorageKey(
                adaptation.Partition,
                adaptation.Key,
                adaptation.Type);
    }

    private PhysicalWriteTransaction CreateTransaction(
        AdaptationWriteKeyPlan keyPlan,
        IEnumerable<PendingAdaptationChange> changes)
    {
        var transaction = new PhysicalWriteTransaction
        {
            PhysicalKey = new PhysicalStorageKey(
        keyPlan.Partition,
        keyPlan.Key,
        keyPlan.Type),
            OriginalRawValue = keyPlan.CurrentRawValue,
            MergedRawValue = keyPlan.NewRawValue
        };

        transaction.Changes.AddRange(changes);

        return transaction;
    }

    private static void ValidateTransactionMergeSafety(PhysicalWriteTransaction transaction)
    {
        if (transaction.Changes.Count <= 1)
            return;

        Dictionary<string, PendingAdaptationChange> touchedFields =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (PendingAdaptationChange change in transaction.Changes)
        {
            AdaptationItemView adaptation = change.Adaptation;

            string fieldKey = BuildTouchedFieldKey(adaptation);

            if (string.IsNullOrWhiteSpace(fieldKey))
            {
                transaction.ValidationErrors.Add(
                    $"{change.Label}: cannot determine touched field for merge validation.");
                continue;
            }

            if (touchedFields.TryGetValue(fieldKey, out PendingAdaptationChange? existing))
            {
                transaction.ValidationErrors.Add(
                    $"{change.Label}: overlaps with {existing.Label} on {fieldKey}.");
                continue;
            }

            touchedFields[fieldKey] = change;
        }
    }

    private static string BuildTouchedFieldKey(AdaptationItemView adaptation)
    {
        if (adaptation.StorageMode.Equals("packedFlags", StringComparison.OrdinalIgnoreCase))
        {
            if (adaptation.Mask is null)
                return string.Empty;

            return $"packedFlags:mask:{adaptation.Mask.Value}";
        }

        if (adaptation.StorageMode.Equals("blobBit", StringComparison.OrdinalIgnoreCase))
        {
            if (adaptation.ByteIndex is null || adaptation.BitIndex is null)
                return string.Empty;

            return $"blobBit:byte:{adaptation.ByteIndex.Value}:bit:{adaptation.BitIndex.Value}";
        }

        if (adaptation.StorageMode.Equals("blobBitsEnum", StringComparison.OrdinalIgnoreCase))
        {
            if (adaptation.ByteIndex is null || adaptation.Mask is null)
                return string.Empty;

            return $"blobBitsEnum:byte:{adaptation.ByteIndex.Value}:mask:{adaptation.Mask.Value}";
        }

        return $"scalar:{adaptation.Id}";
    }

    private static bool ShouldShowGroup(AdaptationCatalogGroup group)
    {
        if (!group.DefaultVisibleInMibExplorer)
            return false;

        return group.Adaptations.Any(a => a.Ui.VisibleByDefault);
    }

    private void LoadCatalog()
    {
        _runtimeCache.Clear();
        OnPropertyChanged(nameof(DirtyPhysicalKeyCount));

        _pendingChanges.Clear();

        RefreshPendingState();

        _transactionHistory.Clear();
        OnPropertyChanged(nameof(TransactionHistoryCount));

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

    private async Task InitializeRuntimeContextAsync()
    {
        try
        {
            IsLoading = true;
            StatusText = "Reading VIN...";

            CurrentVin = await _adaptationsService.ReadVinAsync(
                onOutput: null,
                cancellationToken: CancellationToken.None);

            await LoadHistoryAsync();

            StatusText = $"{Groups.Count} ODIS groups loaded. VIN: {CurrentVin}. History: {HistoryEntryCount} transaction(s).";
        }
        catch (Exception ex)
        {
            CurrentVin = "UNKNOWN";
            await LoadHistoryAsync();
            StatusText = $"VIN read failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            RefreshActionCommands();
        }
    }

    private async Task LoadHistoryAsync()
    {
        HistoryEntries.Clear();

        IReadOnlyList<AdaptationHistoryEntry> entries =
            await _historyService.LoadEntriesAsync(CurrentVin);

        foreach (AdaptationHistoryEntry entry in entries
                     .OrderByDescending(e => e.CreatedAt))
        {
            HistoryEntries.Add(entry);
        }

        SelectedHistoryEntry = HistoryEntries.FirstOrDefault();

        OnPropertyChanged(nameof(HasHistoryEntries));
        OnPropertyChanged(nameof(HistoryEntryCount));
    }

    private static string BuildGroupLabel(AdaptationCatalogGroup group)
    {
        string label = FirstNonEmpty(group.Label, group.OdisOriginalGroupLabel, group.OdisRawGroupLabel, group.HtmlRawGroupLabel);

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
            IReadOnlyList<PhysicalStorageKey> physicalKeys = catalogItem.Storage.PhysicalKeys;

            if (catalogItem.Storage.Mapped && physicalKeys.Count > 0)
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
                PhysicalKeys = physicalKeys,
                StorageMode = catalogItem.Storage.Mode,
                ValueType = catalogItem.ValueType,
                Mask = catalogItem.Storage.Mask,
                Shift = catalogItem.Storage.Shift,
                BitWidth = catalogItem.Storage.BitWidth,
                ByteIndex = catalogItem.Storage.ByteIndex,
                BitIndex = catalogItem.Storage.BitIndex,
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

            item.PendingChangeChanged += (_, _) =>
            {
                SyncPendingChange(item);
                RefreshPendingState();
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

        foreach (AdaptationCatalogEnumValue value in catalogItem.Storage.Values)
        {
            item.StorageOptions.Add(new AdaptationEditOption
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
            if (!ConfirmCloseGroup(group))
                return;

            DiscardGroupChanges(group);
            group.IsExpanded = false;

            if (ReferenceEquals(_expandedGroup, group))
                _expandedGroup = GetLastExpandedGroupOrNull();

            RefreshActionCommands();
            return;
        }

        _expandedGroup = group;
        SelectedAdaptation = null;
        RefreshActionCommands();

        if (!group.IsLoaded)
            await ReadGroupAsync(group, forceRefresh: false);

        group.IsExpanded = true;
        RefreshActionCommands();
    }

    private void OnGroupExpansionChanged(AdaptationGroupView group, bool isExpanded)
    {
        if (!isExpanded)
        {
            if (ReferenceEquals(_expandedGroup, group))
                _expandedGroup = GetLastExpandedGroupOrNull();

            group.Status = group.IsLoaded ? "Loaded" : "Collapsed";
            RefreshActionCommands();
            return;
        }

        _expandedGroup = group;
        SelectedAdaptation = null;
        RefreshActionCommands();
    }

    private async Task ReloadGroupAsync(AdaptationGroupView group)
    {
        if (IsLoading)
            return;

        if (!group.IsExpanded || !group.IsLoaded)
            return;

        if (HasPendingChanges(group))
        {
            MessageBoxResult result = AppMessageBox.Show(
                $"The group \"{group.Label}\" contains unapplied changes.\n\n" +
                "Reloading this group will discard these pending changes and read values again.\n\n" +
                "Discard changes and reload?",
                "Reload group",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            DiscardGroupChanges(group);
        }

        await ReadGroupAsync(group, forceRefresh: true);
    }

    private bool ConfirmCloseGroup(AdaptationGroupView group)
    {
        if (!HasPendingChanges(group))
            return true;

        MessageBoxResult result = AppMessageBox.Show(
            $"The group \"{group.Label}\" contains unapplied changes.\n\n" +
            "Closing this group will discard these changes.\n\n" +
            "Discard changes and close the group?",
            "Unsaved adaptation changes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    private AdaptationGroupView? GetLastExpandedGroupOrNull()
    {
        return Groups
            .LastOrDefault(g => g.IsExpanded);
    }

    private async void DiscardPendingGroupsWithConfirmation()
    {
        IReadOnlyList<AdaptationGroupView> pendingGroups = GetPendingGroups();

        if (pendingGroups.Count == 0)
            return;

        string groupList = string.Join(
            Environment.NewLine,
            pendingGroups.Select(g => $"- {g.Label}"));

        MessageBoxResult result = AppMessageBox.Show(
            $"Discard all pending changes in {pendingGroups.Count} group(s)?\n\n{groupList}",
            "Discard changes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            IsLoading = true;

            foreach (AdaptationGroupView group in pendingGroups)
                group.IsLoading = true;

            RefreshActionCommands();

            StatusText = $"Discarding pending changes in {pendingGroups.Count} group(s)...";

            await Task.Yield();
            await Task.Delay(300);

            foreach (AdaptationGroupView group in pendingGroups)
            {
                DiscardGroupChanges(group);
                group.Status = "Changes discarded";
            }

            StatusText = $"Pending changes discarded in {pendingGroups.Count} group(s).";
        }
        finally
        {
            foreach (AdaptationGroupView group in pendingGroups)
                group.IsLoading = false;

            IsLoading = false;
            RefreshActionCommands();
        }
    }

    private void DiscardGroupChanges(AdaptationGroupView group)
    {
        foreach (AdaptationItemView adaptation in group.Adaptations)
        {
            adaptation.EditValue = null;
            _pendingChanges.Remove(adaptation.Id);
        }

        group.AdaptationsView.Refresh();
        RefreshPendingState();
    }

    private async void ApplyPendingGroups()
    {
        IReadOnlyList<AdaptationGroupView> pendingGroups = GetPendingGroups();

        if (pendingGroups.Count == 0)
            return;

        AdaptationWritePlan plan = BuildWritePlan(pendingGroups);

        if (!plan.HasChanges)
        {
            AppMessageBox.Show(
                BuildWritePlanPreview(plan),
                "Apply adaptations",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        string groupList = string.Join(
            Environment.NewLine,
            pendingGroups.Select(g => $"- {g.Label}"));

        string preview = BuildWritePlanPreview(plan);

        MessageBoxResult result = AppMessageBox.Show(
            $"Apply pending changes in {pendingGroups.Count} group(s)?\n\n{groupList}\n\n{preview}",
            "Apply adaptations - preview",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        //await FakeApplyAsync(plan, pendingGroups);
        await PreflightApplyAsync(plan, pendingGroups);
    }

    private async Task PreflightApplyAsync(
        AdaptationWritePlan plan,
        IReadOnlyList<AdaptationGroupView> groups)
    {
        try
        {
            IsLoading = true;

            foreach (AdaptationGroupView group in groups)
                group.IsLoading = true;

            RefreshActionCommands();

            StatusText = IsDesignMode
                ? $"Running design preflight for {groups.Count} group(s)..."
                : $"Running real MIB preflight for {groups.Count} group(s). No write will be performed.";

            await Task.Yield();

            AdaptationWriteResult result =
                await _adaptationsService.PreflightWriteAdaptationsAsync(
                    plan.Transactions,
                    onOutput: null,
                    cancellationToken: CancellationToken.None);

            ApplyPreflightResultToPlan(plan, result);

            string resultText = BuildPreflightResultPreview(result);

            AppMessageBox.Show(
                resultText,
                result.Success ? "Preflight OK - no write performed" : "Preflight failed - no write performed",
                MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);

            StatusText = result.Success
                ? $"Preflight OK across {plan.Transactions.Count} transaction(s). No write performed."
                : $"Preflight failed: {result.Message}. No write performed.";
        }
        catch (Exception ex)
        {
            StatusText = $"Preflight failed: {ex.Message}";

            AppMessageBox.Show(
                $"Preflight failed before any write could be attempted.\n\n{ex.Message}",
                "Preflight failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            foreach (AdaptationGroupView group in groups)
                group.IsLoading = false;

            IsLoading = false;
            RefreshActionCommands();
        }
    }

    private static void ApplyPreflightResultToPlan(
        AdaptationWritePlan plan,
        AdaptationWriteResult result)
    {
        foreach (PhysicalWriteTransaction transaction in plan.Transactions)
        {
            string transactionKey = AdaptationReadValue.BuildCacheKey(
                transaction.PhysicalKey.Partition,
                transaction.PhysicalKey.Key,
                transaction.PhysicalKey.Type);

            AdaptationPhysicalWriteResult? physicalResult =
                result.PhysicalResults.FirstOrDefault(r =>
                    string.Equals(
                        AdaptationReadValue.BuildCacheKey(
                            r.PhysicalKey.Partition,
                            r.PhysicalKey.Key,
                            r.PhysicalKey.Type),
                        transactionKey,
                        StringComparison.OrdinalIgnoreCase));

            if (physicalResult is null)
            {
                transaction.Status = PhysicalWriteTransactionStatus.PreflightFailed;
                transaction.ErrorMessage = "No preflight result returned for this transaction.";
                transaction.CompletedAtUtc = DateTime.UtcNow;
                continue;
            }

            transaction.ReadbackRawValue = physicalResult.ReadbackRawValue;
            transaction.CompletedAtUtc = DateTime.UtcNow;

            if (physicalResult.Success)
            {
                transaction.Status = PhysicalWriteTransactionStatus.PreflightVerified;
                transaction.ErrorMessage = string.Empty;
            }
            else
            {
                transaction.Status = PhysicalWriteTransactionStatus.PreflightFailed;
                transaction.ErrorMessage = physicalResult.Message;
            }
        }
    }

    private static string BuildPreflightResultPreview(AdaptationWriteResult result)
    {
        var lines = new List<string>
        {
            result.Success
                ? "Preflight completed successfully. No write was performed."
                : "Preflight failed. No write was performed.",
            string.Empty,
            $"Result: {result.Message}",
            string.Empty,
            "Physical checks:"
        };

        foreach (AdaptationPhysicalWriteResult physicalResult in result.PhysicalResults)
        {
            lines.Add(
                $"- {physicalResult.PhysicalKey} [{(physicalResult.Success ? "OK" : "FAILED")}]");

            lines.Add($"  Current/readback: {FormatRawPreview(physicalResult.ReadbackRawValue)}");

            if (!string.IsNullOrWhiteSpace(physicalResult.ExpectedOriginalRawValue))
                lines.Add($"  Expected: {FormatRawPreview(physicalResult.ExpectedOriginalRawValue)}");

            if (!string.IsNullOrWhiteSpace(physicalResult.TargetRawValue))
                lines.Add($"  Target: {FormatRawPreview(physicalResult.TargetRawValue)}");

            if (!string.IsNullOrWhiteSpace(physicalResult.Message))
                lines.Add($"  Message: {physicalResult.Message}");

            lines.Add(string.Empty);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private async Task FakeApplyAsync(
        AdaptationWritePlan plan,
        IReadOnlyList<AdaptationGroupView> groups)
    {
        HashSet<string> appliedAdaptationIds = plan.Keys
            .SelectMany(k => k.Fields)
            .Select(f => f.AdaptationId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Dictionary<string, AdaptationWriteKeyPlan> keyPlanByAdaptationId = plan.Keys
            .SelectMany(k => k.Fields.Select(f => new
            {
                f.AdaptationId,
                KeyPlan = k
            }))
            .Where(x => !string.IsNullOrWhiteSpace(x.AdaptationId))
            .GroupBy(x => x.AdaptationId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().KeyPlan, StringComparer.OrdinalIgnoreCase);

        try
        {
            IsLoading = true;

            foreach (AdaptationGroupView group in groups)
                group.IsLoading = true;

            RefreshActionCommands();

            StatusText = $"Applying {groups.Count} group(s) locally (safe fake apply)...";

            await Task.Yield();
            await Task.Delay(600);

            foreach (AdaptationGroupView group in groups)
            {
                foreach (AdaptationItemView adaptation in group.Adaptations)
                {
                    if (!adaptation.HasPendingChange)
                        continue;

                    if (!appliedAdaptationIds.Contains(adaptation.Id))
                        continue;

                    if (keyPlanByAdaptationId.TryGetValue(adaptation.Id, out AdaptationWriteKeyPlan? keyPlan))
                        adaptation.RawValue = keyPlan.NewRawValue;

                    adaptation.CurrentValue = adaptation.EditValue ?? adaptation.CurrentValue;
                    adaptation.EditValue = null;
                    _pendingChanges.Remove(adaptation.Id);
                }
            }

            UpdateRuntimeCacheFromWritePlan(plan);
            AddTransactionsToHistory(plan);

            await _historyService.AddTransactionsAsync(
                CurrentVin,
                IsDesignMode ? "design" : "real",
                plan.Transactions);

            await LoadHistoryAsync();

            foreach (AdaptationGroupView group in groups)
            {
                RefreshAdaptationsFromRuntimeCache(group);
                group.AdaptationsView.Refresh();
                group.Status = "Applied locally";
            }

            RefreshPendingState();

            StatusText = plan.HasBlockedChanges
                ? $"Fake apply completed for supported changes only across {groups.Count} group(s). Some changes were blocked. {TransactionHistoryCount} transaction(s) in history. Pending: {PendingAdaptationCount} change(s) in {PendingGroupCount} group(s)."
                : $"Fake apply completed across {groups.Count} group(s). {TransactionHistoryCount} transaction(s) in history. Pending: {PendingAdaptationCount} change(s) in {PendingGroupCount} group(s).";
        }
        finally
        {
            foreach (AdaptationGroupView group in groups)
                group.IsLoading = false;

            IsLoading = false;
            RefreshActionCommands();
        }
    }

    private void UpdateRuntimeCacheFromWritePlan(AdaptationWritePlan plan)
    {
        foreach (AdaptationWriteKeyPlan keyPlan in plan.Keys)
        {
            string cacheKey = AdaptationReadValue.BuildCacheKey(
                keyPlan.Partition,
                keyPlan.Key,
                keyPlan.Type);

            string originalRawValue = _runtimeCache.TryGetValue(cacheKey, out RuntimeStorageValue? existing)
                ? existing.OriginalRawValue
                : keyPlan.CurrentRawValue;

            _runtimeCache[cacheKey] = new RuntimeStorageValue
            {
                Key = new PhysicalStorageKey(
                    keyPlan.Partition,
                    keyPlan.Key,
                    keyPlan.Type),
                OriginalRawValue = originalRawValue,
                RawValue = keyPlan.NewRawValue,
                ReadAtUtc = DateTime.UtcNow,
                Source = IsDesignMode ? "design-fake-apply" : "pending-real-write"
            };

            PhysicalWriteTransaction? transaction = plan.Transactions
                .FirstOrDefault(t =>
                    string.Equals(
                        AdaptationReadValue.BuildCacheKey(
                            t.PhysicalKey.Partition,
                            t.PhysicalKey.Key,
                            t.PhysicalKey.Type),
                        cacheKey,
                        StringComparison.OrdinalIgnoreCase));

            if (transaction is not null)
            {
                transaction.Status = PhysicalWriteTransactionStatus.AppliedLocally;
                transaction.ReadbackRawValue = keyPlan.NewRawValue;

                SimulateReadbackVerification(transaction);
            }
        }

        OnPropertyChanged(nameof(DirtyPhysicalKeyCount));
    }

    private void AddTransactionsToHistory(AdaptationWritePlan plan)
    {
        foreach (PhysicalWriteTransaction transaction in plan.Transactions)
        {
            if (!transaction.IsDirty)
                continue;

            _transactionHistory.Add(transaction);
        }

        OnPropertyChanged(nameof(TransactionHistoryCount));
    }

    private async void RestoreSelectedHistoryEntry()
    {
        AdaptationHistoryEntry? entry = SelectedHistoryEntry;

        if (entry is null)
            return;

        if (!TryParsePhysicalKey(entry.PhysicalKey, out PhysicalStorageKey physicalKey))
        {
            AppMessageBox.Show(
                $"Cannot restore this transaction because its physical key is invalid:\n\n{entry.PhysicalKey}",
                "Restore transaction",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        AdaptationWritePlan plan = BuildRestoreWritePlan(entry, physicalKey);

        string preview = BuildWritePlanPreview(plan);

        MessageBoxResult result = AppMessageBox.Show(
            $"Restore selected transaction?\n\n" +
            $"This will create a reverse fake write for:\n\n" +
            $"{entry.PhysicalKey}\n\n" +
            $"{preview}",
            "Restore transaction - preview",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        await FakeRestoreAsync(entry, plan);
    }

    private static bool TryParsePhysicalKey(
        string physicalKeyText,
        out PhysicalStorageKey physicalKey)
    {
        physicalKey = new PhysicalStorageKey(string.Empty, string.Empty, string.Empty);

        if (string.IsNullOrWhiteSpace(physicalKeyText))
            return false;

        string[] parts = physicalKeyText.Split(':');

        if (parts.Length != 3)
            return false;

        if (parts.Any(string.IsNullOrWhiteSpace))
            return false;

        physicalKey = new PhysicalStorageKey(
            parts[0].Trim(),
            parts[1].Trim(),
            parts[2].Trim());

        return true;
    }

    private AdaptationWritePlan BuildRestoreWritePlan(
        AdaptationHistoryEntry entry,
        PhysicalStorageKey physicalKey)
    {
        var plan = new AdaptationWritePlan();

        var keyPlan = new AdaptationWriteKeyPlan
        {
            Partition = physicalKey.Partition,
            Key = physicalKey.Key,
            Type = physicalKey.Type,
            CurrentRawValue = entry.MergedRawValue,
            NewRawValue = entry.OriginalRawValue
        };

        foreach (AdaptationHistoryChange change in entry.Changes)
        {
            keyPlan.Fields.Add(new AdaptationWriteFieldPlan
            {
                AdaptationId = change.AdaptationId,
                Label = change.Label,
                CurrentValue = change.NewValue,
                NewValue = change.CurrentValue
            });
        }

        if (keyPlan.IsDirty)
            plan.Keys.Add(keyPlan);

        var transaction = new PhysicalWriteTransaction
        {
            PhysicalKey = physicalKey,
            OriginalRawValue = entry.MergedRawValue,
            MergedRawValue = entry.OriginalRawValue
        };

        if (transaction.IsDirty)
            plan.Transactions.Add(transaction);

        return plan;
    }

    private async Task FakeRestoreAsync(
        AdaptationHistoryEntry sourceEntry,
        AdaptationWritePlan plan)
    {
        try
        {
            IsLoading = true;
            RefreshActionCommands();

            StatusText = $"Restoring transaction {sourceEntry.PhysicalKey} locally (safe fake restore)...";

            await Task.Yield();
            await Task.Delay(600);

            UpdateRuntimeCacheFromWritePlan(plan);
            AddTransactionsToHistory(plan);

            await AddRestoreHistoryEntryAsync(sourceEntry, plan);

            await LoadHistoryAsync();

            RefreshLoadedGroupsFromRuntimeCache();

            StatusText =
                $"Fake restore completed for {sourceEntry.PhysicalKey}. {TransactionHistoryCount} transaction(s) in history.";
        }
        finally
        {
            IsLoading = false;
            RefreshActionCommands();
        }
    }

    private async Task AddRestoreHistoryEntryAsync(
        AdaptationHistoryEntry sourceEntry,
        AdaptationWritePlan plan)
    {
        PhysicalWriteTransaction? transaction = plan.Transactions.FirstOrDefault();

        if (transaction is null)
            return;

        List<AdaptationHistoryEntry> entries =
            await _historyService.LoadEntriesAsync(CurrentVin);

        entries.Add(new AdaptationHistoryEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.Now,
            Vin = CurrentVin,
            Source = IsDesignMode ? "design-restore" : "real-restore",
            OdisGroup = sourceEntry.OdisGroup,
            PhysicalKey = sourceEntry.PhysicalKey,
            OriginalRawValue = sourceEntry.MergedRawValue,
            MergedRawValue = sourceEntry.OriginalRawValue,
            ReadbackRawValue = sourceEntry.OriginalRawValue,
            Status = transaction.Status.ToString(),
            Changes = sourceEntry.Changes.Select(change => new AdaptationHistoryChange
            {
                AdaptationId = change.AdaptationId,
                Label = change.Label,
                CurrentValue = change.NewValue,
                NewValue = change.CurrentValue
            }).ToList()
        });

        await _historyService.SaveEntriesAsync(CurrentVin, entries);
    }

    private void RefreshLoadedGroupsFromRuntimeCache()
    {
        foreach (AdaptationGroupView group in Groups.Where(g => g.IsLoaded))
        {
            RefreshAdaptationsFromRuntimeCache(group);
            group.AdaptationsView.Refresh();
        }

        RefreshPendingState();
    }

    private static void SimulateReadbackVerification(PhysicalWriteTransaction transaction)
    {
        if (!transaction.RequiresReadbackVerification)
        {
            transaction.Status = PhysicalWriteTransactionStatus.Written;
            transaction.CompletedAtUtc = DateTime.UtcNow;
            return;
        }

        bool verified = string.Equals(
            transaction.ReadbackRawValue,
            transaction.MergedRawValue,
            StringComparison.OrdinalIgnoreCase);

        if (verified)
        {
            transaction.Status = PhysicalWriteTransactionStatus.ReadbackVerified;
            transaction.CompletedAtUtc = DateTime.UtcNow;
            return;
        }

        transaction.Status = PhysicalWriteTransactionStatus.Failed;
        transaction.ErrorMessage =
            $"Readback mismatch: expected '{transaction.MergedRawValue}' but got '{transaction.ReadbackRawValue}'.";
    }

    private void RefreshAdaptationsFromRuntimeCache(AdaptationGroupView group)
    {
        Dictionary<string, List<AdaptationItemView>> adaptationsByCacheKey =
            group.Adaptations
                .Where(a => a.IsStorageMapped)
                .GroupBy(a => a.CacheKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToList(),
                    StringComparer.OrdinalIgnoreCase);

        foreach ((string cacheKey, RuntimeStorageValue runtimeValue) in _runtimeCache)
        {
            if (!adaptationsByCacheKey.TryGetValue(cacheKey, out List<AdaptationItemView>? adaptations))
                continue;

            foreach (AdaptationItemView adaptation in adaptations)
            {
                adaptation.RawValue = runtimeValue.RawValue;
                adaptation.CurrentValue =
                    DecodeCurrentValue(adaptation, runtimeValue.RawValue);

                adaptation.EditValue = null;
            }
        }
    }

    private static bool HasPendingChanges(AdaptationGroupView group)
    {
        return group.Adaptations.Any(a => a.HasPendingChange);
    }

    private IReadOnlyList<AdaptationGroupView> GetPendingGroups()
    {
        if (_pendingChanges.Count == 0)
            return Array.Empty<AdaptationGroupView>();

        HashSet<string> pendingGroupLabels = _pendingChanges.Values
            .Select(c => c.Adaptation.Group)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Groups
            .Where(g => pendingGroupLabels.Contains(g.Label))
            .ToList();
    }

    private void SyncPendingChange(AdaptationItemView adaptation)
    {
        if (!adaptation.HasPendingChange)
        {
            _pendingChanges.Remove(adaptation.Id);
            return;
        }

        _pendingChanges[adaptation.Id] = BuildPendingChange(adaptation);
    }

    private static PendingAdaptationChange BuildPendingChange(AdaptationItemView adaptation)
    {
        return new PendingAdaptationChange
        {
            Adaptation = adaptation,
            PrimaryKey = GetPrimaryPhysicalKey(adaptation),
            CacheKey = adaptation.CacheKey,
            CurrentDisplayValue = adaptation.CurrentValue,
            RequestedDisplayValue = adaptation.EditValue ?? string.Empty,
            RequestedRawValue = adaptation.EditRawValue,
            StorageMode = adaptation.StorageMode,
            IsStorageMapped = adaptation.IsStorageMapped,
            IsMultiStorage = adaptation.IsMultiStorage,
            HasStorageWarning = adaptation.HasStorageWarning,
            StorageWarning = adaptation.StorageWarning
        };
    }

    private IReadOnlyList<PendingAdaptationChange> BuildPendingChanges(AdaptationGroupView group)
    {
        HashSet<string> adaptationIds = group.Adaptations
            .Select(a => a.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _pendingChanges.Values
            .Where(c => adaptationIds.Contains(c.AdaptationId))
            .ToList();
    }

    private AdaptationWritePlan BuildWritePlan(IReadOnlyList<AdaptationGroupView> groups)
    {
        var plan = new AdaptationWritePlan();

        HashSet<string> groupIds = groups
            .SelectMany(g => g.Adaptations.Select(a => a.Id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<PendingAdaptationChange> pendingChanges = _pendingChanges.Values
            .Where(c => groupIds.Contains(c.AdaptationId))
            .ToList();

        foreach (PendingAdaptationChange change in pendingChanges.Where(c => !c.IsStorageMapped))
        {
            plan.BlockedReasons.Add(
                $"{change.Label}: storage is not mapped; cannot build write plan.");
        }

        foreach (PendingAdaptationChange change in pendingChanges.Where(c => c.IsMultiStorage))
        {
            plan.BlockedReasons.Add(
                $"{change.Label}: multiStorage write is not implemented yet.");
        }

        foreach (PendingAdaptationChange change in pendingChanges.Where(c => c.HasStorageWarning))
        {
            plan.BlockedReasons.Add(
                $"{change.Label}: storage warning blocks write plan ({change.StorageWarning}).");
        }

        List<AdaptationItemView> writableAdaptations = pendingChanges
            .Where(c => c.IsStorageMapped)
            .Where(c => !c.IsMultiStorage)
            .Where(c => !c.HasStorageWarning)
            .Select(c => c.Adaptation)
            .ToList();

        foreach (IGrouping<string, AdaptationItemView> keyGroup in writableAdaptations
                     .GroupBy(a => a.CacheKey))
        {
            AdaptationItemView first = keyGroup.First();

            if (first.StorageMode.Equals("scalar", StringComparison.OrdinalIgnoreCase)
                || first.StorageMode.Equals("scalarEnum", StringComparison.OrdinalIgnoreCase))
            {
                BuildScalarWritePlan(plan, keyGroup);
                continue;
            }

            if (first.StorageMode.Equals("packedFlags", StringComparison.OrdinalIgnoreCase))
            {
                BuildPackedFlagsWritePlan(plan, keyGroup);
                continue;
            }

            if (first.StorageMode.Equals("blobBit", StringComparison.OrdinalIgnoreCase))
            {
                BuildBlobBitWritePlan(plan, keyGroup);
                continue;
            }

            if (first.StorageMode.Equals("blobBitsEnum", StringComparison.OrdinalIgnoreCase))
            {
                BuildBlobBitsEnumWritePlan(plan, keyGroup);
                continue;
            }

            plan.BlockedReasons.Add(
                $"{first.Label}: storage mode '{first.StorageMode}' is not supported by the write plan yet.");
        }

        foreach (AdaptationWriteKeyPlan keyPlan in plan.Keys)
        {
            List<PendingAdaptationChange> transactionChanges = pendingChanges
                .Where(c =>
                    string.Equals(c.CacheKey,
                        AdaptationReadValue.BuildCacheKey(
                            keyPlan.Partition,
                            keyPlan.Key,
                            keyPlan.Type),
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            PhysicalWriteTransaction transaction =
                CreateTransaction(keyPlan, transactionChanges);

            ValidateTransactionMergeSafety(transaction);

            if (!transaction.IsValid)
            {
                foreach (string error in transaction.ValidationErrors)
                    plan.BlockedReasons.Add(error);

                continue;
            }

            if (transaction.IsDirty)
                plan.Transactions.Add(transaction);
        }

        return plan;
    }

    private void BuildScalarWritePlan(
        AdaptationWritePlan plan,
        IGrouping<string, AdaptationItemView> keyGroup)
    {
        AdaptationItemView first = keyGroup.First();
        PhysicalStorageKey physicalKey = GetPrimaryPhysicalKey(first);

        if (keyGroup.Count() > 1)
        {
            plan.BlockedReasons.Add(
                $"{first.Label}: multiple scalar adaptations share the same physical key; scalar merge is not supported.");
            return;
        }

        if (!TryGetCurrentRuntimeRawValue(first, out string currentRawValue))
        {
            plan.BlockedReasons.Add(
                $"{first.Label}: runtime value for {physicalKey} is not loaded; read the group before applying.");
            return;
        }

        foreach (AdaptationItemView adaptation in keyGroup)
        {
            string newRawValue = GetStorageWriteRawValue(adaptation);

            if (string.IsNullOrWhiteSpace(newRawValue))
                continue;

            var keyPlan = new AdaptationWriteKeyPlan
            {
                Partition = physicalKey.Partition,
                Key = physicalKey.Key,
                Type = physicalKey.Type,
                CurrentRawValue = currentRawValue,
                NewRawValue = newRawValue
            };

            keyPlan.Fields.Add(new AdaptationWriteFieldPlan
            {
                AdaptationId = adaptation.Id,
                Label = adaptation.Label,
                CurrentValue = adaptation.CurrentValue,
                NewValue = adaptation.EditValue ?? string.Empty,
                Mask = adaptation.Mask,
                Shift = adaptation.Shift
            });

            if (keyPlan.IsDirty)
                plan.Keys.Add(keyPlan);
        }
    }

    private static string GetStorageWriteRawValue(AdaptationItemView adaptation)
    {
        if (string.IsNullOrWhiteSpace(adaptation.EditValue))
            return string.Empty;

        if (adaptation.StorageMode.Equals("scalarEnum", StringComparison.OrdinalIgnoreCase)
            || adaptation.StorageMode.Equals("blobBitsEnum", StringComparison.OrdinalIgnoreCase))
        {
            AdaptationEditOption? storageOption = adaptation.StorageOptions.FirstOrDefault(o =>
                string.Equals(o.Label, adaptation.EditValue, StringComparison.OrdinalIgnoreCase)
                || string.Equals(o.Value, adaptation.EditValue, StringComparison.OrdinalIgnoreCase));

            if (storageOption is null)
                return string.Empty;

            if (string.IsNullOrWhiteSpace(storageOption.Value))
                return string.Empty;

            return storageOption.Value;
        }

        return adaptation.EditRawValue;
    }

    private void BuildPackedFlagsWritePlan(
        AdaptationWritePlan plan,
        IGrouping<string, AdaptationItemView> keyGroup)
    {
        AdaptationItemView first = keyGroup.First();
        PhysicalStorageKey physicalKey = GetPrimaryPhysicalKey(first);

        if (!TryGetCurrentRuntimeRawValue(first, out string currentRawText))
        {
            plan.BlockedReasons.Add(
                $"{first.Label}: runtime value for {physicalKey} is not loaded; read the group before applying.");
            return;
        }

        if (!TryParseIntegerValue(currentRawText, out int currentRaw))
        {
            plan.BlockedReasons.Add(
                $"{first.Label}: runtime value '{currentRawText}' cannot be parsed as integer.");
            return;
        }

        int newRaw = currentRaw;

        foreach (AdaptationItemView adaptation in keyGroup)
        {
            if (!string.Equals(adaptation.StorageMode, "packedFlags", StringComparison.OrdinalIgnoreCase))
                continue;

            if (adaptation.Mask is not int mask)
                continue;

            bool enable = IsEnabledValue(adaptation.EditValue);

            newRaw = enable
                ? newRaw | mask
                : newRaw & ~mask;
        }

        if (newRaw == currentRaw)
            return;

        var keyPlan = new AdaptationWriteKeyPlan
        {
            Partition = physicalKey.Partition,
            Key = physicalKey.Key,
            Type = physicalKey.Type,
            CurrentRawValue = currentRaw.ToString(CultureInfo.InvariantCulture),
            NewRawValue = newRaw.ToString(CultureInfo.InvariantCulture)
        };

        foreach (AdaptationItemView adaptation in keyGroup)
        {
            keyPlan.Fields.Add(new AdaptationWriteFieldPlan
            {
                AdaptationId = adaptation.Id,
                Label = adaptation.Label,
                CurrentValue = adaptation.CurrentValue,
                NewValue = adaptation.EditValue ?? string.Empty,
                Mask = adaptation.Mask,
                Shift = adaptation.Shift
            });
        }

        plan.Keys.Add(keyPlan);
    }

    private void BuildBlobBitWritePlan(
        AdaptationWritePlan plan,
        IGrouping<string, AdaptationItemView> keyGroup)
    {
        AdaptationItemView first = keyGroup.First();
        PhysicalStorageKey physicalKey = GetPrimaryPhysicalKey(first);

        if (!TryGetCurrentRuntimeRawValue(first, out string currentRawText))
        {
            plan.BlockedReasons.Add(
                $"{first.Label}: runtime value for {physicalKey} is not loaded; read the group before applying.");
            return;
        }

        byte[]? currentBytes = TryParseHexBytes(currentRawText);

        if (currentBytes is null)
        {
            plan.BlockedReasons.Add(
                $"{first.Label}: runtime value '{currentRawText}' cannot be parsed as hex bytes.");
            return;
        }

        byte[] newBytes = currentBytes.ToArray();

        foreach (AdaptationItemView adaptation in keyGroup)
        {
            if (!string.Equals(adaptation.StorageMode, "blobBit", StringComparison.OrdinalIgnoreCase))
                continue;

            if (adaptation.ByteIndex is not int byteIndex || adaptation.BitIndex is not int bitIndex)
                continue;

            if (byteIndex < 0 || byteIndex >= newBytes.Length || bitIndex < 0 || bitIndex > 7)
                continue;

            byte mask = (byte)(1 << bitIndex);
            bool enable = IsEnabledValue(adaptation.EditValue);

            newBytes[byteIndex] = enable
                ? (byte)(newBytes[byteIndex] | mask)
                : (byte)(newBytes[byteIndex] & ~mask);
        }

        string currentRaw = FormatHexBytes(currentBytes);
        string newRaw = FormatHexBytes(newBytes);

        if (string.Equals(currentRaw, newRaw, StringComparison.OrdinalIgnoreCase))
            return;

        var keyPlan = new AdaptationWriteKeyPlan
        {
            Partition = physicalKey.Partition,
            Key = physicalKey.Key,
            Type = physicalKey.Type,
            CurrentRawValue = currentRaw,
            NewRawValue = newRaw
        };

        foreach (AdaptationItemView adaptation in keyGroup)
        {
            keyPlan.Fields.Add(new AdaptationWriteFieldPlan
            {
                AdaptationId = adaptation.Id,
                Label = adaptation.Label,
                CurrentValue = adaptation.CurrentValue,
                NewValue = adaptation.EditValue ?? string.Empty,
                Mask = adaptation.Mask,
                Shift = adaptation.Shift
            });
        }

        plan.Keys.Add(keyPlan);
    }

    private void BuildBlobBitsEnumWritePlan(
        AdaptationWritePlan plan,
        IGrouping<string, AdaptationItemView> keyGroup)
    {
        AdaptationItemView first = keyGroup.First();
        PhysicalStorageKey physicalKey = GetPrimaryPhysicalKey(first);

        if (!TryGetCurrentRuntimeRawValue(first, out string currentRawText))
        {
            plan.BlockedReasons.Add(
                $"{first.Label}: runtime value for {physicalKey} is not loaded; read the group before applying.");
            return;
        }

        byte[]? currentBytes = TryParseHexBytes(currentRawText);

        if (currentBytes is null)
        {
            plan.BlockedReasons.Add(
                $"{first.Label}: runtime value '{currentRawText}' cannot be parsed as hex bytes.");
            return;
        }

        byte[] newBytes = currentBytes.ToArray();

        foreach (AdaptationItemView adaptation in keyGroup)
        {
            if (adaptation.ByteIndex is not int byteIndex ||
                adaptation.BitIndex is not int bitIndex ||
                adaptation.BitWidth is not int bitWidth)
                continue;

            string rawValue = GetStorageWriteRawValue(adaptation);

            if (!TryParseIntegerValue(rawValue, out int newValue))
                continue;

            if (!TrySetBitRange(newBytes, byteIndex, bitIndex, bitWidth, newValue))
                continue;
        }

        string currentRaw = FormatHexBytes(currentBytes);
        string newRaw = FormatHexBytes(newBytes);

        if (string.Equals(currentRaw, newRaw, StringComparison.OrdinalIgnoreCase))
            return;

        var keyPlan = new AdaptationWriteKeyPlan
        {
            Partition = physicalKey.Partition,
            Key = physicalKey.Key,
            Type = physicalKey.Type,
            CurrentRawValue = currentRaw,
            NewRawValue = newRaw
        };

        foreach (AdaptationItemView adaptation in keyGroup)
        {
            keyPlan.Fields.Add(new AdaptationWriteFieldPlan
            {
                AdaptationId = adaptation.Id,
                Label = adaptation.Label,
                CurrentValue = adaptation.CurrentValue,
                NewValue = adaptation.EditValue ?? string.Empty,
                Mask = adaptation.Mask,
                Shift = adaptation.Shift
            });
        }

        plan.Keys.Add(keyPlan);
    }

    private static bool TryExtractBitRange(
        byte[] bytes,
        int byteIndex,
        int bitIndex,
        int bitWidth,
        out int value)
    {
        value = 0;

        if (byteIndex < 0 || bitIndex < 0 || bitIndex > 7 || bitWidth <= 0 || bitWidth > 31)
            return false;

        for (int i = 0; i < bitWidth; i++)
        {
            int absoluteBit = bitIndex + i;
            int currentByteIndex = byteIndex + absoluteBit / 8;
            int currentBitIndex = absoluteBit % 8;

            if (currentByteIndex < 0 || currentByteIndex >= bytes.Length)
                return false;

            if ((bytes[currentByteIndex] & (1 << currentBitIndex)) != 0)
                value |= 1 << i;
        }

        return true;
    }

    private static bool TrySetBitRange(
        byte[] bytes,
        int byteIndex,
        int bitIndex,
        int bitWidth,
        int value)
    {
        if (byteIndex < 0 || bitIndex < 0 || bitIndex > 7 || bitWidth <= 0 || bitWidth > 31)
            return false;

        int maxValue = (1 << bitWidth) - 1;

        if (value < 0 || value > maxValue)
            return false;

        for (int i = 0; i < bitWidth; i++)
        {
            int absoluteBit = bitIndex + i;
            int currentByteIndex = byteIndex + absoluteBit / 8;
            int currentBitIndex = absoluteBit % 8;

            if (currentByteIndex < 0 || currentByteIndex >= bytes.Length)
                return false;

            byte mask = (byte)(1 << currentBitIndex);
            bool bitSet = (value & (1 << i)) != 0;

            bytes[currentByteIndex] = bitSet
                ? (byte)(bytes[currentByteIndex] | mask)
                : (byte)(bytes[currentByteIndex] & ~mask);
        }

        return true;
    }

    private static byte[]? TryParseHexBytes(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var bytes = new List<byte>();

        string[] lines = value
            .Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();

            line = Regex.Replace(line, @"^[0-9A-Fa-f]{8}\s*:\s*", string.Empty);

            int pipeIndex = line.IndexOf('|');
            if (pipeIndex >= 0)
                line = line[..pipeIndex];

            string[] tokens = line.Split(
                new[] { ' ', '\t', ',', ';', '-' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (string token in tokens)
            {
                string t = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? token[2..]
                    : token;

                if (t.Length != 2)
                    continue;

                if (!t.All(Uri.IsHexDigit))
                    continue;

                if (!byte.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                    return null;

                bytes.Add(b);
            }
        }

        if (bytes.Count > 0)
            return bytes.ToArray();

        string compact = Regex.Replace(value, @"[^0-9A-Fa-f]", string.Empty);

        if (compact.Length == 0 || compact.Length % 2 != 0)
            return null;

        for (int i = 0; i < compact.Length; i += 2)
        {
            string hex = compact.Substring(i, 2);

            if (!byte.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                return null;

            bytes.Add(b);
        }

        return bytes.Count == 0 ? null : bytes.ToArray();
    }

    private static bool TryParseIntegerValue(string value, out int result)
    {
        result = 0;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        string cleaned = value.Trim();

        if (int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            return true;

        Match hexPrefix = Regex.Match(cleaned, @"0x[0-9A-Fa-f]+");

        if (hexPrefix.Success)
        {
            return int.TryParse(
                hexPrefix.Value.Substring(2),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out result);
        }

        Match decimalNumber = Regex.Match(cleaned, @"(?<![A-Fa-f0-9])-?\d+(?![A-Fa-f0-9])");

        if (decimalNumber.Success)
        {
            return int.TryParse(
                decimalNumber.Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out result);
        }

        Match hexNumber = Regex.Match(cleaned, @"\b[0-9A-Fa-f]{2,8}\b");

        if (hexNumber.Success)
        {
            return int.TryParse(
                hexNumber.Value,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out result);
        }

        return false;
    }

    private static string FormatHexBytes(byte[] bytes)
    {
        return string.Join(" ", bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
    }

    private static bool IsEnabledValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Equals("activated", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase)
            || value.Equals("available", StringComparison.OrdinalIgnoreCase)
            || value.Equals("available.", StringComparison.OrdinalIgnoreCase)
            || value.Equals("enabled", StringComparison.OrdinalIgnoreCase)
            || value.Equals("[VN]_activated", StringComparison.OrdinalIgnoreCase)
            || value.Equals("[VN]_on", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildWritePlanPreview(AdaptationWritePlan plan)
    {
        var lines = new List<string>();

        if (plan.HasChanges)
        {
            foreach (AdaptationWriteKeyPlan key in plan.Keys)
            {
                lines.Add($"{key.KeyDisplay}");
                lines.Add($"  raw: {FormatRawPreview(key.CurrentRawValue)} -> {FormatRawPreview(key.NewRawValue)}");

                foreach (AdaptationWriteFieldPlan field in key.Fields)
                    lines.Add($"  - {field.Label}: {field.CurrentValue} -> {field.NewValue}");

                lines.Add(string.Empty);
            }
        }
        else
        {
            lines.Add("No writable changes detected.");
            lines.Add(string.Empty);
        }

        if (plan.HasBlockedChanges)
        {
            lines.Add("Blocked changes:");
            foreach (string reason in plan.BlockedReasons)
                lines.Add($"  - {reason}");
        }

        if (plan.Transactions.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Physical transactions:");

            foreach (PhysicalWriteTransaction transaction in plan.Transactions)
            {
                lines.Add($"  - [{transaction.Status}] {transaction.PhysicalKey} : {FormatRawPreview(transaction.OriginalRawValue)} -> {FormatRawPreview(transaction.MergedRawValue)}");

                IReadOnlyList<string> changedBytes = BuildChangedBytesPreview(
                    transaction.OriginalRawValue,
                    transaction.MergedRawValue);

                if (changedBytes.Count > 0)
                {
                    lines.Add("      Changed bytes:");
                    lines.AddRange(changedBytes);
                }

                if (!string.IsNullOrWhiteSpace(transaction.ReadbackRawValue))
                {
                    lines.Add(
                        $"      Readback: {FormatRawPreview(transaction.ReadbackRawValue)}");
                }

                if (!string.IsNullOrWhiteSpace(transaction.ErrorMessage))
                {
                    lines.Add(
                        $"      ERROR: {transaction.ErrorMessage}");
                }

                if (transaction.CompletedAtUtc is not null)
                {
                    lines.Add(
                        $"      Completed: {transaction.CompletedAtUtc:HH:mm:ss}");
                }

                if (!transaction.IsValid)
                {
                    foreach (string error in transaction.ValidationErrors)
                        lines.Add($"      ERROR: {error}");
                }

                foreach (PendingAdaptationChange change in transaction.Changes)
                {
                    lines.Add(
                        $"      {change.Label}: {change.CurrentDisplayValue} -> {change.RequestedDisplayValue}");
                }
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatRawPreview(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "<empty>";

        const int maxLength = 96;

        if (value.Length <= maxLength)
            return value;

        return $"{value[..maxLength]} ... ({value.Length} chars)";
    }

    private static IReadOnlyList<string> BuildChangedBytesPreview(
        string originalRawValue,
        string mergedRawValue)
    {
        byte[]? originalBytes = TryParseHexBytes(originalRawValue);
        byte[]? mergedBytes = TryParseHexBytes(mergedRawValue);

        if (originalBytes is null || mergedBytes is null)
            return Array.Empty<string>();

        int count = Math.Min(originalBytes.Length, mergedBytes.Length);
        var lines = new List<string>();

        for (int i = 0; i < count; i++)
        {
            if (originalBytes[i] == mergedBytes[i])
                continue;

            lines.Add(
                $"      byte 0x{i:X2}: 0x{originalBytes[i]:X2} -> 0x{mergedBytes[i]:X2}");
        }

        if (originalBytes.Length != mergedBytes.Length)
        {
            lines.Add(
                $"      length: {originalBytes.Length} -> {mergedBytes.Length} bytes");
        }

        return lines;
    }

    private void RefreshActionCommands()
    {
        ReloadCommand.RaiseCanExecuteChanged();
        ApplyCommand.RaiseCanExecuteChanged();
        DiscardCommand.RaiseCanExecuteChanged();
        RestoreHistoryCommand.RaiseCanExecuteChanged();
    }

    private void RefreshPendingState()
    {
        foreach (AdaptationGroupView group in Groups)
        {
            group.HasPendingChanges = _pendingChanges.Values.Any(c =>
                string.Equals(
                    c.Adaptation.Group,
                    group.Label,
                    StringComparison.OrdinalIgnoreCase));
        }

        OnPropertyChanged(nameof(PendingGroupCount));
        OnPropertyChanged(nameof(PendingAdaptationCount));
        RefreshActionCommands();
    }

    private async Task ReadGroupAsync(
        AdaptationGroupView group,
        bool forceRefresh)
    {
        if (IsLoading)
            return;

        DateTime loadingStartedAt = DateTime.UtcNow;

        try
        {
            IsLoading = true;
            group.IsLoading = true;
            group.Status = forceRefresh ? "Refreshing from MIB..." : "Reading values...";
            ReloadCommand.RaiseCanExecuteChanged();

            await Task.Yield();
            await Task.Delay(150);

            IReadOnlyList<AdaptationReadValue> cachedValues = BuildCachedReadValues(group);
            IReadOnlyList<AdaptationDefinition> definitionsToRead = forceRefresh
                ? group.ReadDefinitions
                : BuildMissingReadDefinitions(group);

            IReadOnlyList<AdaptationReadValue> freshValues = Array.Empty<AdaptationReadValue>();

            if (definitionsToRead.Count > 0)
            {
                freshValues = await _adaptationsService.ReadAdaptationsAsync(
                    definitionsToRead,
                    cancellationToken: CancellationToken.None);
            }

            IReadOnlyList<AdaptationReadValue> mergedValues = MergeReadValues(
                cachedValues,
                freshValues);

            ApplyReadValues(group, mergedValues);

            group.IsLoaded = true;

            if (definitionsToRead.Count == 0 && cachedValues.Count > 0)
            {
                group.Status = $"{cachedValues.Count} keys from cache";
                StatusText = $"{group.Label}: reused {cachedValues.Count} cached keys. {group.Adaptations.Count} adaptations updated.";
            }
            else
            {
                group.Status = $"{freshValues.Count} keys read";
                StatusText = $"{group.Label}: {freshValues.Count} keys read from {(IsDesignMode ? "design backend" : "MIB")}, {cachedValues.Count} reused from cache.";
            }
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

    private IReadOnlyList<AdaptationDefinition> BuildMissingReadDefinitions(AdaptationGroupView group)
    {
        return group.ReadDefinitions
            .Where(definition => GetDefinitionCacheKeys(definition)
                .Any(cacheKey => !_runtimeCache.ContainsKey(cacheKey)))
            .ToList();
    }

    private IReadOnlyList<AdaptationReadValue> BuildCachedReadValues(AdaptationGroupView group)
    {
        return group.ReadDefinitions
            .SelectMany(GetDefinitionCacheKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(cacheKey => _runtimeCache.ContainsKey(cacheKey))
            .Select(cacheKey =>
            {
                RuntimeStorageValue cached = _runtimeCache[cacheKey];

                return new AdaptationReadValue
                {
                    Partition = cached.Key.Partition,
                    Key = cached.Key.Key,
                    Type = cached.Key.Type,
                    Value = cached.RawValue
                };
            })
            .ToList();
    }

    private static IReadOnlyList<AdaptationReadValue> MergeReadValues(
        IReadOnlyList<AdaptationReadValue> cachedValues,
        IReadOnlyList<AdaptationReadValue> freshValues)
    {
        return cachedValues
            .Concat(freshValues)
            .GroupBy(v => v.CacheKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .ToList();
    }

    private static IReadOnlyList<string> GetDefinitionCacheKeys(AdaptationDefinition definition)
    {
        if (definition.PhysicalKeys.Count > 0)
        {
            return definition.PhysicalKeys
                .Select(k => AdaptationReadValue.BuildCacheKey(
                    k.Partition,
                    k.Key,
                    k.Type))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(definition.Persistence.Partition) &&
            !string.IsNullOrWhiteSpace(definition.Persistence.Key) &&
            !string.IsNullOrWhiteSpace(definition.Persistence.Type))
        {
            return new[]
            {
            AdaptationReadValue.BuildCacheKey(
                definition.Persistence.Partition,
                definition.Persistence.Key,
                definition.Persistence.Type)
        };
        }

        return Array.Empty<string>();
    }

    private void ApplyReadValues(
        AdaptationGroupView group,
        IReadOnlyList<AdaptationReadValue> readValues)
    {
        Dictionary<string, AdaptationReadValue> valuesByKey = readValues
            .GroupBy(v => v.CacheKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (AdaptationReadValue value in readValues)
        {
            _runtimeCache[value.CacheKey] = new RuntimeStorageValue
            {
                Key = new PhysicalStorageKey(value.Partition, value.Key, value.Type),
                OriginalRawValue = value.Value,
                RawValue = value.Value,
                ReadAtUtc = DateTime.UtcNow,
                Source = IsDesignMode ? "design" : "mib"
            };
        }

        foreach (AdaptationItemView adaptation in group.Adaptations)
        {
            List<AdaptationReadValue> matchingValues = adaptation.CacheKeys
                .Where(valuesByKey.ContainsKey)
                .Select(cacheKey => valuesByKey[cacheKey])
                .ToList();

            if (matchingValues.Count == 0)
                continue;

            string rawValue = matchingValues[0].Value;
            adaptation.StorageWarning = string.Empty;

            if (adaptation.IsMultiStorage)
            {
                List<string> distinctRawValues = matchingValues
                    .Select(v => v.Value)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (distinctRawValues.Count > 1)
                {
                    adaptation.RawValue = string.Join(" | ", matchingValues.Select(v =>
                        $"{v.Partition}:{v.Key}:{v.Type}={v.Value}"));

                    adaptation.CurrentValue = "⚠ multi-key mismatch";
                    adaptation.StorageWarning = "Multi-storage entries returned different values.";
                    adaptation.EditValue = null;
                    continue;
                }

                rawValue = distinctRawValues.FirstOrDefault() ?? rawValue;
            }

            adaptation.RawValue = rawValue;
            adaptation.CurrentValue = DecodeCurrentValue(adaptation, rawValue);
            adaptation.EditValue = null;
        }

        OnPropertyChanged(nameof(DirtyPhysicalKeyCount));
        RefreshPendingState();
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
        IReadOnlyList<PhysicalStorageKey> physicalKeys = item.Storage.PhysicalKeys;
        PhysicalStorageKey? firstKey = physicalKeys.FirstOrDefault();

        return new AdaptationDefinition
        {
            Id = item.Id,
            Ecu = "5F",
            Group = group.Label,
            Label = item.Label,
            CurrentValueFromDump = FirstNonEmpty(item.CurrentValue, item.CurrentValueRaw),
            StorageMode = item.Storage.Mode,
            Mask = item.Storage.Mask,
            Shift = item.Storage.Shift,
            BitWidth = item.Storage.BitWidth,
            ByteIndex = item.Storage.ByteIndex,
            BitIndex = item.Storage.BitIndex,
            StorageValues = item.Storage.Values,
            PhysicalKeys = physicalKeys.ToList(),
            Persistence = new AdaptationPersistence
            {
                Partition = firstKey?.Partition ?? item.Storage.Partition ?? string.Empty,
                Key = firstKey?.Key ?? item.Storage.Key ?? string.Empty,
                Type = firstKey?.Type ?? item.Storage.Type ?? string.Empty
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
        string storage;

        if (!item.Storage.Mapped)
        {
            storage = "storage pending";
        }
        else if (item.Storage.IsMultiStorage)
        {
            storage = $"multiStorage entries={item.Storage.PhysicalKeys.Count}";
        }
        else
        {
            storage = $"{item.Storage.Partition}:{item.Storage.Key}:{item.Storage.Type}";
        }

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

        if (adaptation.StorageMode.Equals("scalar", StringComparison.OrdinalIgnoreCase))
            return DecodeCatalogValue(adaptation, rawValue);

        if (adaptation.StorageMode.Equals("scalarEnum", StringComparison.OrdinalIgnoreCase))
            return DecodeStorageValue(adaptation, rawValue);

        if (adaptation.StorageMode.Equals("packedFlags", StringComparison.OrdinalIgnoreCase))
            return DecodePackedFlagValue(adaptation, rawValue);

        if (adaptation.StorageMode.Equals("blobBit", StringComparison.OrdinalIgnoreCase))
            return DecodeBlobBitValue(adaptation, rawValue);

        if (adaptation.StorageMode.Equals("blobBitsEnum", StringComparison.OrdinalIgnoreCase))
            return DecodeBlobBitsEnumValue(adaptation, rawValue);

        return DecodeCatalogValue(adaptation, rawValue);
    }

    private static string DecodeStorageValue(AdaptationItemView item, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "-";

        AdaptationEditOption? storageOption = item.StorageOptions.FirstOrDefault(o =>
            string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase)
            || string.Equals(o.Label, value, StringComparison.OrdinalIgnoreCase));

        if (storageOption is not null)
            return storageOption.Label;

        return DecodeCatalogValue(item, value);
    }

    private static string DecodePackedFlagValue(AdaptationItemView adaptation, string rawValue)
    {
        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int packedValue))
            return DecodeCatalogValue(adaptation, rawValue);

        if (adaptation.Mask is not int mask)
            return DecodeCatalogValue(adaptation, rawValue);

        bool active = (packedValue & mask) != 0;

        return GetBooleanDisplayValue(adaptation, active);
    }

    private static string DecodeBlobBitValue(AdaptationItemView adaptation, string rawValue)
    {
        if (adaptation.ByteIndex is not int byteIndex || adaptation.BitIndex is not int bitIndex)
            return DecodeCatalogValue(adaptation, rawValue);

        byte[]? bytes = TryParseHexBytes(rawValue);

        if (bytes is null || byteIndex < 0 || byteIndex >= bytes.Length || bitIndex < 0 || bitIndex > 7)
            return DecodeCatalogValue(adaptation, rawValue);

        bool active = (bytes[byteIndex] & (1 << bitIndex)) != 0;

        return GetBooleanDisplayValue(adaptation, active);
    }

    private static string DecodeBlobBitsEnumValue(AdaptationItemView adaptation, string rawValue)
    {
        if (adaptation.ByteIndex is not int byteIndex ||
            adaptation.BitIndex is not int bitIndex ||
            adaptation.BitWidth is not int bitWidth)
            return DecodeCatalogValue(adaptation, rawValue);

        byte[]? bytes = TryParseHexBytes(rawValue);

        if (bytes is null)
            return DecodeCatalogValue(adaptation, rawValue);

        if (!TryExtractBitRange(bytes, byteIndex, bitIndex, bitWidth, out int raw))
            return DecodeCatalogValue(adaptation, rawValue);

        return DecodeStorageValue(adaptation, raw.ToString(CultureInfo.InvariantCulture));
    }

    private static string GetBooleanDisplayValue(AdaptationItemView adaptation, bool active)
    {
        foreach (AdaptationEditOption option in adaptation.EditOptions)
        {
            if (active && IsEnabledValue(option.Label))
                return option.Label;

            if (!active && IsDisabledValue(option.Label))
                return option.Label;
        }

        return active ? "activated" : "not activated";
    }

    private static bool IsDisabledValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Equals("not activated", StringComparison.OrdinalIgnoreCase)
            || value.Equals("[VN]_not_activated", StringComparison.OrdinalIgnoreCase)
            || value.Equals("not active", StringComparison.OrdinalIgnoreCase)
            || value.Equals("[VN]_not_active", StringComparison.OrdinalIgnoreCase)
            || value.Equals("off", StringComparison.OrdinalIgnoreCase)
            || value.Equals("[VN]_off", StringComparison.OrdinalIgnoreCase)
            || value.Equals("not available", StringComparison.OrdinalIgnoreCase)
            || value.Equals("[VN]_not_available", StringComparison.OrdinalIgnoreCase)
            || value.Equals("disabled", StringComparison.OrdinalIgnoreCase)
            || value.Equals("locked", StringComparison.OrdinalIgnoreCase);
    }
}
