using MibExplorer.Models;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.Globalization;
using System.IO;
using System.Threading;

namespace MibExplorer.Services;

public sealed partial class SshMibConnectionService : IMibConnectionService
{
    private SshClient? _sshClient;
    private readonly SemaphoreSlim _scpTransferSemaphore = new(1, 1);

    public bool IsConnected => _sshClient?.IsConnected == true;

    public event EventHandler<bool>? ConnectionStateChanged;

    private void RaiseConnectionStateChanged(bool isConnected)
    {
        ConnectionStateChanged?.Invoke(this, isConnected);
    }
}

