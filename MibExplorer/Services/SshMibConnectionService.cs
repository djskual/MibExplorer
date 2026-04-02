using MibExplorer.Models;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.Globalization;
using System.IO;

namespace MibExplorer.Services;

public sealed partial class SshMibConnectionService : IMibConnectionService
{
    private SshClient? _sshClient;

    public bool IsConnected => _sshClient?.IsConnected == true;
}
