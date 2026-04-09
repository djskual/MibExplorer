using MibExplorer.Core;
using MibExplorer.Models;
using MibExplorer.Services;
using MibExplorer.Services.Design;
using MibExplorer.Settings;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace MibExplorer.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IMibConnectionService _mibConnectionService;
    private readonly RelayCommand _refreshCommand;
    private readonly RelayCommand _downloadCommand;
    private readonly RelayCommand _uploadCommand;
    private readonly RelayCommand _renameCommand;
    private readonly RelayCommand _deleteCommand;
    private readonly RelayCommand _extractCommand;
    private readonly RelayCommand _replaceCommand;
    private readonly RelayCommand _uploadFolderCommand;

    private readonly RelayCommand _connectionCommand;
    private readonly DispatcherTimer _connectionMonitorTimer;
    private bool _isConnectedToMib;
    private bool _isConnectionProbeRunning;

    private string _host = "192.168.1.10";
    private string _port = "22";
    private string _username = "root";
    private string _password = string.Empty;
    private bool _usePrivateKey = true;
    private string _privateKeyPath = string.Empty;
    private string _workspaceFolder = string.Empty;
    private string _publicKeyExportPath = string.Empty;
    private string _statusMessage = "Ready. Generate SSH keys, enable SSH on the MIB, then test the connection.";
    private bool _isBusy;
    private double _progressValue;
    private string _progressLabel = "Idle";
    private RemoteExplorerItem? _selectedTreeNode;
    private RemoteExplorerItem? _selectedListItem;

    private string _currentSortColumn = "Name";
    private bool _currentSortAscending = true;

    public MainViewModel()
        : this(new DesignMibConnectionService())
    {
    }

    public IMibConnectionService ConnectionService => _mibConnectionService;

    public MainViewModel(IMibConnectionService mibConnectionService)
    {
        _mibConnectionService = mibConnectionService;

        RootNodes = new ObservableCollection<RemoteExplorerItem>();
        CurrentFolderItems = new ObservableCollection<RemoteExplorerItem>();
        Breadcrumbs = new ObservableCollection<string>();

        CurrentFolderItemsView = CollectionViewSource.GetDefaultView(CurrentFolderItems);

        ActiveSortColumn = _currentSortColumn;
        IsSortAscending = _currentSortAscending;

        _refreshCommand = new RelayCommand(
            async () => await RefreshSelectedFolderAsync(),
            () => !IsBusy && IsConnectedToMib && SelectedTreeNode is not null);
        _connectionCommand = new RelayCommand(async () => await ToggleConnectionAsync(), () => !IsBusy);
        _downloadCommand = new RelayCommand(async () => await DownloadSelectedFileAsync(), () => CanRunFileAction);
        _uploadCommand = new RelayCommand(async () => await UploadFileToSelectedFolderAsync(), () => CanRunFolderAction);
        _renameCommand = new RelayCommand(async () => await RenameSelectedItemAsync(), () => CanRunItemAction);
        _deleteCommand = new RelayCommand(async () => await DeleteSelectedFileAsync(), () => CanRunItemAction);
        _extractCommand = new RelayCommand(async () => await ExtractSelectedFolderAsync(), () => CanRunFolderAction);
        _replaceCommand = new RelayCommand(async () => await ReplaceSelectedFileAsync(), () => CanRunFileAction);
        _uploadFolderCommand = new RelayCommand(
            async () => await UploadFolderAsync(),
            () => IsConnectedToMib && SelectedTreeNode is { IsDirectory: true } && !IsBusy);

        _connectionMonitorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        _connectionMonitorTimer.Tick += ConnectionMonitorTimer_Tick;

        RefreshCommand = _refreshCommand;
        ConnectionCommand = _connectionCommand;
        DownloadCommand = _downloadCommand;
        UploadCommand = _uploadCommand;
        RenameCommand = _renameCommand;
        DeleteCommand = _deleteCommand;
        ExtractCommand = _extractCommand;
        ReplaceCommand = _replaceCommand;

        var settings = AppSettingsStore.Current;

        if (!string.IsNullOrWhiteSpace(settings.LastHost))
            Host = settings.LastHost;

        if (!string.IsNullOrWhiteSpace(settings.LastPort))
            Port = settings.LastPort;

        if (!string.IsNullOrWhiteSpace(settings.LastUsername))
            Username = settings.LastUsername;

        UsePrivateKey = settings.UsePrivateKey;

        if (!string.IsNullOrWhiteSpace(settings.LastPrivateKeyPath))
            PrivateKeyPath = settings.LastPrivateKeyPath;
        else
            PrivateKeyPath = Path.Combine(AppContext.BaseDirectory, "Keys", "id_rsa");

        if (!string.IsNullOrWhiteSpace(settings.LastWorkspaceFolder))
            WorkspaceFolder = settings.LastWorkspaceFolder;
        else
            WorkspaceFolder = Path.Combine(AppContext.BaseDirectory, "Keys");

        if (!string.IsNullOrWhiteSpace(settings.LastPublicKeyExportPath))
            PublicKeyExportPath = settings.LastPublicKeyExportPath;
        else
            PublicKeyExportPath = Path.Combine(AppContext.BaseDirectory, "Keys", "id_rsa.pub");

        _ = PrepareWorkspaceAsync();
    }

    public ObservableCollection<RemoteExplorerItem> RootNodes { get; }

    public ObservableCollection<RemoteExplorerItem> CurrentFolderItems { get; }

    public ICollectionView CurrentFolderItemsView { get; }

    public ObservableCollection<string> Breadcrumbs { get; }

    public RelayCommand RefreshCommand { get; }

    public bool IsConnectedToMib
    {
        get => _isConnectedToMib;
        private set
        {
            if (SetProperty(ref _isConnectedToMib, value))
            {
                OnPropertyChanged(nameof(ConnectionButtonText));
                OnPropertyChanged(nameof(ConnectionStateText));
                RefreshCommands();
            }
        }
    }

    public string ConnectionButtonText => IsConnectedToMib ? "Disconnect" : "Connect";

    public string ConnectionStateText => IsConnectedToMib ? "Connected" : "Disconnected";

    public RelayCommand ConnectionCommand { get; }

    public RelayCommand DownloadCommand { get; }

    public RelayCommand UploadCommand { get; }

    public RelayCommand RenameCommand { get; }

    public RelayCommand DeleteCommand { get; }

    public RelayCommand ExtractCommand { get; }

    public RelayCommand ReplaceCommand { get; }

    public RelayCommand UploadFolderCommand => _uploadFolderCommand;

    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }

    public string Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public bool UsePrivateKey
    {
        get => _usePrivateKey;
        set => SetProperty(ref _usePrivateKey, value);
    }

    public string PrivateKeyPath
    {
        get => _privateKeyPath;
        set => SetProperty(ref _privateKeyPath, value);
    }

    public string WorkspaceFolder
    {
        get => _workspaceFolder;
        set => SetProperty(ref _workspaceFolder, value);
    }

    public string PublicKeyExportPath
    {
        get => _publicKeyExportPath;
        set => SetProperty(ref _publicKeyExportPath, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsProgressVisible));
                RefreshCommands();
            }
        }
    }

    public bool IsProgressVisible => IsBusy;

    public Visibility SelectedItemVisibility =>
        SelectedItem != null ? Visibility.Visible : Visibility.Collapsed;

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public string ProgressLabel
    {
        get => _progressLabel;
        private set => SetProperty(ref _progressLabel, value);
    }

    private string? _activeSortColumn;
    public string? ActiveSortColumn
    {
        get => _activeSortColumn;
        set => SetProperty(ref _activeSortColumn, value);
    }

    private bool _isSortAscending = true;
    public bool IsSortAscending
    {
        get => _isSortAscending;
        set => SetProperty(ref _isSortAscending, value);
    }

    public RemoteExplorerItem? SelectedTreeNode
    {
        get => _selectedTreeNode;
        set
        {
            if (!SetProperty(ref _selectedTreeNode, value))
                return;

            OnPropertyChanged(nameof(SelectedItem));
            RefreshCommands();

            if (value is null)
                return;

            _ = OnTreeNodeSelectedAsync(value);
        }
    }

    public RemoteExplorerItem? SelectedListItem
    {
        get => _selectedListItem;
        set
        {
            if (!SetProperty(ref _selectedListItem, value))
                return;

            OnPropertyChanged(nameof(SelectedItem));
            RefreshCommands();
        }
    }

    public RemoteExplorerItem? SelectedItem => SelectedListItem ?? SelectedTreeNode;

    public string SelectedPath => SelectedItem?.FullPath ?? "-";

    public string SelectedType => SelectedItem?.TypeLabel ?? "-";

    public string SelectedSize => SelectedItem?.SizeLabel ?? "-";

    public string SelectedModifiedAt => SelectedItem?.ModifiedAtLabel ?? "-";

    public string SelectedName => SelectedItem?.Name ?? "Nothing selected";

    public string SelectedHint => SelectedItem is null
        ? "Select a remote file or folder to inspect its metadata."
        : SelectedItem.IsDirectory
            ? "Folder selected. This is where upload and recursive extract will hook in."
            : "File selected. This is where download, rename and controlled edit will hook in.";

    public bool CanRunItemAction =>
        !IsBusy &&
        IsConnectedToMib &&
        SelectedItem is not null;

    public bool CanRunFolderAction =>
        !IsBusy &&
        IsConnectedToMib &&
        (SelectedItem?.IsDirectory ?? false);

    public bool CanRunFileAction =>
        !IsBusy &&
        IsConnectedToMib &&
        SelectedItem is not null &&
        !SelectedItem.IsDirectory;

    public string CurrentFolderLabel => SelectedTreeNode?.FullPath ?? "/";
}
