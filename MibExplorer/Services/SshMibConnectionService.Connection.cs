using MibExplorer.Models;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.IO;

namespace MibExplorer.Services;

public sealed partial class SshMibConnectionService
{
    public Task ConnectAsync(ConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (settings is null)
            throw new ArgumentNullException(nameof(settings));

        if (string.IsNullOrWhiteSpace(settings.Host))
            throw new InvalidOperationException("Host is required.");

        if (string.IsNullOrWhiteSpace(settings.Username))
            throw new InvalidOperationException("Username is required.");

        ConnectionInfo connectionInfo;

        if (settings.UsePrivateKey)
        {
            if (string.IsNullOrWhiteSpace(settings.PrivateKeyPath))
                throw new InvalidOperationException("Private key path is required.");

            if (!File.Exists(settings.PrivateKeyPath))
                throw new FileNotFoundException("Private key file not found.", settings.PrivateKeyPath);

            PrivateKeyFile privateKeyFile = string.IsNullOrWhiteSpace(settings.Passphrase)
                ? new PrivateKeyFile(settings.PrivateKeyPath)
                : new PrivateKeyFile(settings.PrivateKeyPath, settings.Passphrase);

            connectionInfo = new ConnectionInfo(
                settings.Host,
                settings.Port,
                settings.Username,
                new PrivateKeyAuthenticationMethod(settings.Username, privateKeyFile));
        }
        else
        {
            connectionInfo = new ConnectionInfo(
                settings.Host,
                settings.Port,
                settings.Username,
                new PasswordAuthenticationMethod(settings.Username, settings.Password ?? string.Empty));
        }

        connectionInfo.Timeout = TimeSpan.FromSeconds(10);

        _sshClient = new SshClient(connectionInfo);

        try
        {
            _sshClient.Connect();
        }
        catch
        {
            try
            {
                if (_sshClient is not null)
                {
                    if (_sshClient.IsConnected)
                        _sshClient.Disconnect();

                    _sshClient.Dispose();
                    _sshClient = null;
                }
            }
            catch
            {
            }

            throw;
        }

        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        DisconnectInternal();
        return Task.CompletedTask;
    }

    public Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        using var cmd = _sshClient!.CreateCommand(command);
        string result = cmd.Execute();

        if (cmd.ExitStatus != 0)
        {
            string message = string.IsNullOrWhiteSpace(cmd.Error)
                ? "Remote command failed."
                : cmd.Error.Trim();

            throw new SshException(message);
        }

        return Task.FromResult(result);
    }

    public void Dispose()
    {
        DisconnectInternal();
    }

    private void EnsureConnected()
    {
        if (_sshClient?.IsConnected != true)
            throw new InvalidOperationException("SSH client is not connected.");
    }

    private void DisconnectInternal()
    {
        if (_sshClient is null)
            return;

        try
        {
            if (_sshClient.IsConnected)
                _sshClient.Disconnect();
        }
        finally
        {
            _sshClient.Dispose();
            _sshClient = null;
        }
    }
}
