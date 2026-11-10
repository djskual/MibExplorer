using System.Windows;
using MibExplorer.Models;
using MibExplorer.Views.Dialogs;

namespace MibExplorer.ViewModels;

public sealed partial class MainViewModel
{
    private async Task RenameSelectedItemAsync()
    {
        if (!_mibConnectionService.IsConnected)
        {
            StatusMessage = "Not connected. Test the SSH connection first.";
            return;
        }

        if (SelectedItem is null)
        {
            StatusMessage = "Select a file or folder to rename.";
            return;
        }

        var item = SelectedItem;

        if (!await _mibConnectionService.CanWriteToPathAsync(item.FullPath))
        {
            StatusMessage = "Path is not writable.";

            AppMessageBox.Show(
                $"This path cannot be modified.\n\n{item.FullPath}",
                "Not allowed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var dialog = new RenameItemWindow(item.Name)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            StatusMessage = "Rename cancelled.";
            return;
        }

        string newName = dialog.ResultName;

        try
        {
            SetBusyState(true, $"Renaming {item.Name}...", 0);

            await _mibConnectionService.RenamePathAsync(item.FullPath, newName);

            StatusMessage = $"Renamed {item.FullPath} to {newName}";

            try
            {
                await RefreshAfterEntryMovedOrRemovedAsync(item.FullPath);
            }
            catch (Exception refreshError)
            {
                // The remote rename succeeded; a failed UI refresh must not report
                // the rename itself as failed.
                StatusMessage = $"Renamed {item.FullPath} to {newName}, but explorer refresh failed: {refreshError.Message}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Rename failed: {ex.Message}";
            AppMessageBox.Show(
                $"Failed to rename item.\n\n{ex.Message}",
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

        if (SelectedItem is null)
        {
            StatusMessage = "Select an item to delete.";
            return;
        }

        var item = SelectedItem;

        if (!await _mibConnectionService.CanWriteToPathAsync(item.FullPath))
        {
            StatusMessage = "Path is not writable.";

            AppMessageBox.Show(
                $"This path cannot be modified.\n\n{item.FullPath}",
                "Not allowed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        string itemType = item.IsDirectory ? "folder" : "file";

        var result = AppMessageBox.Show(
            $"Delete {itemType}?\n\n{item.FullPath}",
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
            SetBusyState(true, $"Deleting {item.Name}...", 0);

            await _mibConnectionService.DeleteFileAsync(item.FullPath);

            StatusMessage = $"Deleted {item.FullPath}";

            try
            {
                await RefreshAfterEntryMovedOrRemovedAsync(item.FullPath);
            }
            catch (Exception refreshError)
            {
                // Deletion has already completed on QNX. Avoid a misleading
                // "Failed to delete item" when only the explorer refresh failed.
                StatusMessage = $"Deleted {item.FullPath}, but explorer refresh failed: {refreshError.Message}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete failed: {ex.Message}";
            AppMessageBox.Show(
                $"Failed to delete item.\n\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false, "Ready", 0);
        }
    }

    // A renamed/deleted directory or directory symlink can be the currently
    // selected tree node. After the operation, its old path no longer exists:
    // refresh its surviving parent, not the removed/renamed node.
    private async Task RefreshAfterEntryMovedOrRemovedAsync(string oldPath)
    {
        RemoteExplorerItem? folder = SelectedTreeNode;
        if (folder is null)
            return;

        string affectedPath = oldPath.TrimEnd('/');
        string selectedPath = folder.FullPath.TrimEnd('/');
        bool selectionInvalidated =
            string.Equals(selectedPath, affectedPath, StringComparison.Ordinal) ||
            selectedPath.StartsWith(affectedPath + "/", StringComparison.Ordinal);

        if (selectionInvalidated)
        {
            int lastSlash = affectedPath.LastIndexOf('/');
            string parentPath = lastSlash <= 0 ? "/" : affectedPath[..lastSlash];

            RemoteExplorerItem? parentNode = FindLoadedTreeNode(RootNodes, parentPath)
                ?? FindLoadedTreeNode(RootNodes, "/");

            if (parentNode is null)
                throw new InvalidOperationException("The explorer root is not available for refresh.");

            // Move the UI selection before refreshing the parent tree children,
            // which removes the now-invalid selected node from the tree.
            folder.IsSelected = false;
            SelectedListItem = null;
            SelectedTreeNode = parentNode;
            parentNode.IsExpanded = true;
            parentNode.IsSelected = true;
            folder = parentNode;
        }

        await EnsureChildrenLoadedAsync(folder, forceReload: true);
        await PopulateCurrentFolderAsync(folder);
        UpdateBreadcrumbs(folder.FullPath);
    }

    private static RemoteExplorerItem? FindLoadedTreeNode(
        IEnumerable<RemoteExplorerItem> nodes,
        string path)
    {
        foreach (RemoteExplorerItem node in nodes)
        {
            if (string.Equals(node.FullPath, path, StringComparison.Ordinal))
                return node;

            RemoteExplorerItem? found = FindLoadedTreeNode(node.Children, path);
            if (found is not null)
                return found;
        }

        return null;
    }
}
