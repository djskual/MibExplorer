using MibExplorer.Core;
using MibExplorer.Models;
using System.Linq;
using System.Windows.Data;

namespace MibExplorer.ViewModels;

public sealed partial class MainViewModel
{
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
        if (!node.IsNavigable)
            return;

        if (!_mibConnectionService.IsConnected)
            return;

        try
        {
            var children = await _mibConnectionService.GetChildrenAsync(node.FullPath);

            SelectedListItem = null;

            CurrentFolderItems.Clear();

            foreach (var child in children)
                CurrentFolderItems.Add(child);

            ApplySort();
            CurrentFolderItemsView.Refresh();
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
        if (!node.IsNavigable)
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
                .Where(x => x.IsNavigable)
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

    public async Task EnsureTreeNodeChildrenLoadedAsync(RemoteExplorerItem node)
    {
        await EnsureChildrenLoadedAsync(node);
    }
}
