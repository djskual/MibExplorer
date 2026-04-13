using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MibExplorer.Services;

public sealed class MibGatewayDetectionResult
{
    public string GatewayIp { get; init; } = string.Empty;
    public string LocalIpv4 { get; init; } = string.Empty;
    public string InterfaceName { get; init; } = string.Empty;
    public string InterfaceDescription { get; init; } = string.Empty;
    public string DnsSuffix { get; init; } = string.Empty;
}

public static class MibNetworkHelper
{
    public static async Task<MibGatewayDetectionResult?> TryDetectMibGatewayAsync()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n =>
                n.OperationalStatus == OperationalStatus.Up &&
                n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            .ToList();

        // 1) Best match: Wi-Fi with DNS suffix containing "mib" and a validated host IP
        foreach (var ni in interfaces)
        {
            var result = await TryBuildAndValidateResultAsync(ni);
            if (result == null)
                continue;

            if (!string.IsNullOrWhiteSpace(result.DnsSuffix) &&
                result.DnsSuffix.Contains("mib", StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }
        }

        // 2) Strong fallback: expected MIB local range + validated host IP
        foreach (var ni in interfaces)
        {
            var result = await TryBuildAndValidateResultAsync(ni);
            if (result == null)
                continue;

            if (result.LocalIpv4.StartsWith("10.173.189."))
                return result;
        }

        // 3) Wider fallback: any Wi-Fi interface with local + validated host IP in 10.173.x.x
        foreach (var ni in interfaces)
        {
            var result = await TryBuildAndValidateResultAsync(ni);
            if (result == null)
                continue;

            if (result.LocalIpv4.StartsWith("10.173.") &&
                result.GatewayIp.StartsWith("10.173."))
            {
                return result;
            }
        }

        // 4) Last fallback: any Wi-Fi interface with a validated host IP
        foreach (var ni in interfaces)
        {
            var result = await TryBuildAndValidateResultAsync(ni);
            if (result != null)
                return result;
        }

        return null;
    }

    private static MibGatewayDetectionResult? TryBuildResult(NetworkInterface ni)
    {
        var props = ni.GetIPProperties();

        var localAddressInfo = props.UnicastAddresses
            .FirstOrDefault(u =>
                u.Address != null &&
                u.Address.AddressFamily == AddressFamily.InterNetwork &&
                !u.Address.Equals(IPAddress.Any) &&
                !u.Address.ToString().StartsWith("169.254."));

        if (localAddressInfo == null)
            return null;

        var localIpv4 = localAddressInfo.Address;

        var gateway = props.GatewayAddresses
            .Select(g => g.Address)
            .FirstOrDefault(a =>
                a != null &&
                a.AddressFamily == AddressFamily.InterNetwork &&
                !a.Equals(IPAddress.Any));

        var dhcpServer = props.DhcpServerAddresses
            .Select(a => a)
            .FirstOrDefault(a =>
                a != null &&
                a.AddressFamily == AddressFamily.InterNetwork &&
                !a.Equals(IPAddress.Any));

        string detectedHostIp = string.Empty;

        if (gateway != null)
        {
            detectedHostIp = gateway.ToString();
        }
        else if (dhcpServer != null && IsSameSubnet(localAddressInfo, dhcpServer))
        {
            detectedHostIp = dhcpServer.ToString();
        }

        return new MibGatewayDetectionResult
        {
            GatewayIp = detectedHostIp,
            LocalIpv4 = localIpv4.ToString(),
            InterfaceName = ni.Name ?? string.Empty,
            InterfaceDescription = ni.Description ?? string.Empty,
            DnsSuffix = props.DnsSuffix ?? string.Empty
        };
    }

    private static bool IsSameSubnet(UnicastIPAddressInformation localInfo, IPAddress other)
    {
        if (localInfo.Address.AddressFamily != AddressFamily.InterNetwork ||
            other.AddressFamily != AddressFamily.InterNetwork ||
            localInfo.IPv4Mask == null)
        {
            return false;
        }

        byte[] localBytes = localInfo.Address.GetAddressBytes();
        byte[] otherBytes = other.GetAddressBytes();
        byte[] maskBytes = localInfo.IPv4Mask.GetAddressBytes();

        if (localBytes.Length != 4 || otherBytes.Length != 4 || maskBytes.Length != 4)
            return false;

        for (int i = 0; i < 4; i++)
        {
            if ((localBytes[i] & maskBytes[i]) != (otherBytes[i] & maskBytes[i]))
                return false;
        }

        return true;
    }

    private static async Task<MibGatewayDetectionResult?> TryBuildAndValidateResultAsync(NetworkInterface ni)
    {
        var result = TryBuildResult(ni);
        if (result == null)
            return null;

        if (string.IsNullOrWhiteSpace(result.GatewayIp))
            return null;

        bool sshOpen = await IsTcpPortOpenAsync(result.GatewayIp, 22, 800);
        if (!sshOpen)
            return null;

        return result;
    }

    private static async Task<bool> IsTcpPortOpenAsync(string host, int port, int timeoutMs)
    {
        try
        {
            using var client = new TcpClient();

            Task connectTask = client.ConnectAsync(host, port);
            Task timeoutTask = Task.Delay(timeoutMs);

            Task completedTask = await Task.WhenAny(connectTask, timeoutTask);

            if (completedTask != connectTask)
                return false;

            await connectTask;

            return client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
