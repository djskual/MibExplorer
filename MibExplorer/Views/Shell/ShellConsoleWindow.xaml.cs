using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using MibExplorer.ViewModels;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace MibExplorer.Views.Dialogs;

public partial class ShellConsoleWindow : Window
{
    private const string TerminalOrigin = "https://mibexplorer-terminal.local/index.html";
    // Only these known embedded resources can be served to the isolated WebView2 origin.
    // LICENSE files are embedded as well, but are not loaded by the renderer.
    private static readonly IReadOnlyDictionary<string, (string ResourceName, string ContentType)> TerminalResources =
        new Dictionary<string, (string ResourceName, string ContentType)>(StringComparer.Ordinal)
        {
            ["/index.html"] = ("MibExplorer.Terminal.index.html", "text/html; charset=utf-8"),
            ["/bridge.js"] = ("MibExplorer.Terminal.bridge.js", "text/javascript; charset=utf-8"),
            ["/terminal.css"] = ("MibExplorer.Terminal.terminal.css", "text/css; charset=utf-8"),
            ["/vendor/xterm.js"] = ("MibExplorer.Terminal.xterm.js", "text/javascript; charset=utf-8"),
            ["/vendor/xterm.css"] = ("MibExplorer.Terminal.xterm.css", "text/css; charset=utf-8"),
            ["/vendor/addon-fit.js"] = ("MibExplorer.Terminal.addon-fit.js", "text/javascript; charset=utf-8"),
        };

    private static readonly string[] TerminalLicenseResources =
    {
        "MibExplorer.Terminal.xterm.LICENSE",
        "MibExplorer.Terminal.addon-fit.LICENSE"
    };

    private readonly ShellConsoleViewModel _viewModel;
    private bool _pageReady;
    private bool _shellStarted;
    private bool _closed;
    private uint _columns = 80;
    private uint _rows = 24;

    public ShellConsoleWindow(ShellConsoleViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        _viewModel.OutputReceived += OnShellOutput;
        Loaded += ShellConsoleWindow_Loaded;
    }

    private async void ShellConsoleWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= ShellConsoleWindow_Loaded;
        try
        {
            var assembly = typeof(ShellConsoleWindow).Assembly;
            var embeddedNames = assembly.GetManifestResourceNames();
            foreach (string required in TerminalResources.Values.Select(resource => resource.ResourceName)
                         .Concat(TerminalLicenseResources))
            {
                if (!embeddedNames.Contains(required, StringComparer.Ordinal))
                    throw new InvalidOperationException($"Missing embedded terminal resource: {required}");
            }

            await TerminalView.EnsureCoreWebView2Async();
            if (_closed) return;

            TerminalView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            TerminalView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            TerminalView.CoreWebView2.Settings.AreHostObjectsAllowed = false;
            TerminalView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            TerminalView.CoreWebView2.NewWindowRequested += (_, args) => args.Handled = true;
            TerminalView.CoreWebView2.NavigationStarting += (_, args) =>
            {
                if (!string.Equals(args.Uri, TerminalOrigin, StringComparison.Ordinal))
                    args.Cancel = true;
            };
            TerminalView.CoreWebView2.WebMessageReceived += TerminalWebMessageReceived;
            // Intercept every resource request: never access the network or an external Assets folder.
            TerminalView.CoreWebView2.AddWebResourceRequestedFilter(
                "https://mibexplorer-terminal.local/*", CoreWebView2WebResourceContext.All);
            TerminalView.CoreWebView2.WebResourceRequested += TerminalWebResourceRequested;
            TerminalView.CoreWebView2.Navigate(TerminalOrigin);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Unable to start the integrated terminal.\n\n{ex.Message}\n\n" +
                "Check that Microsoft Edge WebView2 Runtime is installed and the terminal resources are embedded in this build.",
                "Remote Shell", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TerminalWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs args)
    {
        if (_closed || TerminalView.CoreWebView2 is null)
            return;

        // Even if Chromium requests a different URL, we never serve arbitrary assembly data.
        if (!Uri.TryCreate(args.Request.Uri, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "mibexplorer-terminal.local", StringComparison.OrdinalIgnoreCase))
            return;

        if (!TerminalResources.TryGetValue(uri.AbsolutePath, out var asset))
        {
            args.Response = TerminalView.CoreWebView2.Environment.CreateWebResourceResponse(
                Stream.Null, 404, "Not Found", "Content-Type: text/plain\r\nCache-Control: no-store");
            return;
        }

        var stream = typeof(ShellConsoleWindow).Assembly.GetManifestResourceStream(asset.ResourceName);
        if (stream is null)
        {
            args.Response = TerminalView.CoreWebView2.Environment.CreateWebResourceResponse(
                Stream.Null, 500, "Embedded resource unavailable", "Content-Type: text/plain\r\nCache-Control: no-store");
            return;
        }

        args.Response = TerminalView.CoreWebView2.Environment.CreateWebResourceResponse(
            stream, 200, "OK",
            $"Content-Type: {asset.ContentType}\r\nX-Content-Type-Options: nosniff\r\nCache-Control: no-store");
    }

    private async void TerminalWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (_closed || !args.Source.StartsWith("https://mibexplorer-terminal.local/", StringComparison.Ordinal))
            return;
        try
        {
            using var json = JsonDocument.Parse(args.WebMessageAsJson);
            var obj = json.RootElement;
            if (!obj.TryGetProperty("type", out var typeElement)) return;
            string? type = typeElement.GetString();
            switch (type)
            {
                case "ready":
                    if (_shellStarted) break;
                    _pageReady = true;
                    _shellStarted = true;
                    await _viewModel.InitializeAsync();
                    if (_viewModel.IsConnected)
                        await _viewModel.ResizeAsync(_columns, _rows);
                    FocusTerminal();
                    break;
                case "input":
                    if (_viewModel.IsConnected && obj.TryGetProperty("data", out var text))
                        await _viewModel.WriteInputAsync(text.GetString() ?? string.Empty);
                    break;
                case "binary":
                    if (_viewModel.IsConnected && obj.TryGetProperty("data", out var binary))
                        await _viewModel.WriteBytesAsync(Convert.FromBase64String(binary.GetString() ?? string.Empty));
                    break;
                case "clipboardCopy":
                    if (obj.TryGetProperty("data", out var clipboardText) &&
                        clipboardText.ValueKind == JsonValueKind.String)
                    {
                        CopyTextToWindowsClipboard(clipboardText.GetString() ?? string.Empty);
                    }
                    break;
                case "clipboardPaste":
                    PasteWindowsClipboardToTerminal();
                    break;
                case "resize":
                    if (obj.TryGetProperty("cols", out var c) && obj.TryGetProperty("rows", out var r))
                    {
                        uint cols = c.GetUInt32();
                        uint rows = r.GetUInt32();
                        if (cols is >= 2 and <= 1000 && rows is >= 2 and <= 500)
                        {
                            _columns = cols;
                            _rows = rows;
                            if (_viewModel.IsConnected) await _viewModel.ResizeAsync(cols, rows);
                        }
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            if (!_closed)
                MessageBox.Show(this, ex.Message, "Remote Shell", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnShellOutput(object? sender, string data)
    {
        if (_closed) return;
        void Dispatch()
        {
            if (!_closed && _pageReady && TerminalView.CoreWebView2 is not null)
                TerminalView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "output", data }));
        }

        if (Dispatcher.CheckAccess()) Dispatch();
        else if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(Dispatch);
    }

    private async Task<string> GetTerminalTextAsync(string functionName)
    {
        if (!_pageReady || TerminalView.CoreWebView2 is null) return string.Empty;
        string result = await TerminalView.CoreWebView2.ExecuteScriptAsync($"window.mibTerminal.{functionName}()");
        return JsonSerializer.Deserialize<string>(result) ?? string.Empty;
    }

    private async void CopySelection_Click(object sender, RoutedEventArgs e)
    {
        string text = await GetTerminalTextAsync("selection");
        CopyTextToWindowsClipboard(text);
        FocusTerminal();
    }

    private async void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        string text = await GetTerminalTextAsync("allText");
        CopyTextToWindowsClipboard(text);
        FocusTerminal();
    }

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        PasteWindowsClipboardToTerminal();
        FocusTerminal();
    }

    private void CopyTextToWindowsClipboard(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Unable to copy text to Windows clipboard.\n\n{ex.Message}",
                "Remote Shell", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void PasteWindowsClipboardToTerminal()
    {
        if (!_pageReady || !_viewModel.IsConnected || TerminalView.CoreWebView2 is null) return;
        try
        {
            if (!Clipboard.ContainsText()) return;
            string data = Clipboard.GetText();
            if (!string.IsNullOrEmpty(data))
                TerminalView.CoreWebView2.PostWebMessageAsJson(
                    JsonSerializer.Serialize(new { type = "paste", data }));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Unable to paste text from Windows clipboard.\n\n{ex.Message}",
                "Remote Shell", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void SaveLog_Click(object sender, RoutedEventArgs e)
    {
        string text = await GetTerminalTextAsync("allText");
        if (string.IsNullOrEmpty(text)) return;

        var dialog = new SaveFileDialog
        {
            Title = "Save remote shell log",
            Filter = "Text files (*.txt)|*.txt|Log files (*.log)|*.log|All files (*.*)|*.*",
            DefaultExt = ".txt",
            FileName = $"mibexplorer-shell-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };
        if (dialog.ShowDialog(this) == true)
            File.WriteAllText(dialog.FileName, text);
        FocusTerminal();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (_pageReady)
            TerminalView.CoreWebView2?.PostWebMessageAsJson("{\"type\":\"clear\"}");
        FocusTerminal();
    }

    private void FocusTerminal()
    {
        if (_pageReady)
        {
            TerminalView.Focus();
            _ = TerminalView.CoreWebView2?.ExecuteScriptAsync("window.mibTerminal.focus()");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _viewModel.OutputReceived -= OnShellOutput;
        _viewModel.Dispose();
        if (TerminalView.CoreWebView2 is not null)
        {
            TerminalView.CoreWebView2.WebMessageReceived -= TerminalWebMessageReceived;
            TerminalView.CoreWebView2.WebResourceRequested -= TerminalWebResourceRequested;
        }
        TerminalView.Dispose();
        base.OnClosed(e);
    }
}
