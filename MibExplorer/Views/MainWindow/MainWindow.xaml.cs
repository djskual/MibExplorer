using MibExplorer.Models;
using MibExplorer.Settings;
using MibExplorer.ViewModels;
using MibExplorer.Views.Dialogs;
using MibExplorer.Services;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Renci.SshNet.Common;

namespace MibExplorer.Views.MainWindow;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;
    private bool _isSortingFromHeader;
    private string? _pendingSortKey;
    private const string SshKeysFolderName = "Keys";
    private const string SshPrivateKeyFileName = "id_rsa";
    private const double FineScrollPixelsPerDetent = 26.0;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new SshMibConnectionService());

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
                "- id_rsa\n" +
                "- id_rsa.pub\n\n" +
                "Next steps:\n" +
                "1. Copy id_rsa.pub to the Toolbox SD card Custom folder.\n" +
                "2. On the MIB, open Toolbox / Green Menu.\n" +
                "3. Run customization -> advanced -> Install SSHD service.\n" +
                "4. Keep id_rsa on this PC for MibExplorer SSH login.\n\n" +
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

    private void ConnectionHelp_Click(object sender, RoutedEventArgs e)
    {
        AppMessageBox.Show(
            this,
            "How to connect to a prepared MIB\n\n" +
            "1. Make sure Toolbox is already installed on the MIB.\n" +
            "2. Generate SSH keys from Tools -> Generate SSH Keys.\n" +
            "3. Copy id_rsa.pub to the Toolbox SD card Custom folder.\n" +
            "4. On the MIB, run:\n" +
            "   customization -> advanced -> Install SSHD service\n\n" +
            "How to find the SSH IP address\n" +
            "Open the Green Menu and go to:\n" +
            "production -> mmx_prod -> ip-setting_prod -> IP-Address\n\n" +
            "Use the correct interface:\n" +
            "- mlan0 = Wi-Fi client\n" +
            "- uap0 = hotspot\n" +
            "- en0 = Ethernet\n\n" +
            "Connection values for MibExplorer\n" +
            "- Host = the IP shown on the MIB\n" +
            "- Port = 22\n" +
            "- Username = root\n" +
            "- Private key = the path configured in Settings\n" +
            "  default: Keys\\id_rsa\n\n" +
            "Tip:\n" +
            "Your PC must be on the same network as the MIB.",
            "Connection Help",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
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

        if (!item.IsDirectory)
            return;

        ViewModel.SelectedTreeNode = item;
    }

    private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }
}
