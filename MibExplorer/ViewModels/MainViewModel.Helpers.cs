using MibExplorer.Models;
using MibExplorer.Views.Dialogs;
using System.IO;
using System.Windows;

namespace MibExplorer.ViewModels;

public sealed partial class MainViewModel
{
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
                StatusMessage = "Workspace ready. Connect to the MIB to load the real remote filesystem.";
            }

            RefreshCommands();
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

    private async Task EnsureWorkspaceDirectoriesAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(WorkspaceFolder))
                await Task.Run(() => Directory.CreateDirectory(WorkspaceFolder));

            if (!string.IsNullOrWhiteSpace(PublicKeyExportPath))
            {
                string? publicKeyDirectory = Path.GetDirectoryName(PublicKeyExportPath);
                if (!string.IsNullOrWhiteSpace(publicKeyDirectory))
                    await Task.Run(() => Directory.CreateDirectory(publicKeyDirectory));
            }

            if (!string.IsNullOrWhiteSpace(PrivateKeyPath))
            {
                string? privateKeyDirectory = Path.GetDirectoryName(PrivateKeyPath);
                if (!string.IsNullOrWhiteSpace(privateKeyDirectory))
                    await Task.Run(() => Directory.CreateDirectory(privateKeyDirectory));
            }
        }
        catch
        {
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

    private void SetBusyState(bool isBusy, string progressLabel, double progressValue)
    {
        IsBusy = isBusy;
        ProgressLabel = progressLabel;
        ProgressValue = progressValue;
    }

    private void RefreshCommands()
    {
        _refreshCommand.RaiseCanExecuteChanged();
        _connectionCommand.RaiseCanExecuteChanged();
        _downloadCommand.RaiseCanExecuteChanged();
        _uploadCommand.RaiseCanExecuteChanged();
        _renameCommand.RaiseCanExecuteChanged();
        _deleteCommand.RaiseCanExecuteChanged();
        _extractCommand.RaiseCanExecuteChanged();
        _replaceCommand.RaiseCanExecuteChanged();

        OnPropertyChanged(nameof(SelectedItem));
        OnPropertyChanged(nameof(SelectedItemVisibility));
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
