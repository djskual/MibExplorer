using System.ComponentModel;
using System.Windows;

namespace MibExplorer.Views.Dialogs;

public enum RebootReconnectResult { Connected, TimedOut, Cancelled, Failed }

public partial class RebootMibReconnectWindow : Window
{
    private readonly CancellationTokenSource _cancellation = new();
    private bool _finished;
    private Func<CancellationToken, Action<string, TimeSpan>, Task<RebootReconnectResult>>? _runner;

    public RebootReconnectResult Result { get; private set; } = RebootReconnectResult.Cancelled;

    public RebootMibReconnectWindow()
    {
        InitializeComponent();
        Loaded += Window_Loaded;
    }

    public void Run(Func<CancellationToken, Action<string, TimeSpan>, Task<RebootReconnectResult>> runner)
    {
        _runner = runner;
        ShowDialog();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_runner is null)
        {
            _finished = true;
            Close();
            return;
        }

        try
        {
            Result = await _runner(_cancellation.Token, SetProgress);
        }
        catch (OperationCanceledException)
        {
            Result = RebootReconnectResult.Cancelled;
        }
        catch (Exception ex)
        {
            Result = RebootReconnectResult.Failed;
            StatusText.Text = $"Auto reconnect failed: {ex.Message}";
        }
        finally
        {
            _finished = true;
            Close();
        }
    }

    private void SetProgress(string message, TimeSpan remaining)
    {
        TimeLeftText.Text = $"Time remaining: {Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds))} s";
        Progress.Value = Math.Clamp(180 - remaining.TotalSeconds, 0, 180);
        StatusText.Text = message;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelReconnectButton.IsEnabled = false;
        StatusText.Text = "Cancelling automatic reconnect...";
        _cancellation.Cancel();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_finished)
        {
            e.Cancel = true;
            _cancellation.Cancel();
            CancelReconnectButton.IsEnabled = false;
        }
    }
}
