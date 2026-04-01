using MibExplorer.Core;
using MibExplorer.Models;
using MibExplorer.Services;
using MibExplorer.Services.Design;
using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.ComponentModel;
using System.Windows.Data;
using MibExplorer.Settings;
using MibExplorer.Views.Dialogs;

namespace MibExplorer.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IMibConnectionService _mibConnectionService;
    private readonly RelayCommand _prepareWorkspaceCommand;
    private readonly RelayCommand _refreshCommand;
    private readonly RelayCommand _downloadCommand;
    private readonly RelayCommand _uploadCommand;
    private readonly RelayCommand _renameCommand;
    private readonly RelayCommand _deleteCommand;
    private readonly RelayCommand _extractCommand;
    private readonly RelayCommand _testConnectionCommand;

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

    public MainViewModel(IMibConnectionService mibConnectionService)
    {
        _mibConnectionService = mibConnectionService;

        RootNodes = new ObservableCollection<RemoteExplorerItem>();
        CurrentFolderItems = new ObservableCollection<RemoteExplorerItem>();
        Breadcrumbs = new ObservableCollection<string>();

        CurrentFolderItemsView = CollectionViewSource.GetDefaultView(CurrentFolderItems);

        ActiveSortColumn = _currentSortColumn;
        IsSortAscending = _currentSortAscending;

        _prepareWorkspaceCommand = new RelayCommand(async () => await PrepareWorkspaceAsync(), () => !IsBusy);
        _refreshCommand = new RelayCommand(async () => await RefreshSelectedFolderAsync(), () => !IsBusy);
        _testConnectionCommand = new RelayCommand(async () => await TestConnectionAsync(), () => !IsBusy);
        _downloadCommand = new RelayCommand(() => ShowPendingMessage("Download"), () => CanRunFileAction);
        _uploadCommand = new RelayCommand(() => ShowPendingMessage("Upload"), () => CanRunFolderAction);
        _renameCommand = new RelayCommand(() => ShowPendingMessage("Rename"), () => CanRunItemAction);
        _deleteCommand = new RelayCommand(() => ShowPendingMessage("Delete"), () => CanRunItemAction);
        _extractCommand = new RelayCommand(() => ShowPendingMessage("Extract folder"), () => CanRunFolderAction);

        PrepareWorkspaceCommand = _prepareWorkspaceCommand;
        RefreshCommand = _refreshCommand;
        TestConnectionCommand = _testConnectionCommand;
        DownloadCommand = _downloadCommand;
        UploadCommand = _uploadCommand;
        RenameCommand = _renameCommand;
        DeleteCommand = _deleteCommand;
        ExtractCommand = _extractCommand;

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

    public RelayCommand PrepareWorkspaceCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand TestConnectionCommand { get; }

    public RelayCommand DownloadCommand { get; }

    public RelayCommand UploadCommand { get; }

    public RelayCommand RenameCommand { get; }

    public RelayCommand DeleteCommand { get; }

    public RelayCommand ExtractCommand { get; }

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

            if (value is null)
            {
                OnPropertyChanged(nameof(SelectedItem));
                RefreshCommands();
                return;
            }

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

    public bool CanRunItemAction => !IsBusy && SelectedItem is not null;

    public bool CanRunFolderAction => !IsBusy && (SelectedItem?.IsDirectory ?? false);

    public bool CanRunFileAction => !IsBusy && SelectedItem is not null && !SelectedItem.IsDirectory;

    public string CurrentFolderLabel => SelectedTreeNode?.FullPath ?? "/";

    private async Task PrepareWorkspaceAsync()
    {
        try
        {
            SetBusyState(true, "Preparing workspace...", 20);

            RootNodes.Clear();
            CurrentFolderItems.Clear();
            Breadcrumbs.Clear();

            var root = new RemoteExplorerItem
            {
                Name = "/",
                FullPath = "/",
                EntryType = RemoteEntryType.Directory,
                IsLoaded = false
            };

            RootNodes.Add(root);
            Breadcrumbs.Add("/");

            if (_mibConnectionService.IsConnected)
            {
                ProgressValue = 60;

                await EnsureChildrenLoadedAsync(root, forceReload: true);
                SelectedTreeNode = root;
                root.IsExpanded = true;
                root.IsSelected = true;

                ProgressValue = 100;
                StatusMessage = "Connected. Remote filesystem loaded from MIB root.";
            }
            else
            {
                SelectedTreeNode = root;
                root.IsExpanded = true;
                root.IsSelected = true;

                ProgressValue = 100;
                StatusMessage = "Workspace ready. Test the SSH connection to load the real MIB filesystem.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Initialization error: {ex.Message}";
        }
        finally
        {
            SetBusyState(false, "Ready", 0);
        }
    }

    private async Task TestConnectionAsync()
    {
        try
        {
            SetBusyState(true, "Testing SSH connection...", 25);

            string privateKeyPath = !string.IsNullOrWhiteSpace(PrivateKeyPath)
                ? PrivateKeyPath
                : Path.Combine(AppContext.BaseDirectory, "Keys", "id_rsa");

            var settings = new ConnectionSettings
            {
                Host = Host.Trim(),
                Port = int.TryParse(Port, out int parsedPort) ? parsedPort : 22,
                Username = Username.Trim(),
                Password = Password,
                UsePrivateKey = UsePrivateKey && File.Exists(privateKeyPath),
                PrivateKeyPath = privateKeyPath,
                WorkspaceFolder = WorkspaceFolder,
                PublicKeyExportPath = PublicKeyExportPath
            };

            await _mibConnectionService.ConnectAsync(settings);

            ProgressValue = 60;

            string pwd = (await _mibConnectionService.ExecuteCommandAsync("pwd")).Trim();
            if (string.IsNullOrWhiteSpace(pwd))
                pwd = "/";

            ProgressValue = 80;

            await PrepareWorkspaceAsync();

            StatusMessage = $"SSH connected successfully. Remote pwd: {pwd}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"SSH connection failed: {ex.Message}";
        }
        finally
        {
            SetBusyState(false, "Ready", 0);
        }
    }

    private async Task RefreshSelectedFolderAsync()
    {
        if (!_mibConnectionService.IsConnected)
        {
            StatusMessage = "Not connected. Test the SSH connection first.";
            return;
        }

        if (SelectedTreeNode is null)
        {
            StatusMessage = "No folder selected.";
            return;
        }

        try
        {
            SetBusyState(true, $"Refreshing {SelectedTreeNode.FullPath}...", 40);

            SelectedTreeNode.IsLoaded = false;
            await EnsureChildrenLoadedAsync(SelectedTreeNode, forceReload: true);
            await PopulateCurrentFolderAsync(SelectedTreeNode);

            StatusMessage = $"Refreshed {SelectedTreeNode.FullPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh failed: {ex.Message}";
        }
        finally
        {
            SetBusyState(false, "Ready", 0);
        }
    }

    private async Task OnTreeNodeSelectedAsync(RemoteExplorerItem node)
    {
        SelectedListItem = null;
        await EnsureChildrenLoadedAsync(node);
        await PopulateCurrentFolderAsync(node);
        UpdateBreadcrumbs(node.FullPath);

        OnPropertyChanged(nameof(CurrentFolderLabel));
        OnPropertyChanged(nameof(SelectedItem));
        OnPropertyChanged(nameof(SelectedPath));
        OnPropertyChanged(nameof(SelectedType));
        OnPropertyChanged(nameof(SelectedSize));
        OnPropertyChanged(nameof(SelectedModifiedAt));
        OnPropertyChanged(nameof(SelectedName));
        OnPropertyChanged(nameof(SelectedHint));
        RefreshCommands();
    }

    private async Task PopulateCurrentFolderAsync(RemoteExplorerItem node)
    {
        if (!node.IsDirectory)
            return;

        if (!_mibConnectionService.IsConnected)
            return;

        try
        {
            var children = await _mibConnectionService.GetChildrenAsync(node.FullPath);

            CurrentFolderItems.Clear();

            foreach (var child in children)
                CurrentFolderItems.Add(child);

            ApplySort();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load folder content: {ex.Message}";
        }
    }

    public void SortCurrentFolder(string columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            return;

        columnName = columnName.Trim();

        if (string.Equals(_currentSortColumn, columnName, StringComparison.OrdinalIgnoreCase))
            _currentSortAscending = !_currentSortAscending;
        else
        {
            _currentSortColumn = columnName;
            _currentSortAscending = true;
        }

        ActiveSortColumn = _currentSortColumn;
        IsSortAscending = _currentSortAscending;

        ApplySort();
    }

    private void ApplySort()
    {
        if (CurrentFolderItemsView is not ListCollectionView listView)
            return;

        using (listView.DeferRefresh())
        {
            listView.CustomSort = new RemoteExplorerItemComparer(_currentSortColumn, _currentSortAscending);
        }
    }

    private async Task EnsureChildrenLoadedAsync(RemoteExplorerItem node, bool forceReload = false)
    {
        if (!node.IsDirectory)
            return;

        if (!_mibConnectionService.IsConnected)
            return;

        if (node.IsLoaded && !forceReload)
            return;

        try
        {
            node.IsLoading = true;
            var children = await _mibConnectionService.GetChildrenAsync(node.FullPath);
            node.Children.Clear();

            foreach (var child in children
             .Where(x => x.IsDirectory)
             .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                child.Children.Add(new RemoteExplorerItem
                {
                    Name = "Loading...",
                    FullPath = child.FullPath + "/__placeholder__",
                    EntryType = RemoteEntryType.Unknown
                });

                node.Children.Add(child);
            }

            node.IsLoaded = true;
        }
        finally
        {
            node.IsLoading = false;
        }
    }

    private void ShowPendingMessage(string actionName)
    {
        var target = SelectedItem?.FullPath ?? "current selection";
        AppMessageBox.Show(
            $"{actionName} is not implemented yet.\n\nTarget: {target}",
            "MibExplorer",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void UpdateBreadcrumbs(string path)
    {
        Breadcrumbs.Clear();

        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            Breadcrumbs.Add("/");
            return;
        }

        Breadcrumbs.Add("/");
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            Breadcrumbs.Add(segment);
    }

    private void SetBusyState(bool isBusy, string progressLabel, double progressValue)
    {
        IsBusy = isBusy;
        ProgressLabel = progressLabel;
        ProgressValue = progressValue;
    }

    private void RefreshCommands()
    {
        _prepareWorkspaceCommand.RaiseCanExecuteChanged();
        _refreshCommand.RaiseCanExecuteChanged();
        _testConnectionCommand.RaiseCanExecuteChanged();
        _downloadCommand.RaiseCanExecuteChanged();
        _uploadCommand.RaiseCanExecuteChanged();
        _renameCommand.RaiseCanExecuteChanged();
        _deleteCommand.RaiseCanExecuteChanged();
        _extractCommand.RaiseCanExecuteChanged();

        OnPropertyChanged(nameof(CanRunItemAction));
        OnPropertyChanged(nameof(CanRunFolderAction));
        OnPropertyChanged(nameof(CanRunFileAction));
        OnPropertyChanged(nameof(SelectedPath));
        OnPropertyChanged(nameof(SelectedType));
        OnPropertyChanged(nameof(SelectedSize));
        OnPropertyChanged(nameof(SelectedModifiedAt));
        OnPropertyChanged(nameof(SelectedName));
        OnPropertyChanged(nameof(SelectedHint));
    }

    private static bool IsPlaceholder(RemoteExplorerItem item)
    {
        return item.EntryType == RemoteEntryType.Unknown && item.Name == "Loading...";
    }
}
