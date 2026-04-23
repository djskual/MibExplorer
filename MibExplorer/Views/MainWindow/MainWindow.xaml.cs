using MibExplorer.Models;
using MibExplorer.Services;
using MibExplorer.Services.Design;
using MibExplorer.Services.Scripting;
using MibExplorer.Settings;
using MibExplorer.ViewModels;
using MibExplorer.Views.Dialogs;
using MibExplorer.Views.Scripting;
using Renci.SshNet.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MibExplorer.Views.MainWindow;

public partial class MainWindow : Window
{
    private ShellConsoleWindow? _shellConsoleWindow;
    private ScriptRunnerWindow? _scriptRunnerWindow;
    private MainViewModel ViewModel => (MainViewModel)DataContext;
    private bool _isSortingFromHeader;
    private string? _pendingSortKey;
    private const string SshKeysFolderName = "Keys";
    private const string SshPrivateKeyFileName = "id_rsa";
    private const double FineScrollPixelsPerDetent = 26.0;

    private readonly Dictionary<string, FileEditorWindow> _openFileEditors = new(StringComparer.Ordinal);
    private static readonly HashSet<string> BlockedEditorExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip",
        ".7z",
        ".rar",
        ".tar",
        ".gz",
        ".bz2",

        ".bin",
        ".img",
        ".iso",
        ".dat",
        ".pak",
        ".sig",

        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".gif",
        ".ico",

        ".mcf",
        ".gca",
        ".res"
    };
    public MainWindow()
    {
        InitializeComponent();

        bool useDesignMode = false;

        DataContext = useDesignMode
            ? new MainViewModel()
            : new MainViewModel(new SshMibConnectionService());

        Loaded += (_, _) => UpdateSortHeaderVisuals();

        CurrentFolderList.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(CurrentFolderHeader_PreviewMouseLeftButtonDown), true);

        ApplyWindowPlacementFromSettings();

        Loaded += async (_, __) =>
        {
            UpdateWindowTitle();

            if (AppSettingsStore.Current.AutoCheckUpdatesOnStartup)
                await CheckForUpdatesAsync(silentIfUpToDate: true, silentOnError: true);
        };

        Closing += (_, __) =>
        {
            PersistWindowPlacementToSettings();

            AppSettingsStore.Save(UpdateSettings(settings =>
            {
                settings.LastHost = ViewModel.Host;
                settings.LastPort = ViewModel.Port;
                settings.LastUsername = ViewModel.Username;
                settings.UsePrivateKey = ViewModel.UsePrivateKey;
                settings.LastPrivateKeyPath = ViewModel.PrivateKeyPath;
                settings.LastWorkspaceFolder = ViewModel.WorkspaceFolder;
                settings.LastPublicKeyExportPath = ViewModel.PublicKeyExportPath;
            }));
        };
    }

    private void ScriptCenterMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenScriptRunnerWindow();
    }

    private void OpenScriptRunnerWindow()
    {
        if (_scriptRunnerWindow is not null)
        {
            if (_scriptRunnerWindow.WindowState == WindowState.Minimized)
                _scriptRunnerWindow.WindowState = WindowState.Normal;

            _scriptRunnerWindow.Show();
            _scriptRunnerWindow.Activate();
            _scriptRunnerWindow.Focus();
            return;
        }

        IScriptCatalogService catalogService;
        IScriptExecutionService executionService;
        IOfficialScriptUpdateService officialScriptUpdateService;

        if (ViewModel.ConnectionService is DesignMibConnectionService)
        {
            catalogService = new DesignScriptCatalogService();
            executionService = new DesignScriptExecutionService();
            officialScriptUpdateService = new DesignOfficialScriptUpdateService();
        }
        else
        {
            catalogService = new ScriptCatalogService();
            executionService = new ScriptExecutionService(ViewModel.ConnectionService);
            officialScriptUpdateService = new OfficialScriptUpdateService();
        }

        var scriptRunnerViewModel = new ScriptRunnerViewModel(
            ViewModel.ConnectionService,
            catalogService,
            executionService,
            officialScriptUpdateService);

        var window = new ScriptRunnerWindow
        {
            DataContext = scriptRunnerViewModel,
            Width = 1100,
            Height = 710
        };

        double left = Left + Math.Max(0, (ActualWidth - window.Width) / 2);
        double top = Top + Math.Max(0, (ActualHeight - window.Height) / 2);

        window.Left = left;
        window.Top = top;

        window.Closed += (_, _) => _scriptRunnerWindow = null;

        _scriptRunnerWindow = window;

        window.Show();
        window.Activate();
    }

    private void RemoteShellConsoleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenRemoteShellWindow();
    }

    private void OpenRemoteShellWindow()
    {
        if (_shellConsoleWindow is not null)
        {
            if (_shellConsoleWindow.WindowState == WindowState.Minimized)
                _shellConsoleWindow.WindowState = WindowState.Normal;

            _shellConsoleWindow.Show();
            _shellConsoleWindow.Activate();
            _shellConsoleWindow.Focus();
            return;
        }

        if (DataContext is not MainViewModel mainViewModel)
        {
            AppMessageBox.Show(this,
                "Main view model is unavailable.",
                "Remote Shell Console",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!mainViewModel.ConnectionService.IsConnected)
        {
            AppMessageBox.Show(this,
                "You must connect to the MIB before opening the shell console.",
                "Remote Shell Console",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var viewModel = new ShellConsoleViewModel(mainViewModel.ConnectionService);
        var window = new ShellConsoleWindow(viewModel);

        window.Width = 980;
        window.Height = 650;

        double left = Left + Math.Max(0, (ActualWidth - window.Width) / 2);
        double top = Top + Math.Max(0, (ActualHeight - window.Height) / 2);

        window.Left = left;
        window.Top = top;

        window.Closed += ShellConsoleWindow_Closed;

        _shellConsoleWindow = window;

        window.Show();
        window.Activate();
    }

    private async Task OpenRemoteShellWindowWithPathAsync(string remotePath)
    {
        if (_shellConsoleWindow is null)
        {
            OpenRemoteShellWindow();
        }
        else
        {
            if (_shellConsoleWindow.WindowState == WindowState.Minimized)
                _shellConsoleWindow.WindowState = WindowState.Normal;

            _shellConsoleWindow.Show();
            _shellConsoleWindow.Activate();
            _shellConsoleWindow.Focus();
        }

        if (_shellConsoleWindow?.DataContext is not ShellConsoleViewModel vm)
            return;

        string safePath = remotePath.Replace("\"", "\\\"");

        for (int i = 0; i < 20; i++)
        {
            if (vm.IsConnected)
                break;

            await Task.Delay(100);
        }

        if (!vm.IsConnected)
            return;

        await vm.SendCommandDirectAsync($"cd \"{safePath}\"");
    }

    private static string GetShellTargetPath(RemoteExplorerItem item)
    {
        if (item.IsDirectory || item.IsNavigable)
            return item.FullPath;

        string path = item.FullPath;
        int lastSlash = path.LastIndexOf('/');

        if (lastSlash <= 0)
            return "/";

        return path.Substring(0, lastSlash);
    }

    private async void OpenInShellMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var item = ViewModel.SelectedItem ?? ViewModel.SelectedTreeNode;
        if (item is null)
            return;

        string targetPath = GetShellTargetPath(item);
        await OpenRemoteShellWindowWithPathAsync(targetPath);
    }

    private void ShellConsoleWindow_Closed(object? sender, EventArgs e)
    {
        if (_shellConsoleWindow is not null)
        {
            _shellConsoleWindow.Closed -= ShellConsoleWindow_Closed;
            _shellConsoleWindow = null;
        }
    }

    private static bool IsBlockedForTextEditor(string remotePath)
    {
        string extension = Path.GetExtension(remotePath);

        if (string.IsNullOrWhiteSpace(extension))
            return false;

        return BlockedEditorExtensions.Contains(extension);
    }

    private bool TryGetFileEditorMode(string remotePath, out bool isReadOnly)
    {
        isReadOnly = false;

        if (IsBlockedForTextEditor(remotePath))
        {
            AppMessageBox.Show(this,
                "This file type is not supported by the remote text editor.",
                "Remote File Editor",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        if (!ViewModel.ConnectionService.CanWriteToPath(remotePath))
            isReadOnly = true;

        return true;
    }

    private void EditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedListItem is not RemoteExplorerItem item)
            return;

        if (item.IsDirectory)
            return;

        OpenFileEditorWindow(item.FullPath);
    }

    private void OpenFileEditorWindow(string remotePath)
    {
        if (!TryGetFileEditorMode(remotePath, out bool isReadOnly))
            return;

        if (_openFileEditors.TryGetValue(remotePath, out FileEditorWindow? existingWindow))
        {
            if (existingWindow.WindowState == WindowState.Minimized)
                existingWindow.WindowState = WindowState.Normal;

            existingWindow.Show();
            existingWindow.Activate();
            existingWindow.Focus();
            return;
        }

        var viewModel = new FileEditorViewModel(ViewModel.ConnectionService, remotePath, isReadOnly);
        var window = new FileEditorWindow(viewModel);

        double left = Left + Math.Max(0, (ActualWidth - window.Width) / 2);
        double top = Top + Math.Max(0, (ActualHeight - window.Height) / 2);

        window.Left = left;
        window.Top = top;

        _openFileEditors[remotePath] = window;

        window.Closed += (_, _) =>
        {
            _openFileEditors.Remove(remotePath);
        };

        window.Show();
        window.Activate();
    }

    private void CreateMibSshSdUpdate_Click(object sender, RoutedEventArgs e)
    {
        var message =
            @"This tool creates a dedicated SD update package
            for your MIB.

            Planned content:

            - RSA public key (id_rsa.pub)
            - Embedded SSHD payload
            - Final SWDL script
            - SSH install script
            - ZIP generated next to the app

            User flow:

            1. Click OK to build the package
            2. Copy ZIP content to SD card
            3. Insert SD into MIB and run update
            4. After reboot, connect to MIB Wi-Fi
            5. Read Default Gateway on your PC
            6. Use it as SSH IP in MibExplorer

            Notes:

            - id_rsa is stored in the Keys folder
            - Public key is generated for the package
            - SD update embeds the public key
            - SWDL format, encoding and hashes OK";

        var result = AppMessageBox.Show(
            message,
            "Create MIB SSH SD Update",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Information);

        if (result != MessageBoxResult.OK)
            return;

        try
        {
            var builder = new SdUpdatePackageBuilder();
            string packagePath = builder.BuildPackage();

            AppMessageBox.Show(
                $"SD update package created successfully:{Environment.NewLine}{Environment.NewLine}{packagePath}{Environment.NewLine}{Environment.NewLine}The ZIP file was created next to the application executable.",
                "MIB SSH SD Update",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppMessageBox.Show(
                $"Failed to create SD update package.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "MIB SSH SD Update",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CreateMibSshSdUninstall_Click(object sender, RoutedEventArgs e)
    {
        var message =
            @"This tool creates a dedicated SD uninstall package
for your MIB.

Planned content:

- SWDL uninstall trigger package
- Dummy trigger file
- Final SWDL script
- SSH uninstall script
- Boot finisher script
- ZIP generated next to the app

User flow:

1. Click OK to build the uninstall package
2. Copy ZIP content to SD card
3. Insert SD into MIB and run update
4. Reboot the unit
5. Let finish_ssh_boot.sh complete the cleanup after boot
6. Check logs on the same SD card

Notes:

- This package removes the SSH payload
- It restores backed up system config when available
- startup.sh hook is intentionally kept
- Logging is written to the same SD used for SWDL
- SWDL format, encoding and hashes OK";

        var result = AppMessageBox.Show(
            message,
            "Create MIB SSH SD Uninstall",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Information);

        if (result != MessageBoxResult.OK)
            return;

        try
        {
            var builder = new SdUninstallPackageBuilder();
            string packagePath = builder.BuildPackage();

            AppMessageBox.Show(
                $"SD uninstall package created successfully:{Environment.NewLine}{Environment.NewLine}{packagePath}{Environment.NewLine}{Environment.NewLine}The ZIP file was created next to the application executable.",
                "MIB SSH SD Uninstall",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppMessageBox.Show(
                $"Failed to create SD uninstall package.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "MIB SSH SD Uninstall",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void UninstallSshFromMib_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsConnectedToMib)
        {
            AppMessageBox.Show(
                this,
                "You must be connected to the MIB over SSH before using direct uninstall.",
                "Uninstall SSH from MIB",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var confirmResult = AppMessageBox.Show(
            this,
            "Direct SSH uninstall\n\n" +
            "This action will remove the SSH installation from the connected MIB without using an SD package.\n\n" +
            "What will be removed/restored:\n" +
            "- SSH payload\n" +
            "- /root/.ssh and defensive cleanup of /root/.sshd\n" +
            "- authorized_keys\n" +
            "- /root/scp\n" +
            "- /root/.profile\n" +
            "- inetd.conf restored from backup when available\n" +
            "- firewall pf*.conf restored from backup when available\n" +
            "- MibExplorer.info removed from SWDL FileCopyInfo\n\n" +
            "Important:\n" +
            "- startup.sh hook will be kept intentionally\n" +
            "- A reboot is recommended after uninstall\n" +
            "- The current SSH session may stop after cleanup\n\n" +
            "Continue?",
            "Uninstall SSH from MIB",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (confirmResult != MessageBoxResult.OK)
            return;

        try
        {
            string command =
                "export PATH=/proc/boot:/bin:/usr/bin:/usr/sbin:/sbin:/mnt/app/media/gracenote/bin:/mnt/app/armle/bin:/mnt/app/armle/sbin:/mnt/app/armle/usr/bin:/mnt/app/armle/usr/sbin:$PATH; " +
                "SSH_DIR=/net/mmx/mnt/app/eso/hmi/engdefs/scripts/ssh; " +
                "ROOT_HOME=/mnt/app/root; " +
                "ROOT_SSH_DIR=$ROOT_HOME/.ssh; " +
                "ROOT_SSHD_DIR=$ROOT_HOME/.sshd; " +
                "ROOT_AUTH_KEYS=$ROOT_SSH_DIR/authorized_keys; " +
                "ROOT_PROFILE=$ROOT_HOME/.profile; " +
                "ROOT_SCP=$ROOT_HOME/scp; " +
                "INETD_CONF=/mnt/system/etc/inetd.conf; " +
                "INETD_CONF_BU=/mnt/system/etc/inetd.conf.bu; " +
                "EFS_PERSIST=/net/rcc/mnt/efs-persist; " +
                "FILECOPYINFO_DIR=$EFS_PERSIST/SWDL/FileCopyInfo; " +
                "MIBEXPLORER_INFO_FILE=$FILECOPYINFO_DIR/MibExplorer.info; " +
                "mount -uw /mnt/system; " +
                "mount -uw /mnt/app; " +
                "mount -uw $EFS_PERSIST; " +
                "if [ -f \"$INETD_CONF_BU\" ]; then " +
                "  mv -f \"$INETD_CONF_BU\" \"$INETD_CONF\"; " +
                "elif [ -f \"$INETD_CONF\" ]; then " +
                "  cp -p \"$INETD_CONF\" \"$INETD_CONF.mibexplorer.tmp\" && " +
                "  sed -i -r 's:^.*start_sshd.*\\n*::p' \"$INETD_CONF.mibexplorer.tmp\" && " +
                "  cp -p \"$INETD_CONF.mibexplorer.tmp\" \"$INETD_CONF\"; " +
                "  rm -f \"$INETD_CONF.mibexplorer.tmp\"; " +
                "fi; " +
                "for PF in /mnt/system/etc/pf*.conf; do " +
                "  if [ -f \"${PF}.bu\" ]; then mv -f \"${PF}.bu\" \"$PF\"; fi; " +
                "done; " +
                "if [ -f /mnt/system/etc/pf.mlan0.conf ]; then /mnt/app/armle/sbin/pfctl -F all -f /mnt/system/etc/pf.mlan0.conf >/dev/null 2>&1; fi; " +
                "slay -v inetd >/dev/null 2>&1; " +
                "sleep 1; " +
                "inetd >/dev/null 2>&1; " +
                "rm -f \"$ROOT_AUTH_KEYS\"; " +
                "rm -f \"$ROOT_PROFILE\"; " +
                "rm -f \"$ROOT_SCP\"; " +
                "rm -rf \"$ROOT_SSH_DIR\"; " +
                "rm -rf \"$ROOT_SSHD_DIR\"; " +
                "rm -f /net/mmx/mnt/app/eso/hmi/engdefs/id_rsa.pub; " +
                "rm -f /net/mmx/mnt/app/eso/hmi/engdefs/id_rsa.pub.checksum; " +
                "rm -f /net/mmx/mnt/app/eso/hmi/engdefs/id_rsa.pub.fileinfo; " +
                "rm -f /net/mmx/mnt/app/eso/hmi/engdefs/dummy.txt; " +
                "rm -f /net/mmx/mnt/app/eso/hmi/engdefs/dummy.txt.checksum; " +
                "rm -f /net/mmx/mnt/app/eso/hmi/engdefs/dummy.txt.fileinfo; " +
                "rm -f \"$MIBEXPLORER_INFO_FILE\"; " +
                "rm -rf \"$SSH_DIR\"; " +
                "mount -ur $EFS_PERSIST; " +
                "mount -ur /mnt/app; " +
                "mount -ur /mnt/system; " +
                "echo OK";

            await ViewModel.ConnectionService.ExecuteCommandAsync(command);

            AppMessageBox.Show(
                this,
                "SSH uninstall command completed.\n\n" +
                "What to do next:\n" +
                "- Reboot the MIB\n" +
                "- After reboot, SSH should be removed\n" +
                "- startup.sh hook remains intentionally in place\n\n" +
                "If the current SSH session drops after cleanup, this is expected.",
                "Uninstall SSH from MIB",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppMessageBox.Show(
                this,
                $"Direct SSH uninstall failed.\n\n{ex.Message}",
                "Uninstall SSH from MIB",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void GenerateSshKeys_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string baseFolder = Path.Combine(AppContext.BaseDirectory, SshKeysFolderName);
            Directory.CreateDirectory(baseFolder);

            var service = new SshKeyService();

            var result = await service.GenerateRsaKeyPairAsync(
                baseFolder,
                SshPrivateKeyFileName,
                "mibexplorer");

            AppSettingsStore.Save(UpdateSettings(settings =>
            {
                settings.UsePrivateKey = true;
                settings.LastPrivateKeyPath = result.PrivateKeyPath;
                settings.LastWorkspaceFolder = baseFolder;
                settings.LastPublicKeyExportPath = result.PublicKeyPath;
            }));

            AppMessageBox.Show(
                this,
                "SSH key pair successfully generated.\n\n" +
                "Files created:\n" +
                "- Keys\\id_rsa\n" +
                "- Keys\\id_rsa.pub\n\n" +
                "Important:\n" +
                "- MibExplorer keeps id_rsa for SSH login on this PC\n" +
                "- id_rsa.pub is used when building the SD update package\n" +
                "- the SD update package itself will embed the public key automatically\n\n" +
                "The key folder will now open.",
                "SSH Keys",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{baseFolder}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppMessageBox.Show(
                this,
                $"Failed to generate SSH keys.\n\n{ex.Message}",
                "SSH Keys",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void DetectMibGateway_Click(object sender, RoutedEventArgs e)
    {
        var confirmResult = AppMessageBox.Show(
            this,
            "Automatic MIB IP detection\n\n" +
            "Make sure your PC is connected to the MIB Wi-Fi hotspot before continuing.\n\n" +
            "MibExplorer will inspect the active Wi-Fi network and use its default gateway as the SSH host.\n\n" +
            "Internet access is not required.\n\n" +
            "Continue?",
            "Detect MIB IP",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (confirmResult != MessageBoxResult.OK)
            return;

        try
        {
            var result = await MibNetworkHelper.TryDetectMibGatewayAsync();

            if (result == null)
            {
                AppMessageBox.Show(
                    this,
                    "MibExplorer could not detect the MIB hotspot automatically.\n\n" +
                    "What to check:\n" +
                    "- Connect your PC to the MIB Wi-Fi hotspot\n" +
                    "- Wait a few seconds for Windows to get an IPv4 address\n" +
                    "- Make sure the hotspot network is active\n\n" +
                    "Expected pattern:\n" +
                    "- Local IPv4 is usually in the 10.173.189.x range\n" +
                    "- Default Gateway is usually the MIB IP to use for SSH\n" +
                    "- Internet access is NOT required\n" +
                    "- SSH must be installed and reachable on port 22",
                    "Detect MIB IP",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            ViewModel.Host = result.GatewayIp;
            ViewModel.Port = "22";

            AppMessageBox.Show(
                this,
                "MIB hotspot detected successfully.\n\n" +
                $"Host: {result.GatewayIp}\n" +
                "Port: 22\n" +
                $"Local IPv4: {result.LocalIpv4}\n" +
                $"Interface: {result.InterfaceName}\n" +
                (!string.IsNullOrWhiteSpace(result.DnsSuffix)
                    ? $"DNS suffix: {result.DnsSuffix}\n"
                    : string.Empty),
                "Detect MIB IP",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppMessageBox.Show(
                this,
                $"Failed to detect the MIB IP automatically.\n\n{ex.Message}",
                "Detect MIB IP",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var window = new MibExplorer.Views.Dialogs.SettingsWindow(AppSettingsStore.Current)
        {
            Owner = this
        };

        if (window.ShowDialog() != true)
            return;

        AppSettingsStore.Save(window.ResultSettings);

        ViewModel.Host = window.ResultSettings.LastHost ?? ViewModel.Host;
        ViewModel.Port = window.ResultSettings.LastPort ?? ViewModel.Port;
        ViewModel.Username = window.ResultSettings.LastUsername ?? ViewModel.Username;
        ViewModel.UsePrivateKey = window.ResultSettings.UsePrivateKey;
        ViewModel.PrivateKeyPath = window.ResultSettings.LastPrivateKeyPath
            ?? Path.Combine(AppContext.BaseDirectory, "Keys", "id_rsa");
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(silentIfUpToDate: false, silentOnError: false);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow
        {
            Owner = this
        };

        about.ShowDialog();
    }

    private void ApplyWindowPlacementFromSettings()
    {
        var settings = AppSettingsStore.Current;
        if (!settings.RememberWindowSizeAndPosition)
            return;

        if (settings.WindowWidth is > 0)
            Width = settings.WindowWidth.Value;

        if (settings.WindowHeight is > 0)
            Height = settings.WindowHeight.Value;

        if (settings.WindowLeft.HasValue && settings.WindowTop.HasValue)
        {
            Left = settings.WindowLeft.Value;
            Top = settings.WindowTop.Value;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }
    }

    private void PersistWindowPlacementToSettings()
    {
        var copy = AppSettingsStore.Current.Clone();

        copy.WindowWidth = Width;
        copy.WindowHeight = Height;
        copy.WindowLeft = Left;
        copy.WindowTop = Top;

        AppSettingsStore.Save(copy);
    }

    private static AppSettings UpdateSettings(Action<AppSettings> update)
    {
        var copy = AppSettingsStore.Current.Clone();
        update(copy);
        copy.Normalize();
        return copy;
    }

    private void UpdateWindowTitle()
    {
        var version = GetBuildTag();

        if (string.Equals(version, "unknown", StringComparison.OrdinalIgnoreCase))
            version = "dev";

        Title = $"MibExplorer {version}";
    }

    private static string GetBuildTag()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "git-tag.txt");
            if (!File.Exists(path))
                return "unknown";

            var tag = File.ReadAllText(path).Trim();
            if (string.IsNullOrWhiteSpace(tag))
                return "unknown";

            return tag;
        }
        catch
        {
            return "unknown";
        }
    }

    private readonly struct TagVersion : IComparable<TagVersion>
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string? PreLabel { get; }
        public int PreNumber { get; }
        public bool IsPrerelease => !string.IsNullOrWhiteSpace(PreLabel);

        public TagVersion(int major, int minor, int patch, string? preLabel, int preNumber)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            PreLabel = preLabel;
            PreNumber = preNumber;
        }

        public int CompareTo(TagVersion other)
        {
            var c = Major.CompareTo(other.Major);
            if (c != 0) return c;

            c = Minor.CompareTo(other.Minor);
            if (c != 0) return c;

            c = Patch.CompareTo(other.Patch);
            if (c != 0) return c;

            var thisIsStable = string.IsNullOrWhiteSpace(PreLabel);
            var otherIsStable = string.IsNullOrWhiteSpace(other.PreLabel);

            if (thisIsStable && otherIsStable) return 0;
            if (thisIsStable) return 1;
            if (otherIsStable) return -1;

            c = string.Compare(PreLabel, other.PreLabel, StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;

            return PreNumber.CompareTo(other.PreNumber);
        }
    }

    private static bool TryParseTagVersion(string? tag, out TagVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(tag))
            return false;

        var s = tag.Trim();

        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            s = s[1..];

        string corePart = s;
        string? prePart = null;

        var dashIndex = s.IndexOf('-');
        if (dashIndex >= 0)
        {
            corePart = s[..dashIndex];
            prePart = s[(dashIndex + 1)..];
        }

        var core = corePart.Split('.');
        if (core.Length != 3)
            return false;

        if (!int.TryParse(core[0], out var major)) return false;
        if (!int.TryParse(core[1], out var minor)) return false;
        if (!int.TryParse(core[2], out var patch)) return false;

        string? preLabel = null;
        int preNumber = 0;

        if (!string.IsNullOrWhiteSpace(prePart))
        {
            var pre = prePart.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);

            preLabel = pre[0].Trim();

            if (pre.Length > 1 && !int.TryParse(pre[1], out preNumber))
                preNumber = 0;
        }

        version = new TagVersion(major, minor, patch, preLabel, preNumber);
        return true;
    }

    private async Task CheckForUpdatesAsync(bool silentIfUpToDate, bool silentOnError)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "MibExplorer");
            client.Timeout = TimeSpan.FromSeconds(5);

            var json = await client.GetStringAsync(
                "https://api.github.com/repos/djskual/MibExplorer/tags");

            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            {
                if (!silentIfUpToDate)
                {
                    AppMessageBox.Show(
                        this,
                        "No tag found on GitHub.",
                        "Update check",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                return;
            }

            bool includePrerelease = AppSettingsStore.Current.IncludePrereleaseVersionsInUpdateCheck;

            string? latestTag = null;
            TagVersion? latestVersion = null;

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (!element.TryGetProperty("name", out var nameProp))
                    continue;

                var tagName = nameProp.GetString();
                if (string.IsNullOrWhiteSpace(tagName))
                    continue;

                if (!TryParseTagVersion(tagName, out var parsed))
                    continue;

                if (!includePrerelease && parsed.IsPrerelease)
                    continue;

                if (latestVersion == null || parsed.CompareTo(latestVersion.Value) > 0)
                {
                    latestVersion = parsed;
                    latestTag = tagName.Trim();
                }
            }

            if (latestVersion == null || string.IsNullOrWhiteSpace(latestTag))
            {
                if (!silentIfUpToDate)
                {
                    AppMessageBox.Show(
                        this,
                        "No matching version tag found on GitHub.",
                        "Update check",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                return;
            }

            var currentTag = GetBuildTag();

            if (!TryParseTagVersion(currentTag, out var currentVersion))
            {
                if (!silentOnError)
                {
                    AppMessageBox.Show(
                        this,
                        $"Current version tag is invalid: {currentTag}",
                        "Update check",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                return;
            }

            if (latestVersion.Value.CompareTo(currentVersion) > 0)
            {
                var result = AppMessageBox.Show(
                    this,
                    $"New version available: {latestTag}\n\nOpen download page?",
                    "Update available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/djskual/MibExplorer/releases",
                        UseShellExecute = true
                    });
                }
            }
            else if (!silentIfUpToDate)
            {
                AppMessageBox.Show(
                    this,
                    "You already have the latest version.",
                    "No update",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            if (!silentOnError)
            {
                AppMessageBox.Show(
                    this,
                    $"Unable to check updates.\n\n{ex.Message}",
                    "Update error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }

    private static string NormalizeHeaderText(string? headerText)
    {
        if (string.IsNullOrWhiteSpace(headerText))
            return string.Empty;

        return headerText
            .Replace(" ▲", string.Empty)
            .Replace(" ▼", string.Empty)
            .Trim();
    }

    private void UpdateSortHeaderVisuals()
    {
        if (CurrentFolderList.View is not GridView gridView)
            return;

        var activeColumn = ViewModel.ActiveSortColumn?.Trim();
        var arrow = ViewModel.IsSortAscending ? " ▲" : " ▼";

        foreach (var column in gridView.Columns)
        {
            if (column.Header is not string headerText)
                continue;

            var baseHeader = NormalizeHeaderText(headerText);

            if (string.IsNullOrWhiteSpace(baseHeader))
                continue;

            column.Header = string.Equals(baseHeader, activeColumn, StringComparison.OrdinalIgnoreCase)
                ? baseHeader + arrow
                : baseHeader;
        }
    }

    private void CurrentFolderHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isSortingFromHeader)
            return;

        if (e.OriginalSource is not DependencyObject source)
            return;

        var header = FindVisualParent<GridViewColumnHeader>(source);
        if (header?.Column == null)
            return;

        string? sortKey = null;

        if (header.Column.Header is string headerText)
            sortKey = NormalizeHeaderText(headerText);
        else if (header.Column.Header is TextBlock tb)
            sortKey = NormalizeHeaderText(tb.Text);

        if (string.IsNullOrWhiteSpace(sortKey))
            return;

        // On bloque le comportement natif du header
        e.Handled = true;

        _pendingSortKey = sortKey;

        // On capture AVANT toute reconstruction
        FreezeCurrentFolderList();

        // On force l'overlay à être réellement visible tout de suite
        CurrentFolderListFreezeOverlay.UpdateLayout();

        _isSortingFromHeader = true;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            ExecutePendingHeaderSort();
        }), DispatcherPriority.Send);
    }

    private void FreezeCurrentFolderList()
    {
        CurrentFolderList.UpdateLayout();

        double width = CurrentFolderList.ActualWidth;
        double height = CurrentFolderList.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        var rtb = new RenderTargetBitmap(
            (int)Math.Ceiling(width),
            (int)Math.Ceiling(height),
            96,
            96,
            PixelFormats.Pbgra32);

        rtb.Render(CurrentFolderList);

        CurrentFolderListFreezeOverlay.Source = rtb;
        CurrentFolderListFreezeOverlay.Visibility = Visibility.Visible;
        CurrentFolderListFreezeOverlay.Opacity = 1.0;
        CurrentFolderListFreezeOverlay.UpdateLayout();
    }

    private async void UnfreezeCurrentFolderListDelayed()
    {
        await Task.Delay(25);

        CurrentFolderListFreezeOverlay.Source = null;
        CurrentFolderListFreezeOverlay.Visibility = Visibility.Collapsed;
        CurrentFolderListFreezeOverlay.Opacity = 1.0;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T typed)
                return typed;

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent is null)
            return null;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild)
                return typedChild;

            var result = FindVisualChild<T>(child);
            if (result is not null)
                return result;
        }

        return null;
    }

    private void ExecutePendingHeaderSort()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_pendingSortKey))
                return;

            var scrollViewer = FindVisualChild<ScrollViewer>(CurrentFolderList);
            double horizontalOffset = scrollViewer?.HorizontalOffset ?? 0;
            double verticalOffset = scrollViewer?.VerticalOffset ?? 0;

            ViewModel.SortCurrentFolder(_pendingSortKey);
            UpdateSortHeaderVisuals();

            CurrentFolderList.UpdateLayout();

            var updatedScrollViewer = FindVisualChild<ScrollViewer>(CurrentFolderList);
            if (updatedScrollViewer is not null)
            {
                updatedScrollViewer.ScrollToHorizontalOffset(horizontalOffset);
                updatedScrollViewer.ScrollToVerticalOffset(verticalOffset);
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                UnfreezeCurrentFolderListDelayed();
                _pendingSortKey = null;
                _isSortingFromHeader = false;
            }), DispatcherPriority.Render);
        }
        catch
        {
            UnfreezeCurrentFolderListDelayed();
            _pendingSortKey = null;
            _isSortingFromHeader = false;
            throw;
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void RemoteTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is RemoteExplorerItem item)
            ViewModel.SelectedTreeNode = item;
    }

    private void RemoteTreeView_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ApplyFineVerticalScroll(RemoteTreeView, e);
    }

    private void RemoteTreeView_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var treeViewItem = FindVisualParent<TreeViewItem>(e.OriginalSource as DependencyObject);
        if (treeViewItem?.DataContext is not RemoteExplorerItem item)
            return;

        item.IsSelected = true;
        treeViewItem.Focus();
        e.Handled = true;
    }

    private void CurrentFolderList_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var listViewItem = FindVisualParent<ListViewItem>(e.OriginalSource as DependencyObject);
        if (listViewItem?.DataContext is not RemoteExplorerItem item)
            return;

        CurrentFolderList.SelectedItem = item;
        listViewItem.Focus();
        e.Handled = true;
    }

    private async void RemoteTreeViewItem_OnExpanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem treeViewItem)
            return;

        if (treeViewItem.DataContext is not RemoteExplorerItem item)
            return;

        await ViewModel.EnsureTreeNodeChildrenLoadedAsync(item);
    }

    private void CurrentFolderList_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ApplyFineVerticalScroll(CurrentFolderList, e);
    }

    private void ApplyFineVerticalScroll(DependencyObject source, MouseWheelEventArgs e)
    {
        var scrollViewer = FindVisualChild<ScrollViewer>(source);
        if (scrollViewer is null)
            return;

        double deltaSteps = e.Delta / 120.0;
        double targetOffset = scrollViewer.VerticalOffset - (deltaSteps * FineScrollPixelsPerDetent);

        if (targetOffset < 0)
            targetOffset = 0;
        else if (targetOffset > scrollViewer.ScrollableHeight)
            targetOffset = scrollViewer.ScrollableHeight;

        scrollViewer.ScrollToVerticalOffset(targetOffset);
        e.Handled = true;
    }

    private void CurrentFolderList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListView listView || listView.SelectedItem is not RemoteExplorerItem item)
            return;

        if (!item.IsNavigable)
        {
            OpenFileEditorWindow(item.FullPath);
            return;
        }

        var currentTreeNode = ViewModel.SelectedTreeNode;
        if (currentTreeNode is null)
            return;

        currentTreeNode.IsExpanded = true;

        var matchingTreeNode = currentTreeNode.Children
            .FirstOrDefault(child => string.Equals(child.FullPath, item.FullPath, StringComparison.Ordinal));

        if (matchingTreeNode is null)
        {
            ViewModel.SelectedTreeNode = item;
            return;
        }

        currentTreeNode.IsSelected = false;

        matchingTreeNode.IsSelected = true;
        matchingTreeNode.IsExpanded = true;

        ViewModel.SelectedTreeNode = matchingTreeNode;
    }

    private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }
}
