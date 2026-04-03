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
                var result = AppMessageBox.Show(
                                $"The file already exists:\n\n{remotePath}\n\n" +
                                "Do you want to replace it?",
                                "Replace file",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    StatusMessage = "Upload cancelled.";
                    return;
                }

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

    private async Task UploadFolderAsync()
    {
        if (!_mibConnectionService.IsConnected)
        {
            StatusMessage = "Not connected.";
            return;
        }

        if (SelectedTreeNode is null || !SelectedTreeNode.IsDirectory)
        {
            StatusMessage = "Select a target folder.";
            return;
        }

        var dialog = new Microsoft.Win32.OpenFolderDialog();
        if (dialog.ShowDialog() != true)
            return;

        string localRoot = dialog.FolderName;
        string folderName = await TryGetMappedRootFolderNameAsync(localRoot)
            ?? new DirectoryInfo(localRoot).Name;

        string remoteParent = SelectedTreeNode.FullPath.TrimEnd('/');
        string remoteTarget = remoteParent + "/" + folderName;

        try
        {
            SetBusyState(true, "Preparing upload...", 0);

            bool targetExists = await _mibConnectionService.RemotePathExistsAsync(remoteTarget);

            if (targetExists)
            {
                var result = AppMessageBox.Show(
                    $"The folder already exists:\n\n{remoteTarget}\n\n" +
                    "Replacing it will overwrite ALL its content.\n\n" +
                    "Are you sure you want to continue?",
                    "Replace folder",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    StatusMessage = "Upload cancelled.";
                    return;
                }

                await ReplaceFolderSafeAsync(localRoot, remoteTarget);
            }
            else
            {
                await UploadFolderInternalAsync(localRoot, remoteTarget);
            }

            StatusMessage = "Folder upload completed.";
            await RefreshSelectedFolderAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Upload failed: {ex.Message}";
        }
        finally
        {
            SetBusyState(false, "Ready", 0);
        }
    }

    private async Task UploadFolderInternalAsync(string localRoot, string remoteTarget)
    {
        await _mibConnectionService.RunWritableOperationAsync(remoteTarget, async _ =>
        {
            await UploadFolderCoreAsync(localRoot, remoteTarget);
        });
    }

    private async Task UploadFolderCoreAsync(string localRoot, string remoteTarget)
    {
        await EnsureRemoteDirectoryExistsAsync(remoteTarget);

        Dictionary<string, ExtractMapEntry> replayEntries = await LoadUploadReplayEntriesAsync(localRoot);

        var allDirectories = Directory.GetDirectories(localRoot, "*", SearchOption.AllDirectories)
            .Select(directory => Path.GetRelativePath(localRoot, directory).Replace('\\', '/'))
            .OrderBy(path => path.Count(c => c == '/'))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (string directory in allDirectories)
        {
            string remoteRelativeDirectory = ResolveUploadRemoteRelativePath(directory, replayEntries);
            string remoteDirectory = remoteTarget + "/" + remoteRelativeDirectory;
            await EnsureRemoteDirectoryExistsAsync(remoteDirectory);
        }

        var allFiles = Directory.GetFiles(localRoot, "*", SearchOption.AllDirectories)
            .Where(file => !Path.GetFileName(file).Equals(".mibexplorer-map.json", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        int total = allFiles.Length;
        int index = 0;

        foreach (var file in allFiles)
        {
            index++;

            string relative = Path.GetRelativePath(localRoot, file).Replace('\\', '/');
            string remoteRelativePath = ResolveUploadRemoteRelativePath(relative, replayEntries);
            string remotePath = remoteTarget + "/" + remoteRelativePath;

            string remoteDir = Path.GetDirectoryName(remotePath)!.Replace('\\', '/');

            await EnsureRemoteDirectoryExistsAsync(remoteDir);
            await _mibConnectionService.UploadFileWithoutMountAsync(file, remotePath);

            ProgressValue = total == 0 ? 100 : (double)index / total * 100;
            ProgressLabel = $"Uploading {index}/{total}";
        }
    }

    private async Task ReplaceFolderSafeAsync(string localRoot, string remoteTarget)
    {
        string tempPath = remoteTarget + ".__mibexplorer_tmp__";
        string backupPath = remoteTarget + ".__mibexplorer_bak__";

        await _mibConnectionService.RunWritableOperationAsync(remoteTarget, async _ =>
        {
            // 1. Clean temp if exists
            if (await _mibConnectionService.RemotePathExistsAsync(tempPath))
                await _mibConnectionService.DeletePathWithoutMountAsync(tempPath);

            // 2. Upload into temp
            await UploadFolderCoreAsync(localRoot, tempPath);

            // 3. Backup existing
            if (await _mibConnectionService.RemotePathExistsAsync(remoteTarget))
                await _mibConnectionService.MovePathWithoutMountAsync(remoteTarget, backupPath);

            // 4. Move temp -> target
            await _mibConnectionService.MovePathWithoutMountAsync(tempPath, remoteTarget);

            // 5. Cleanup backup
            if (await _mibConnectionService.RemotePathExistsAsync(backupPath))
                await _mibConnectionService.DeletePathWithoutMountAsync(backupPath);
        });
    }

    private async Task EnsureRemoteDirectoryExistsAsync(string remoteDirectory)
    {
        if (string.IsNullOrWhiteSpace(remoteDirectory))
            return;

        await _mibConnectionService.CreateDirectoryWithoutMountAsync(remoteDirectory);
    }
}
