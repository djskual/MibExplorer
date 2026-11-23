using MibExplorer.Core;
using MibExplorer.Models;
using System.Linq;
using System.Windows.Data;

namespace MibExplorer.ViewModels;

public sealed partial class MainViewModel
{
    // Share an in-flight directory read between selection and expansion.
    // Entries are removed at completion; this is not a persistent folder cache.
    private readonly Dictionary<RemoteExplorerItem, Task<IReadOnlyList<RemoteExplorerItem>>> _pendingTreeReads = new();

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

        var node = SelectedTreeNode;
        try
        {
            SetBusyState(true, $"Refreshing {node.FullPath}...", 40);
            CurrentFolderItems.Clear(); // No stale content on refresh errors.

            node.IsLoaded = false;
            var loadedChildren = await EnsureChildrenLoadedAsync(node, forceReload: true);
            if (!ReferenceEquals(SelectedTreeNode, node))
                return;
            await PopulateCurrentFolderAsync(node, loadedChildren);

            StatusMessage = $"Refreshed {node.FullPath}" +
                (node.FullPath != node.BrowsingPath
                    ? $" (target {node.BrowsingPath})" : string.Empty);
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
        // Never show MMX contents under an RCC path while a new listing is
        // loading, or if that listing fails. The selection callback is async.
        CurrentFolderItems.Clear();
        CurrentFolderItemsView.Refresh();
        try
        {
            var loadedChildren = await EnsureChildrenLoadedAsync(node);
            if (!ReferenceEquals(SelectedTreeNode, node))
                return; // A newer selection superseded this asynchronous load.

            await PopulateCurrentFolderAsync(node, loadedChildren);
            if (!ReferenceEquals(SelectedTreeNode, node))
                return;

            UpdateBreadcrumbs(node.FullPath);
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(SelectedTreeNode, node))
            {
                CurrentFolderItems.Clear();
                StatusMessage = $"Failed to load {node.FullPath}: {ex.Message}";
            }
        }
        finally
        {
            if (ReferenceEquals(SelectedTreeNode, node))
            {
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
        }
    }

    private async Task PopulateCurrentFolderAsync(
        RemoteExplorerItem node, IReadOnlyList<RemoteExplorerItem>? loadedChildren = null)
    {
        if (!node.IsNavigable)
            return;

        if (!_mibConnectionService.IsConnected)
            return;

        // A symbolic link must be browsed via its node-qualified destination.
        // The SSH connection is on MMX, which would otherwise resolve RCC's
        // absolute /tmp -> /dev/shmem link in the *MMX* filesystem.
        // A newly loaded branch already fetched this listing. Reuse it for
        // the file pane instead of immediately running the same SSH command.
        // Selecting a previously loaded branch still refreshes the file pane.
        var children = loadedChildren ?? await _mibConnectionService.GetChildrenAsync(node.BrowsingPath);
        if (!ReferenceEquals(SelectedTreeNode, node))
            return;

        SelectedListItem = null;
        CurrentFolderItems.Clear();
        foreach (var child in children)
            CurrentFolderItems.Add(child);

        ApplySort();
        CurrentFolderItemsView.Refresh();
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

    private async Task<IReadOnlyList<RemoteExplorerItem>?> EnsureChildrenLoadedAsync(
        RemoteExplorerItem node, bool forceReload = false)
    {
        if (!node.IsNavigable || !_mibConnectionService.IsConnected)
            return null;

        // Expanded and Selected events can arrive together. They must await
        // one remote read, not issue two concurrent listings of the same path.
        if (_pendingTreeReads.TryGetValue(node, out var pending))
            return await pending;

        if (node.IsLoaded && !forceReload)
            return null;

        node.IsLoading = true;
        try
        {
            var request = _mibConnectionService.GetChildrenAsync(node.BrowsingPath);
            _pendingTreeReads.Add(node, request);
            var children = await request;
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
            return children;
        }
        catch
        {
            node.IsLoaded = false;
            node.Children.Clear(); // Do not leave stale children after a failed Qnet traversal.
            throw;
        }
        finally
        {
            _pendingTreeReads.Remove(node);
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

        // /net is the visible tree root; do not add a separate "/" breadcrumb.
        if (path != "/net" && !path.StartsWith("/net/", StringComparison.Ordinal))
            Breadcrumbs.Add("/");

        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            Breadcrumbs.Add(segment);
    }

    public async Task EnsureTreeNodeChildrenLoadedAsync(RemoteExplorerItem node)
    {
        // Called by an async-void WPF Expanded event: do not let SSH failures
        // escape to the Dispatcher and crash the entire explorer.
        try
        {
            await EnsureChildrenLoadedAsync(node);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to expand {node.FullPath}: {ex.Message}";
        }
    }
}
