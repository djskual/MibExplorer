using MibExplorer.Core;
using MibExplorer.Models.Coding;
using MibExplorer.Services.Coding;
using MibExplorer.Views.Dialogs;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace MibExplorer.ViewModels;

public sealed class CodingCenterViewModel : ObservableObject
{

    private readonly ICodingCenterService _codingCenterService;
    private readonly CodingFeatureService _featureService = new();
    public ObservableCollection<CodingFeatureValue> Features { get; } = new();
    public ObservableCollection<CodingPendingChange> PendingChanges { get; } = new();
    public ObservableCollection<CodingHistorySnapshot> HistorySnapshots { get; } = new();
    public ObservableCollection<CodingRawFeatureView> SelectedByteFeatures { get; } = new();

    private bool _isLoading;
    private string _statusText = "Ready.";
    private string _codingHex = string.Empty;
    private string _modifiedCodingHex = string.Empty;
    private CodingByte? _selectedByte;
    private string _selectedByteHexInput = string.Empty;
    private string _selectedByteBinaryInput = string.Empty;
    private bool _isSynchronizingRawByteInputs;
    private bool _suspendFeatureRebuild;
    private bool _isSynchronizingLongCodingInput;
    private int _pendingChangesCount;
    private string _changesTabHeader = "Changes";
    private readonly CodingHistoryService _historyService = new();
    private CodingHistorySnapshot? _selectedHistorySnapshot;
    private int _currentByteCount;

    public CodingCenterViewModel(ICodingCenterService codingCenterService)
    {
        _codingCenterService = codingCenterService;
        Bytes = new ObservableCollection<CodingByte>();
        ReloadCommand = new RelayCommand(async () => await LoadAsync(), () => !IsLoading);

        CreateSnapshotCommand = new RelayCommand(
            async () => await CreateSnapshotAsync(),
            () => !IsLoading && !string.IsNullOrWhiteSpace(CodingHex));

        DeleteSnapshotCommand = new RelayCommand(
            async () => await DeleteSelectedSnapshotAsync(),
            () => !IsLoading && SelectedHistorySnapshot is not null);

        ApplyCodingCommand = new RelayCommand(
            async () => await ApplyCodingAsync(),
            () => !IsLoading && PendingChangesCount > 0);

        RestoreSnapshotCommand = new RelayCommand(
            async () => await RestoreSnapshotAsync(),
            () => !IsLoading && SelectedHistorySnapshot is not null);
    }

    public ObservableCollection<CodingByte> Bytes { get; }

    public RelayCommand ReloadCommand { get; }

    public RelayCommand CreateSnapshotCommand { get; }

    public RelayCommand DeleteSnapshotCommand { get; }

    public RelayCommand ApplyCodingCommand { get; }

    public RelayCommand RestoreSnapshotCommand { get; }

    public CodingHistorySnapshot? SelectedHistorySnapshot
    {
        get => _selectedHistorySnapshot;
        set
        {
            if (SetProperty(ref _selectedHistorySnapshot, value))
            {
                DeleteSnapshotCommand.RaiseCanExecuteChanged();
                RestoreSnapshotCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                ReloadCommand.RaiseCanExecuteChanged();
                CreateSnapshotCommand.RaiseCanExecuteChanged();
                DeleteSnapshotCommand.RaiseCanExecuteChanged();
                ApplyCodingCommand.RaiseCanExecuteChanged();
                RestoreSnapshotCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(IsEditorEnabled));
            }
        }
    }

    public bool IsEditorEnabled => !IsLoading;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string CodingHex
    {
        get => _codingHex;
        private set => SetProperty(ref _codingHex, value);
    }

    public string ModifiedCodingHex
    {
        get => _modifiedCodingHex;
        set
        {
            string normalized = (value ?? string.Empty)
                .Trim()
                .ToUpperInvariant();

            if (!SetProperty(ref _modifiedCodingHex, normalized))
                return;

            if (_isSynchronizingLongCodingInput)
                return;

            if (normalized.Length == 0)
                return;

            if (normalized.Length != 50 || !normalized.All(IsHexChar))
            {
                if (normalized.Length >= 50)
                    StatusText = "Invalid long coding. Expected 50 HEX characters.";

                return;
            }

            ApplyExternalCodingHex(normalized);
        }
    }

    private string _vin = "UNKNOWN";
    public string Vin
    {
        get => _vin;
        private set => SetProperty(ref _vin, value);
    }

    public CodingByte? SelectedByte
    {
        get => _selectedByte;
        set
        {
            if (SetProperty(ref _selectedByte, value))
            {
                SynchronizeRawByteInputs();
                RebuildSelectedByteFeatures();
            }
        }
    }

    public string SelectedByteHexInput
    {
        get => _selectedByteHexInput;
        set
        {
            string normalized = (value ?? string.Empty).Trim().ToUpperInvariant();

            if (!SetProperty(ref _selectedByteHexInput, normalized))
                return;

            if (_isSynchronizingRawByteInputs)
                return;

            if (normalized.Length == 0)
                return;

            if (normalized.Length != 2 || !normalized.All(IsHexChar))
            {
                if (normalized.Length >= 2)
                    StatusText = "Invalid HEX byte value.";

                return;
            }

            ApplyRawByteEdit(Convert.ToByte(normalized, 16));
        }
    }

    public string SelectedByteBinaryInput
    {
        get => _selectedByteBinaryInput;
        set
        {
            string normalized = (value ?? string.Empty).Trim();

            if (!SetProperty(ref _selectedByteBinaryInput, normalized))
                return;

            if (_isSynchronizingRawByteInputs)
                return;

            if (normalized.Length == 0)
                return;

            if (normalized.Length != 8 || normalized.Any(c => c is not '0' and not '1'))
            {
                if (normalized.Length >= 8)
                    StatusText = "Invalid binary byte value.";

                return;
            }

            ApplyRawByteEdit(Convert.ToByte(normalized, 2));
        }
    }

    public int PendingChangesCount
    {
        get => _pendingChangesCount;
        private set
        {
            if (SetProperty(ref _pendingChangesCount, value))
            {
                ChangesTabHeader = value == 0 ? "Changes" : $"Changes ({value})";
                OnPropertyChanged(nameof(HasPendingChanges));
            }
        }
    }

    public bool HasPendingChanges => PendingChangesCount > 0;

    public string ChangesTabHeader
    {
        get => _changesTabHeader;
        private set => SetProperty(ref _changesTabHeader, value);
    }

    public async Task LoadAsync()
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;
            StatusText = "Reading 5F coding from MIB...";

            CodingReadResult result = await _codingCenterService.Read5FCodingAsync();

            Bytes.Clear();
            foreach (CodingByte codingByte in result.Bytes)
                Bytes.Add(codingByte);

            CodingHex = result.CodingHex;
            SetModifiedCodingHex(result.CodingHex);
            Vin = result.Vin;
            _currentByteCount = result.ByteCount;
            SelectedByte = Bytes.FirstOrDefault();

            await _featureService.LoadAsync("Data/Coding/mib2_5f_coding_catalog.json");

            foreach (CodingFeatureValue feature in Features)
                feature.PropertyChanged -= Feature_PropertyChanged;

            Features.Clear();
            PendingChanges.Clear();
            PendingChangesCount = 0;

            foreach (CodingFeatureValue feature in _featureService.Decode(result.Bytes))
            {
                feature.PropertyChanged += Feature_PropertyChanged;
                Features.Add(feature);
            }

            RebuildPendingChanges();
            RebuildSelectedByteFeatures();

            await LoadHistoryAsync();

            StatusText = $"Loaded {result.ByteCount} bytes, {Features.Count} feature(s). Vehicle ready.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Feature_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suspendFeatureRebuild)
            return;

        if (e.PropertyName is nameof(CodingFeatureValue.SelectedOption)
            or nameof(CodingFeatureValue.IsModified)
            or nameof(CodingFeatureValue.SelectedRawValue)
            or nameof(CodingFeatureValue.SelectedValue))
        {
            RebuildPendingChanges();
        }
    }

    private void RebuildPendingChanges()
    {
        PendingChanges.Clear();

        List<CodingByte> modifiedBytes = _featureService.BuildModifiedBytes(
            Bytes,
            Features);

        SetModifiedCodingHex(_featureService.BuildCodingHex(modifiedBytes));

        foreach (CodingFeatureValue feature in Features.Where(f => f.IsModified))
        {
            CodingByte? currentByte = Bytes.FirstOrDefault(b => b.Index == feature.Byte);
            CodingByte? modifiedByte = modifiedBytes.FirstOrDefault(b => b.Index == feature.Byte);

            PendingChanges.Add(new CodingPendingChange
            {
                FeatureId = feature.Id,
                Label = feature.Label,
                CurrentValue = feature.Value,
                CurrentRawValue = feature.RawValue,
                NewValue = feature.SelectedValue,
                NewRawValue = feature.SelectedRawValue,
                Key = feature.Key,
                Type = feature.Type,
                Byte = feature.Byte,
                CurrentByteHex = currentByte?.Hex ?? string.Empty,
                NewByteHex = modifiedByte?.Hex ?? string.Empty
            });
        }

        PendingChangesCount = PendingChanges.Count;
        ApplyCodingCommand.RaiseCanExecuteChanged();

        SynchronizeRawByteInputs();
        RebuildSelectedByteFeatures();
    }

    private async Task ApplyCodingAsync()
    {
        if (PendingChangesCount == 0)
            return;

        MessageBoxResult confirm = AppMessageBox.Show(
            $"Apply {PendingChangesCount} coding change(s)?\n\nA snapshot of the current coding will be created before applying.",
            "Apply coding changes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            IsLoading = true;

            string targetHex = ModifiedCodingHex;

            StatusText = "Creating snapshot...";

            await _historyService.AddSnapshotAsync(
                Vin,
                CodingHex,
                _currentByteCount,
                "Before Apply",
                PendingChanges);

            StatusText = "Writing coding to MIB...";

            CodingWriteResult writeResult = await _codingCenterService.Write5FCodingAsync(
                targetHex,
                onOutput: null,
                CancellationToken.None);

            if (!writeResult.Success)
            {
                StatusText = $"Apply failed: {writeResult.Message}";
                await LoadHistoryAsync();
                return;
            }

            if (!string.Equals(writeResult.AfterHex, targetHex, StringComparison.OrdinalIgnoreCase))
            {
                StatusText = "Apply failed: readback mismatch.";
                await LoadHistoryAsync();
                return;
            }

            ApplyLoadedCoding(writeResult.AfterHex);

            await LoadHistoryAsync();

            StatusText = "Coding applied successfully.";
        }
        catch (Exception ex)
        {
            StatusText = $"Apply failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RestoreSnapshotAsync()
    {
        if (SelectedHistorySnapshot is null)
            return;

        MessageBoxResult confirm = AppMessageBox.Show(
            "Restore selected coding snapshot?\n\nCurrent unsaved changes will be discarded.",
            "Restore coding snapshot",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            IsLoading = true;

            string targetHex = SelectedHistorySnapshot.CodingHex;

            StatusText = "Restoring coding snapshot...";

            CodingWriteResult writeResult = await _codingCenterService.Write5FCodingAsync(
                targetHex,
                onOutput: null,
                CancellationToken.None);

            if (!writeResult.Success)
            {
                StatusText = $"Restore failed: {writeResult.Message}";
                return;
            }

            if (!string.Equals(writeResult.AfterHex, targetHex, StringComparison.OrdinalIgnoreCase))
            {
                StatusText = "Restore failed: readback mismatch.";
                return;
            }

            ApplyLoadedCoding(writeResult.AfterHex);

            await LoadHistoryAsync();

            StatusText = "Snapshot restored successfully.";
        }
        catch (Exception ex)
        {
            StatusText = $"Restore failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SetModifiedCodingHex(string codingHex)
    {
        try
        {
            _isSynchronizingLongCodingInput = true;
            ModifiedCodingHex = codingHex.ToUpperInvariant();
        }
        finally
        {
            _isSynchronizingLongCodingInput = false;
        }
    }

    private void ApplyExternalCodingHex(string targetHex)
    {
        if (Bytes.Count == 0)
            return;

        if (targetHex.Length != Bytes.Count * 2)
        {
            StatusText = $"Invalid long coding length. Expected {Bytes.Count * 2} HEX characters.";
            return;
        }

        var targetBytes = new List<byte>();

        for (int i = 0; i < targetHex.Length; i += 2)
            targetBytes.Add(Convert.ToByte(targetHex.Substring(i, 2), 16));

        _suspendFeatureRebuild = true;

        try
        {
            foreach (CodingFeatureValue feature in Features)
            {
                if (feature.Byte < 0 || feature.Byte >= targetBytes.Count)
                    continue;

                int rawValue = (targetBytes[feature.Byte] & feature.Mask) >> feature.Shift;
                string raw = rawValue.ToString();

                if (string.Equals(raw, feature.RawValue, StringComparison.OrdinalIgnoreCase))
                {
                    feature.SelectedOption = null;
                    continue;
                }

                CodingFeatureOption? option = feature.Options.FirstOrDefault(o =>
                    string.Equals(o.Raw, raw, StringComparison.OrdinalIgnoreCase));

                if (option is null)
                {
                    option = new CodingFeatureOption
                    {
                        Raw = raw,
                        Label = $"Unknown ({raw})"
                    };

                    feature.Options.Add(option);
                }

                feature.SelectedOption = option;
            }
        }
        finally
        {
            _suspendFeatureRebuild = false;
        }

        RebuildPendingChanges();

        StatusText = PendingChangesCount == 0
            ? "Long coding matches current coding."
            : "Long coding loaded. Review changes before applying.";
    }

    private void SynchronizeRawByteInputs()
    {
        if (SelectedByte is null)
        {
            SetRawByteInputs(string.Empty, string.Empty);
            return;
        }

        CodingByte? modifiedByte = GetModifiedBytes()
            .FirstOrDefault(b => b.Index == SelectedByte.Index);

        if (modifiedByte is null)
        {
            SetRawByteInputs(string.Empty, string.Empty);
            return;
        }

        SetRawByteInputs(modifiedByte.Hex, modifiedByte.Binary);
    }

    private void SetRawByteInputs(string hex, string binary)
    {
        try
        {
            _isSynchronizingRawByteInputs = true;

            SelectedByteHexInput = hex;
            SelectedByteBinaryInput = binary;
        }
        finally
        {
            _isSynchronizingRawByteInputs = false;
        }
    }

    private List<CodingByte> GetModifiedBytes()
    {
        return _featureService.BuildModifiedBytes(
            Bytes,
            Features);
    }

    private void ApplyRawByteEdit(byte newByteValue)
    {
        if (SelectedByte is null)
            return;

        _suspendFeatureRebuild = true;

        try
        {
            foreach (CodingFeatureValue feature in Features.Where(f => f.Byte == SelectedByte.Index))
            {
                int rawValue = (newByteValue & feature.Mask) >> feature.Shift;
                string raw = rawValue.ToString();

                if (string.Equals(raw, feature.RawValue, StringComparison.OrdinalIgnoreCase))
                {
                    feature.SelectedOption = null;
                    continue;
                }

                CodingFeatureOption? option = feature.Options.FirstOrDefault(o =>
                    string.Equals(o.Raw, raw, StringComparison.OrdinalIgnoreCase));

                if (option is null)
                {
                    option = new CodingFeatureOption
                    {
                        Raw = raw,
                        Label = $"Unknown ({raw})"
                    };

                    feature.Options.Add(option);
                }

                feature.SelectedOption = option;
            }
        }
        finally
        {
            _suspendFeatureRebuild = false;
        }

        RebuildPendingChanges();
    }

    private static bool IsHexChar(char value)
    {
        return value is >= '0' and <= '9'
            or >= 'a' and <= 'f'
            or >= 'A' and <= 'F';
    }

    private void RebuildSelectedByteFeatures()
    {
        SelectedByteFeatures.Clear();

        if (SelectedByte is null)
            return;

        foreach (CodingFeatureValue feature in Features
                     .Where(f => f.Byte == SelectedByte.Index)
                     .OrderBy(f => f.Shift)
                     .ThenBy(f => f.Label))
        {
            SelectedByteFeatures.Add(new CodingRawFeatureView(feature));
        }
    }

    private void ApplyLoadedCoding(string codingHex)
    {
        CodingHex = codingHex.ToUpperInvariant();
        SetModifiedCodingHex(CodingHex);

        Bytes.Clear();

        for (int i = 0; i < CodingHex.Length; i += 2)
        {
            Bytes.Add(new CodingByte
            {
                Index = i / 2,
                Value = Convert.ToByte(CodingHex.Substring(i, 2), 16)
            });
        }

        foreach (CodingFeatureValue feature in Features)
            feature.PropertyChanged -= Feature_PropertyChanged;

        Features.Clear();

        var decoded = _featureService.Decode(Bytes);

        foreach (CodingFeatureValue feature in decoded)
        {
            feature.PropertyChanged += Feature_PropertyChanged;
            Features.Add(feature);
        }

        PendingChanges.Clear();
        PendingChangesCount = 0;
        SynchronizeRawByteInputs();
        RebuildSelectedByteFeatures();
    }

    private async Task LoadHistoryAsync()
    {
        HistorySnapshots.Clear();

        List<CodingHistorySnapshot> snapshots = await _historyService.LoadSnapshotsAsync(Vin);

        foreach (CodingHistorySnapshot snapshot in snapshots.OrderByDescending(s => s.CreatedAt))
            HistorySnapshots.Add(snapshot);

        SelectedHistorySnapshot = HistorySnapshots.FirstOrDefault();
    }

    private async Task CreateSnapshotAsync()
    {
        if (string.IsNullOrWhiteSpace(CodingHex))
            return;

        try
        {
            IsLoading = true;
            StatusText = "Creating coding snapshot...";

            CodingHistorySnapshot snapshot = await _historyService.AddSnapshotAsync(
                Vin,
                CodingHex,
                _currentByteCount,
                PendingChangesCount == 0 ? "Manual snapshot" : "Before apply preview",
                PendingChanges);

            await LoadHistoryAsync();

            SelectedHistorySnapshot = HistorySnapshots.FirstOrDefault(s =>
                string.Equals(s.Id, snapshot.Id, StringComparison.OrdinalIgnoreCase));

            StatusText = "Coding snapshot created.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to create snapshot: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteSelectedSnapshotAsync()
    {
        if (SelectedHistorySnapshot is null)
            return;

        try
        {
            IsLoading = true;
            StatusText = "Deleting coding snapshot...";

            await _historyService.DeleteSnapshotAsync(Vin, SelectedHistorySnapshot);

            await LoadHistoryAsync();

            StatusText = "Coding snapshot deleted.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to delete snapshot: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}