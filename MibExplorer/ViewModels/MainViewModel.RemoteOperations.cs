using System.Windows;
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

        if (!_mibConnectionService.CanWriteToPath(item.FullPath))
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

            if (SelectedTreeNode is not null)
            {
                await EnsureChildrenLoadedAsync(SelectedTreeNode, forceReload: true);
                await PopulateCurrentFolderAsync(SelectedTreeNode);
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

        if (!_mibConnectionService.CanWriteToPath(item.FullPath))
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
}
