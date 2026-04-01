using MibExplorer.Core;
using MibExplorer.Models;
using MibExplorer.Services;
using MibExplorer.Services.Design;
using MibExplorer.Settings;
using MibExplorer.Views.Dialogs;
using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.ComponentModel;
using System.Windows.Data;
using Microsoft.Win32;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MibExplorer.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IMibConnectionService _mibConnectionService;
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

        _refreshCommand = new RelayCommand(async () => await RefreshSelectedFolderAsync(), () => !IsBusy);
        _testConnectionCommand = new RelayCommand(async () => await TestConnectionAsync(), () => !IsBusy);
        _downloadCommand = new RelayCommand(async () => await DownloadSelectedFileAsync(), () => CanRunFileAction);
        _uploadCommand = new RelayCommand(async () => await UploadFileToSelectedFolderAsync(), () => CanRunFolderAction);
        _renameCommand = new RelayCommand(() => ShowPendingMessage("Rename"), () => CanRunItemAction);
        _deleteCommand = new RelayCommand(async () => await DeleteSelectedFileAsync(), () => CanRunFileAction);
        _extractCommand = new RelayCommand(async () => await ExtractSelectedFolderAsync(), () => CanRunFolderAction);

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

    private sealed class ExtractDirectoryPlan
    {
        public string LocalRelativePath { get; init; } = string.Empty;
        public string RemoteRelativePath { get; init; } = string.Empty;
    }

    private sealed class ExtractFilePlan
    {
        public string RemotePath { get; init; } = string.Empty;
        public string LocalRelativePath { get; init; } = string.Empty;
        public string RemoteRelativePath { get; init; } = string.Empty;
        public ulong Size { get; init; }
    }

    private sealed class ExtractPlan
    {
        public List<ExtractDirectoryPlan> Directories { get; } = [];
        public List<ExtractFilePlan> Files { get; } = [];
        public ulong TotalBytes { get; set; }
    }

    private sealed class ExtractMapFile
    {
        [JsonPropertyName("version")]
        public int Version { get; init; } = 1;

        [JsonPropertyName("remoteRoot")]
        public string RemoteRoot { get; init; } = string.Empty;

        [JsonPropertyName("entries")]
        public List<ExtractMapEntry> Entries { get; init; } = [];
    }

    private sealed class ExtractMapEntry
    {
        [JsonPropertyName("localRelativePath")]
        public string LocalRelativePath { get; init; } = string.Empty;

        [JsonPropertyName("remoteRelativePath")]
        public string RemoteRelativePath { get; init; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;
    }

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

    private async Task DownloadSelectedFileAsync()
    {
        if (!_mibConnectionService.IsConnected)
        {
            StatusMessage = "Not connected. Test the SSH connection first.";
            return;
        }

        if (SelectedItem is null || SelectedItem.IsDirectory)
        {
            StatusMessage = "Select a file to download.";
            return;
        }

        var selectedFile = SelectedItem;

        var saveDialog = new SaveFileDialog
        {
            Title = "Download remote file",
            FileName = SanitizeLocalPathSegment(selectedFile.Name),
            Filter = "All files (*.*)|*.*",
            OverwritePrompt = true,
            AddExtension = false,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        };

        if (saveDialog.ShowDialog() != true)
        {
            StatusMessage = "Download cancelled.";
            return;
        }

        try
        {
            SetBusyState(true, $"Downloading {selectedFile.Name}...", 0);
            StatusMessage = $"Starting download of {selectedFile.Name}...";

            var progress = new Progress<FileTransferProgressInfo>(info =>
            {
                if (info.HasKnownLength)
                {
                    ProgressValue = info.Percentage;
                    ProgressLabel = $"{info.Percentage:0}%";
                    StatusMessage =
                        $"Downloading {selectedFile.Name}... " +
                        $"{FormatTransferSize(info.BytesTransferred)} / {FormatTransferSize(info.TotalBytes!.Value)}";
                }
                else
                {
                    ProgressLabel = "Working...";
                    StatusMessage = $"Downloading {selectedFile.Name}...";
                }
            });

            await _mibConnectionService.DownloadFileAsync(
                selectedFile.FullPath,
                saveDialog.FileName,
                progress);

            ProgressValue = 100;
            ProgressLabel = "100%";
            StatusMessage = $"Downloaded {selectedFile.FullPath} to {saveDialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Download failed: {ex.Message}";
            AppMessageBox.Show(
                $"Failed to download file.\n\n{ex.Message}",
                "MibExplorer",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false, "Ready", 0);
        }
    }

    private async Task UploadFileToSelectedFolderAsync()
    {
        if (!_mibConnectionService.IsConnected)
        {
            StatusMessage = "Not connected. Test the SSH connection first.";
            return;
        }

        if (SelectedItem is null || !SelectedItem.IsDirectory)
        {
            StatusMessage = "Select a destination folder first.";
            return;
        }

        var selectedFolder = SelectedItem;

        var openDialog = new OpenFileDialog
        {
            Title = "Select file to upload",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            Filter = "All files (*.*)|*.*"
        };

        if (openDialog.ShowDialog() != true)
        {
            StatusMessage = "Upload cancelled.";
            return;
        }

        string localPath = openDialog.FileName;
        string fileName = Path.GetFileName(localPath);
        string remotePath = selectedFolder.FullPath.TrimEnd('/') + "/" + fileName;

        try
        {
            SetBusyState(true, $"Uploading {fileName}...", 0);
            StatusMessage = $"Starting upload of {fileName}...";

            var progress = new Progress<FileTransferProgressInfo>(info =>
            {
                if (info.HasKnownLength)
                {
                    ProgressValue = info.Percentage;
                    ProgressLabel = $"{info.Percentage:0}%";
                    StatusMessage =
                        $"Uploading {fileName}... " +
                        $"{FormatTransferSize(info.BytesTransferred)} / {FormatTransferSize(info.TotalBytes!.Value)}";
                }
                else
                {
                    ProgressLabel = "Working...";
                    StatusMessage = $"Uploading {fileName}...";
                }
            });

            await _mibConnectionService.UploadFileAsync(localPath, remotePath, progress);

            ProgressValue = 100;
            ProgressLabel = "100%";
            StatusMessage = $"Uploaded {localPath} to {remotePath}";

            await EnsureChildrenLoadedAsync(selectedFolder, forceReload: true);
            await PopulateCurrentFolderAsync(selectedFolder);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Upload failed: {ex.Message}";
            AppMessageBox.Show(
                $"Failed to upload file.\n\n{ex.Message}",
                "MibExplorer",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false, "Ready", 0);
        }
    }

    private async Task DeleteSelectedFileAsync()
    {
        if (!_mibConnectionService.IsConnected)
        {
            StatusMessage = "Not connected.";
            return;
        }

        if (SelectedItem is null || SelectedItem.IsDirectory)
        {
            StatusMessage = "Select a file to delete.";
            return;
        }

        var file = SelectedItem;

        if (!_mibConnectionService.CanWriteToPath(file.FullPath))
        {
            StatusMessage = "Path is not writable.";

            AppMessageBox.Show(
                $"This path cannot be modified.\n\n{file.FullPath}",
                "Not allowed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var result = AppMessageBox.Show(
            $"Delete file?\n\n{file.FullPath}",
            "Confirm delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            StatusMessage = "Delete cancelled.";
            return;
        }

        try
        {
            SetBusyState(true, $"Deleting {file.Name}...", 0);

            await _mibConnectionService.DeleteFileAsync(file.FullPath);

            StatusMessage = $"Deleted {file.FullPath}";

            if (SelectedTreeNode is not null)
            {
                await EnsureChildrenLoadedAsync(SelectedTreeNode, true);
                await PopulateCurrentFolderAsync(SelectedTreeNode);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete failed: {ex.Message}";
            AppMessageBox.Show(
                $"Failed to delete file.\n\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false, "Ready", 0);
        }
    }

    private static string CombineRelativePath(string parent, string child)
    {
        if (string.IsNullOrWhiteSpace(parent))
            return child;

        return Path.Combine(parent, child);
    }

    private static string SanitizeLocalPathSegment(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "_";

        char[] invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);

        foreach (char c in name)
        {
            builder.Append(Array.IndexOf(invalidChars, c) >= 0 ? '_' : c);
        }

        string sanitized = builder.ToString().Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "_";

        sanitized = sanitized.TrimEnd('.', ' ');

        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "_";

        string upper = sanitized.ToUpperInvariant();

        if (upper is "CON" or "PRN" or "AUX" or "NUL" or
            "COM1" or "COM2" or "COM3" or "COM4" or "COM5" or "COM6" or "COM7" or "COM8" or "COM9" or
            "LPT1" or "LPT2" or "LPT3" or "LPT4" or "LPT5" or "LPT6" or "LPT7" or "LPT8" or "LPT9")
        {
            sanitized = "_" + sanitized;
        }

        return sanitized;
    }

    private static string GetExtractRootFolderName(RemoteExplorerItem folder)
    {
        return folder.FullPath == "/"
            ? "root"
            : folder.Name;
    }

    private async Task WriteExtractMapAsync(string extractRoot, string remoteRootPath, ExtractPlan plan)
    {
        string mapPath = Path.Combine(extractRoot, ".mibexplorer-map.json");

        string remoteRootNormalized = NormalizeRemotePathForMap(remoteRootPath);

        var entries = new List<ExtractMapEntry>();

        foreach (ExtractDirectoryPlan directory in plan.Directories.OrderBy(p => p.LocalRelativePath, StringComparer.Ordinal))
        {
            entries.Add(new ExtractMapEntry
            {
                LocalRelativePath = NormalizeRelativePathForMap(directory.LocalRelativePath),
                RemoteRelativePath = NormalizeRelativePathForMap(directory.RemoteRelativePath),
                Type = "directory"
            });
        }

        foreach (ExtractFilePlan file in plan.Files.OrderBy(f => f.LocalRelativePath, StringComparer.Ordinal))
        {
            entries.Add(new ExtractMapEntry
            {
                LocalRelativePath = NormalizeRelativePathForMap(file.LocalRelativePath),
                RemoteRelativePath = NormalizeRelativePathForMap(file.RemoteRelativePath),
                Type = "file"
            });
        }

        var map = new ExtractMapFile
        {
            RemoteRoot = remoteRootNormalized,
            Entries = entries
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json = JsonSerializer.Serialize(map, jsonOptions);
        await File.WriteAllTextAsync(mapPath, json);
    }

    private static string NormalizeRemotePathForMap(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/";

        return path.Replace('\\', '/').TrimEnd('/');
    }

    private static string NormalizeRelativePathForMap(string path)
    {
        return path.Replace('\\', '/');
    }

    private async Task ExtractSelectedFolderAsync()
    {
        if (!_mibConnectionService.IsConnected)
        {
            StatusMessage = "Not connected. Test the SSH connection first.";
            return;
        }

        if (SelectedItem is null || !SelectedItem.IsDirectory)
        {
            StatusMessage = "Select a folder to extract.";
            return;
        }

        var selectedFolder = SelectedItem;

        string initialFolder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        var folderDialog = new OpenFolderDialog
        {
            Title = "Select extraction destination",
            InitialDirectory = initialFolder,
            FolderName = initialFolder,
            Multiselect = false
        };

        bool? dialogResult = folderDialog.ShowDialog();

        if (dialogResult != true || string.IsNullOrWhiteSpace(folderDialog.FolderName))
        {
            StatusMessage = "Extract folder cancelled.";
            return;
        }

        string rootFolderName = SanitizeLocalPathSegment(GetExtractRootFolderName(selectedFolder));
        string extractRoot = Path.Combine(folderDialog.FolderName, rootFolderName);

        try
        {
            SetBusyState(true, $"Scanning {selectedFolder.Name}...", 0);
            StatusMessage = $"Scanning {selectedFolder.FullPath} for extraction...";

            ExtractPlan plan = await BuildExtractPlanAsync(selectedFolder.FullPath);

            Directory.CreateDirectory(extractRoot);

            foreach (ExtractDirectoryPlan relativeDirectory in plan.Directories)
                Directory.CreateDirectory(Path.Combine(extractRoot, relativeDirectory.LocalRelativePath));

            await WriteExtractMapAsync(extractRoot, selectedFolder.FullPath, plan);

            if (plan.Files.Count == 0)
            {
                ProgressValue = 100;
                ProgressLabel = "100%";
                StatusMessage = $"Extracted empty folder {selectedFolder.FullPath} to {extractRoot}";
                return;
            }

            ulong completedBytes = 0;

            for (int i = 0; i < plan.Files.Count; i++)
            {
                var file = plan.Files[i];
                int fileIndex = i + 1;
                ulong completedBeforeCurrentFile = completedBytes;

                string localFilePath = Path.Combine(extractRoot, file.LocalRelativePath);

                var progress = new Progress<FileTransferProgressInfo>(info =>
                {
                    if (plan.TotalBytes > 0)
                    {
                        ulong totalTransferred = completedBeforeCurrentFile + info.BytesTransferred;
                        double percentage = Math.Clamp(totalTransferred * 100d / plan.TotalBytes, 0d, 100d);

                        ProgressValue = percentage;
                        ProgressLabel = $"{percentage:0}%";
                        StatusMessage =
                            $"Extracting {fileIndex}/{plan.Files.Count}: {file.LocalRelativePath} " +
                            $"({FormatTransferSize(totalTransferred)} / {FormatTransferSize(plan.TotalBytes)})";
                    }
                    else
                    {
                        double percentage = Math.Clamp((double)(fileIndex - 1) * 100d / plan.Files.Count, 0d, 100d);

                        ProgressValue = percentage;
                        ProgressLabel = $"{percentage:0}%";
                        StatusMessage = $"Extracting {fileIndex}/{plan.Files.Count}: {file.LocalRelativePath}";
                    }
                });

                await _mibConnectionService.DownloadFileAsync(file.RemotePath, localFilePath, progress);

                completedBytes += file.Size;

                if (plan.TotalBytes > 0)
                {
                    double percentage = Math.Clamp(completedBytes * 100d / plan.TotalBytes, 0d, 100d);
                    ProgressValue = percentage;
                    ProgressLabel = $"{percentage:0}%";
                }
            }

            ProgressValue = 100;
            ProgressLabel = "100%";
            StatusMessage = $"Extracted {selectedFolder.FullPath} to {extractRoot}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Extract folder failed: {ex.Message}";
            AppMessageBox.Show(
                $"Failed to extract folder.\n\n{ex.Message}",
                "MibExplorer",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false, "Ready", 0);
        }
    }

    private async Task<ExtractPlan> BuildExtractPlanAsync(string remoteRootPath)
    {
        var plan = new ExtractPlan();
        await BuildExtractPlanRecursiveAsync(remoteRootPath, string.Empty, plan);
        return plan;
    }

    private async Task BuildExtractPlanRecursiveAsync(string remoteFolderPath, string relativeFolderPath, ExtractPlan plan)
    {
        var children = await _mibConnectionService.GetChildrenAsync(remoteFolderPath);

        foreach (var child in children)
        {
            string safeChildName = SanitizeLocalPathSegment(child.Name);
            string remoteRelativeChildPath = CombineRelativePath(relativeFolderPath, child.Name);
            string localRelativeChildPath = CombineRelativePath(relativeFolderPath, safeChildName);

            if (child.IsDirectory)
            {
                plan.Directories.Add(new ExtractDirectoryPlan
                {
                    LocalRelativePath = localRelativeChildPath,
                    RemoteRelativePath = remoteRelativeChildPath
                });

                await BuildExtractPlanRecursiveAsync(child.FullPath, remoteRelativeChildPath, plan);
            }
            else if (child.EntryType == RemoteEntryType.File)
            {
                ulong fileSize = child.Size > 0 ? (ulong)child.Size : 0UL;

                plan.Files.Add(new ExtractFilePlan
                {
                    RemotePath = child.FullPath,
                    LocalRelativePath = localRelativeChildPath,
                    RemoteRelativePath = remoteRelativeChildPath,
                    Size = fileSize
                });

                plan.TotalBytes += fileSize;
            }
        }
    }

    private static string FormatTransferSize(ulong bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        int unitIndex = 0;

        while (size >= 1024d && unitIndex < units.Length - 1)
        {
            size /= 1024d;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{size:0} {units[unitIndex]}"
            : $"{size:0.##} {units[unitIndex]}";
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
