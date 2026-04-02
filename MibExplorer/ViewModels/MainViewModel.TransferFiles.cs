using MibExplorer.Models;
using MibExplorer.Views.Dialogs;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace MibExplorer.ViewModels;

public sealed partial class MainViewModel
{
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
            bool remoteExists = await _mibConnectionService.RemotePathExistsAsync(remotePath);

            string operationName = remoteExists ? "Replacing" : "Uploading";
            string finalStatusVerb = remoteExists ? "Replaced" : "Uploaded";

            SetBusyState(true, $"{operationName} {fileName}...", 0);
            StatusMessage = $"Starting {operationName.ToLowerInvariant()} of {fileName}...";

            var progress = new Progress<FileTransferProgressInfo>(info =>
            {
                if (info.HasKnownLength)
                {
                    ProgressValue = info.Percentage;
                    ProgressLabel = $"{info.Percentage:0}%";
                    StatusMessage =
                        $"{operationName} {fileName}... " +
                        $"{FormatTransferSize(info.BytesTransferred)} / {FormatTransferSize(info.TotalBytes!.Value)}";
                }
                else
                {
                    ProgressLabel = "Working...";
                    StatusMessage = $"{operationName} {fileName}...";
                }
            });

            if (remoteExists)
            {
                await _mibConnectionService.ReplaceFileAsync(localPath, remotePath, progress);
            }
            else
            {
                await _mibConnectionService.UploadFileAsync(localPath, remotePath, progress);
            }

            ProgressValue = 100;
            ProgressLabel = "100%";
            StatusMessage = $"{finalStatusVerb} {localPath} to {remotePath}";

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

    private async Task ReplaceSelectedFileAsync()
    {
        if (!_mibConnectionService.IsConnected)
        {
            StatusMessage = "Not connected. Test the SSH connection first.";
            return;
        }

        if (SelectedItem is null || SelectedItem.IsDirectory)
        {
            StatusMessage = "Select a file to replace.";
            return;
        }

        var selectedFile = SelectedItem;

        if (!_mibConnectionService.CanWriteToPath(selectedFile.FullPath))
        {
            StatusMessage = "Path is not writable.";

            AppMessageBox.Show(
                $"This path cannot be modified.\n\n{selectedFile.FullPath}",
                "Not allowed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var openDialog = new OpenFileDialog
        {
            Title = $"Select replacement file for {selectedFile.Name}",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            Filter = "All files (*.*)|*.*"
        };

        if (openDialog.ShowDialog() != true)
        {
            StatusMessage = "Replace cancelled.";
            return;
        }

        string localPath = openDialog.FileName;

        try
        {
            SetBusyState(true, $"Replacing {selectedFile.Name}...", 0);
            StatusMessage = $"Starting replace of {selectedFile.Name}...";

            var progress = new Progress<FileTransferProgressInfo>(info =>
            {
                if (info.HasKnownLength)
                {
                    ProgressValue = info.Percentage;
                    ProgressLabel = $"{info.Percentage:0}%";
                    StatusMessage =
                        $"Replacing {selectedFile.Name}... " +
                        $"{FormatTransferSize(info.BytesTransferred)} / {FormatTransferSize(info.TotalBytes!.Value)}";
                }
                else
                {
                    ProgressLabel = "Working...";
                    StatusMessage = $"Replacing {selectedFile.Name}...";
                }
            });

            await _mibConnectionService.ReplaceFileAsync(localPath, selectedFile.FullPath, progress);

            ProgressValue = 100;
            ProgressLabel = "100%";
            StatusMessage = $"Replaced {selectedFile.FullPath} with {localPath}";

            if (SelectedTreeNode is not null)
            {
                await EnsureChildrenLoadedAsync(SelectedTreeNode, forceReload: true);
                await PopulateCurrentFolderAsync(SelectedTreeNode);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Replace failed: {ex.Message}";
            AppMessageBox.Show(
                $"Failed to replace file.\n\n{ex.Message}",
                "MibExplorer",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false, "Ready", 0);
        }
    }
}
