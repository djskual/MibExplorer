using MibExplorer.Models;
using System.IO;

namespace MibExplorer.ViewModels;

public sealed partial class MainViewModel
{
    private async Task ToggleConnectionAsync()
    {
        if (IsConnectedToMib)
        {
            await DisconnectAsync();
            return;
        }

        await ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        try
        {
            SetBusyState(true, "Connecting...", 25);

            string privateKeyPath = !string.IsNullOrWhiteSpace(PrivateKeyPath)
                ? PrivateKeyPath
                : Path.Combine(AppContext.BaseDirectory, "Keys", "id_rsa");

            var settings = new ConnectionSettings
            {
                Host = Host.Trim(),
                Port = int.TryParse(Port, out int parsedPort) ? parsedPort : 22,
                Username = Username.Trim(),
                Password = Password,
                UsePrivateKey = UsePrivateKey && File.Exists(privateKeyPath),
                PrivateKeyPath = privateKeyPath,
                WorkspaceFolder = WorkspaceFolder,
                PublicKeyExportPath = PublicKeyExportPath
            };

            await _mibConnectionService.ConnectAsync(settings);

            ProgressValue = 60;

            string pwd = (await _mibConnectionService.ExecuteCommandAsync("pwd")).Trim();
            if (string.IsNullOrWhiteSpace(pwd))
                pwd = "/";

            await PrepareWorkspaceAsync();

            IsConnectedToMib = true;
            _connectionMonitorTimer.Start();

            StatusMessage = $"SSH connected successfully. Remote pwd: {pwd}";
        }
        catch (Exception ex)
        {
            IsConnectedToMib = false;
            _connectionMonitorTimer.Stop();
            StatusMessage = $"SSH connection failed: {ex.Message}";
        }
        finally
        {
            SetBusyState(false, "Ready", 0);
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            SetBusyState(true, "Disconnecting...", 0);

            _connectionMonitorTimer.Stop();

            await _mibConnectionService.DisconnectAsync();
            IsConnectedToMib = false;

            await PrepareWorkspaceAsync();

            StatusMessage = "Disconnected from MIB.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Disconnect failed: {ex.Message}";
        }
        finally
        {
            SetBusyState(false, "Ready", 0);
        }
    }

    private async void ConnectionMonitorTimer_Tick(object? sender, EventArgs e)
    {
        if (_isConnectionProbeRunning || !_mibConnectionService.IsConnected)
            return;

        try
        {
            _isConnectionProbeRunning = true;
            await _mibConnectionService.ExecuteCommandAsync("pwd");
        }
        catch
        {
            _connectionMonitorTimer.Stop();
            IsConnectedToMib = false;
            StatusMessage = "Connection lost.";
            await PrepareWorkspaceAsync();
        }
        finally
        {
            _isConnectionProbeRunning = false;
        }
    }

    private async Task HandleConnectionLostAsync()
    {
        _connectionMonitorTimer.Stop();
        IsConnectedToMib = false;
        StatusMessage = "Connection lost.";
        await PrepareWorkspaceAsync();
    }
}
