using MibExplorer.Core;
using MibExplorer.Models;
using MibExplorer.Services;
using MibExplorer.Services.Design;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.ComponentModel;
using System.Windows.Data;

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

    private string _host = "192.168.1.10";
    private string _port = "22";
    private string _username = "root";
    private string _password = string.Empty;
    private string _statusMessage = "UI initialized. SSH layer will be wired in the next step.";
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
        _downloadCommand = new RelayCommand(() => ShowPendingMessage("Download"), () => CanRunFileAction);
        _uploadCommand = new RelayCommand(() => ShowPendingMessage("Upload"), () => CanRunFolderAction);
        _renameCommand = new RelayCommand(() => ShowPendingMessage("Rename"), () => CanRunItemAction);
        _deleteCommand = new RelayCommand(() => ShowPendingMessage("Delete"), () => CanRunItemAction);
        _extractCommand = new RelayCommand(() => ShowPendingMessage("Extract folder"), () => CanRunFolderAction);

        PrepareWorkspaceCommand = _prepareWorkspaceCommand;
        RefreshCommand = _refreshCommand;
        DownloadCommand = _downloadCommand;
        UploadCommand = _uploadCommand;
        RenameCommand = _renameCommand;
        DeleteCommand = _deleteCommand;
        ExtractCommand = _extractCommand;

        _ = PrepareWorkspaceAsync();
    }

    public ObservableCollection<RemoteExplorerItem> RootNodes { get; }

    public ObservableCollection<RemoteExplorerItem> CurrentFolderItems { get; }

    public ICollectionView CurrentFolderItemsView { get; }

    public ObservableCollection<string> Breadcrumbs { get; }

    public RelayCommand PrepareWorkspaceCommand { get; }

    public RelayCommand RefreshCommand { get; }

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
            SetBusyState(true, "Loading sample MIB structure...", 20);

            RootNodes.Clear();
            CurrentFolderItems.Clear();
            Breadcrumbs.Clear();

            var root = new RemoteExplorerItem
            {
                Name = "/",
                FullPath = "/",
                EntryType = RemoteEntryType.Directory
            };

            root.Children.Add(new RemoteExplorerItem
            {
                Name = "Loading...",
                FullPath = "/__placeholder__",
                EntryType = RemoteEntryType.Unknown
            });

            RootNodes.Add(root);

            ProgressValue = 70;

            await EnsureChildrenLoadedAsync(root);
            SelectedTreeNode = root;
            root.IsExpanded = true;
            root.IsSelected = true;

            ProgressValue = 100;
            StatusMessage = "Workspace ready. The UI now has the structure needed for the SSH layer.";
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

    private async Task RefreshSelectedFolderAsync()
    {
        if (SelectedTreeNode is null)
            return;

        SelectedTreeNode.IsLoaded = false;
        await EnsureChildrenLoadedAsync(SelectedTreeNode, forceReload: true);
        await PopulateCurrentFolderAsync(SelectedTreeNode);
        StatusMessage = $"Refreshed {SelectedTreeNode.FullPath}";
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
        CurrentFolderItems.Clear();

        if (!node.IsDirectory)
            return;

        var children = await _mibConnectionService.GetChildrenAsync(node.FullPath);

        foreach (var child in children)
            CurrentFolderItems.Add(child);

        ApplySort();
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
        MessageBox.Show(
            $"{actionName} will be connected once the SSH transport layer is implemented.\n\nTarget: {target}",
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
