using MibExplorer.Models;
using MibExplorer.Views.Dialogs;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace MibExplorer.ViewModels;

public sealed partial class MainViewModel
{
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
}
