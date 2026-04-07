using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

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
    public static MibGatewayDetectionResult? TryDetectMibGateway()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n =>
                n.OperationalStatus == OperationalStatus.Up &&
                n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .ToList();

        // 1) Best match: DNS suffix contains "mib"
        foreach (var ni in interfaces)
        {
            var result = TryBuildResult(ni);
            if (result == null)
                continue;

            if (!string.IsNullOrWhiteSpace(result.DnsSuffix) &&
                result.DnsSuffix.Contains("mib", System.StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }
        }

        // 2) Strong fallback: gateway in 10.173.189.x
        foreach (var ni in interfaces)
        {
            var result = TryBuildResult(ni);
            if (result == null)
                continue;

            if (result.GatewayIp.StartsWith("10.173.189."))
                return result;
        }

        // 3) Wider fallback: local IP + gateway both in 10.173.x.x
        foreach (var ni in interfaces)
        {
            var result = TryBuildResult(ni);
            if (result == null)
                continue;

            if (result.LocalIpv4.StartsWith("10.173.") &&
                result.GatewayIp.StartsWith("10.173."))
            {
                return result;
            }
        }

        // 4) Last fallback: any gateway in 10.x.x.x
        foreach (var ni in interfaces)
        {
            var result = TryBuildResult(ni);
            if (result == null)
                continue;

            if (result.GatewayIp.StartsWith("10."))
                return result;
        }

        return null;
    }

    private static MibGatewayDetectionResult? TryBuildResult(NetworkInterface ni)
    {
        var props = ni.GetIPProperties();

        var gateway = props.GatewayAddresses
            .Select(g => g.Address)
            .FirstOrDefault(a =>
                a != null &&
                a.AddressFamily == AddressFamily.InterNetwork &&
                !a.Equals(System.Net.IPAddress.Any));

        if (gateway == null)
            return null;

        var localIpv4 = props.UnicastAddresses
            .Select(u => u.Address)
            .FirstOrDefault(a =>
                a != null &&
                a.AddressFamily == AddressFamily.InterNetwork &&
                !a.Equals(System.Net.IPAddress.Any));

        if (localIpv4 == null)
            return null;

        return new MibGatewayDetectionResult
        {
            GatewayIp = gateway.ToString(),
            LocalIpv4 = localIpv4.ToString(),
            InterfaceName = ni.Name ?? string.Empty,
            InterfaceDescription = ni.Description ?? string.Empty,
            DnsSuffix = props.DnsSuffix ?? string.Empty
        };
    }
}
