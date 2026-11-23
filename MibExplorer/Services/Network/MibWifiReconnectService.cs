using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

namespace MibExplorer.Services;

/// <summary>
/// Uses Windows Native Wi-Fi to preserve the EXACT connected profile and adapter.
/// No password is read or saved. Never creates/modifies a Wi-Fi profile.
/// </summary>
public static class MibWifiReconnectService
{
    public sealed record WifiSnapshot(Guid InterfaceId, string ProfileName, string Ssid);

    private const uint ClientVersion = 2;
    private const int CurrentConnectionOpcode = 7;

    public static WifiSnapshot? CaptureCurrentWifi(string sshHost) =>
        CaptureCurrentWifi(sshHost, out _);

    /// <summary>
    /// Reports the actual reason Wi-Fi discovery failed instead of silently treating
    /// missing Wi-Fi as a successful Wi-Fi connection. On Windows 11 24H2 and later,
    /// Native Wi-Fi SSID APIs may return ERROR_ACCESS_DENIED (5) when Location
    /// access is disabled for desktop apps.
    /// </summary>
    public static WifiSnapshot? CaptureCurrentWifi(string sshHost, out string? diagnostic)
    {
        diagnostic = null;
        try
        {
            using var client = new WlanClient();
            var interfaces = client.GetInterfaces();
            WifiSnapshot? fallback = null;
            bool accessDenied = false;
            foreach (var item in interfaces)
            {
                var snapshot = client.TryGetConnection(item.InterfaceGuid, out uint status);
                if (status == 5) // ERROR_ACCESS_DENIED
                    accessDenied = true;
                if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.ProfileName))
                    continue;

                // Prefer the adapter whose local IPv4 subnet includes the SSH host.
                if (HostIsOnWifiSubnet(sshHost, item.InterfaceGuid))
                    return snapshot;

                fallback ??= snapshot;
            }

            if (fallback is not null)
                return fallback;

            diagnostic = accessDenied
                ? "Windows denied Wi-Fi access (error 5). Enable Location services and desktop app access in Windows Settings."
                : interfaces.Count == 0
                    ? "No Wi-Fi adapter found. Only SSH reconnection can be attempted."
                    : "No active saved Wi-Fi profile found. Restore Wi-Fi manually if needed.";
            return null;
        }
        catch (Exception ex) when (IsWlanUnavailable(ex))
        {
            diagnostic = $"Windows Wi-Fi detection unavailable: {ex.Message}";
            return null;
        }
    }

    public static bool IsConnected(WifiSnapshot snapshot)
    {
        try
        {
            using var client = new WlanClient();
            var current = client.TryGetConnection(snapshot.InterfaceId);
            return current is not null &&
                   string.Equals(current.ProfileName, snapshot.ProfileName, StringComparison.Ordinal) &&
                   string.Equals(current.Ssid, snapshot.Ssid, StringComparison.Ordinal);
        }
        catch (Exception ex) when (IsWlanUnavailable(ex))
        {
            return false;
        }
    }

    /// <summary>
    /// Only request a connection if the original SSID is visibly available.
    /// This avoids forcing Windows off a live network while the MIB is booting.
    /// </summary>
    public static bool TryConnectIfVisible(WifiSnapshot snapshot, bool requestScan)
    {
        try
        {
            using var client = new WlanClient();
            if (client.IsConnected(snapshot))
                return true;

            if (requestScan)
                client.RequestScan(snapshot.InterfaceId);

            if (!client.IsNetworkVisible(snapshot))
                return false;

            return client.Connect(snapshot);
        }
        catch (Exception ex) when (IsWlanUnavailable(ex))
        {
            return false;
        }
    }

    public static void RequestScan(WifiSnapshot snapshot)
    {
        try
        {
            using var client = new WlanClient();
            client.RequestScan(snapshot.InterfaceId);
        }
        catch (Exception ex) when (IsWlanUnavailable(ex))
        {
            // Windows WLAN service may temporarily be unavailable during roaming.
        }
    }

    private static bool HostIsOnWifiSubnet(string host, Guid interfaceId)
    {
        if (!System.Net.IPAddress.TryParse(host, out var remote) ||
            remote.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return false;

        var remoteBytes = remote.GetAddressBytes();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (!Guid.TryParse(adapter.Id, out var adapterGuid) || adapterGuid != interfaceId)
                continue;

            foreach (var local in adapter.GetIPProperties().UnicastAddresses)
            {
                if (local.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
                    local.IPv4Mask is null)
                    continue;

                var address = local.Address.GetAddressBytes();
                var mask = local.IPv4Mask.GetAddressBytes();
                if (Enumerable.Range(0, 4).All(i => (remoteBytes[i] & mask[i]) == (address[i] & mask[i])))
                    return true;
            }
        }
        return false;
    }

    private static bool IsWlanUnavailable(Exception exception) =>
        exception is DllNotFoundException or EntryPointNotFoundException or
        ExternalException or InvalidOperationException or NetworkInformationException;

    private sealed class WlanClient : IDisposable
    {
        private readonly IntPtr _handle;

        public WlanClient()
        {
            uint status = WlanOpenHandle(ClientVersion, IntPtr.Zero, out _, out _handle);
            if (status != 0 || _handle == IntPtr.Zero)
                throw new InvalidOperationException($"Windows Wi-Fi API unavailable ({status}).");
        }

        public List<WlanInterfaceInfo> GetInterfaces()
        {
            uint status = WlanEnumInterfaces(_handle, IntPtr.Zero, out IntPtr buffer);
            if (status != 0)
                throw new InvalidOperationException(status == 5
                    ? "Windows denied Wi-Fi access (error 5). Check Location services and desktop app location access."
                    : $"Windows Wi-Fi adapter enumeration failed (error {status}).");
            if (buffer == IntPtr.Zero)
                return new List<WlanInterfaceInfo>();

            try
            {
                int count = Marshal.ReadInt32(buffer);
                if (count < 0 || count > 32)
                    return new List<WlanInterfaceInfo>();

                int offset = 8;
                int itemSize = Marshal.SizeOf<WlanInterfaceInfo>();
                var result = new List<WlanInterfaceInfo>(count);
                for (int i = 0; i < count; i++)
                    result.Add(Marshal.PtrToStructure<WlanInterfaceInfo>(IntPtr.Add(buffer, offset + i * itemSize)));
                return result;
            }
            finally
            {
                WlanFreeMemory(buffer);
            }
        }

        public WifiSnapshot? TryGetConnection(Guid interfaceId) =>
            TryGetConnection(interfaceId, out _);

        public WifiSnapshot? TryGetConnection(Guid interfaceId, out uint status)
        {
            status = WlanQueryInterface(_handle, ref interfaceId, CurrentConnectionOpcode,
                IntPtr.Zero, out uint size, out IntPtr buffer, out _);
            if (status != 0 || buffer == IntPtr.Zero)
                return null;

            try
            {
                if (size < Marshal.SizeOf<WlanConnectionAttributes>())
                    return null;

                var attributes = Marshal.PtrToStructure<WlanConnectionAttributes>(buffer);
                if (attributes.State != 1 || string.IsNullOrWhiteSpace(attributes.ProfileName))
                    return null;

                int length = checked((int)attributes.Association.Ssid.Length);
                if (length <= 0 || length > 32)
                    return null;

                string ssid = Encoding.UTF8.GetString(attributes.Association.Ssid.Bytes, 0, length);
                return new WifiSnapshot(interfaceId, attributes.ProfileName, ssid);
            }
            finally
            {
                WlanFreeMemory(buffer);
            }
        }

        public bool IsConnected(WifiSnapshot snapshot)
        {
            var current = TryGetConnection(snapshot.InterfaceId);
            return current is not null &&
                   string.Equals(current.ProfileName, snapshot.ProfileName, StringComparison.Ordinal) &&
                   string.Equals(current.Ssid, snapshot.Ssid, StringComparison.Ordinal);
        }

        public void RequestScan(Guid interfaceId)
        {
            // Scan is asynchronous. The next polling iteration sees updated results.
            _ = WlanScan(_handle, ref interfaceId, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        }

        public bool IsNetworkVisible(WifiSnapshot snapshot)
        {
            Guid id = snapshot.InterfaceId;
            uint status = WlanGetAvailableNetworkList(_handle, ref id, 0, IntPtr.Zero, out IntPtr buffer);
            if (status != 0 || buffer == IntPtr.Zero)
                return false;

            try
            {
                int count = Marshal.ReadInt32(buffer);
                if (count < 0 || count > 2048)
                    return false;
                int size = Marshal.SizeOf<WlanAvailableNetwork>();
                for (int i = 0; i < count; i++)
                {
                    var network = Marshal.PtrToStructure<WlanAvailableNetwork>(IntPtr.Add(buffer, 8 + i * size));
                    int length = (int)network.Ssid.Length;
                    if (length is <= 0 or > 32 || !network.IsConnectable)
                        continue;

                    string ssid = Encoding.UTF8.GetString(network.Ssid.Bytes, 0, length);
                    if (string.Equals(ssid, snapshot.Ssid, StringComparison.Ordinal))
                        return true;
                }
                return false;
            }
            finally
            {
                WlanFreeMemory(buffer);
            }
        }

        public bool Connect(WifiSnapshot snapshot)
        {
            var parameters = new WlanConnectionParameters
            {
                ConnectionMode = 0, // wlan_connection_mode_profile
                ProfileName = snapshot.ProfileName,
                BssType = 3, // dot11_BSS_type_any
                Flags = 0
            };
            Guid id = snapshot.InterfaceId;
            return WlanConnect(_handle, ref id, ref parameters, IntPtr.Zero) == 0;
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
                WlanCloseHandle(_handle, IntPtr.Zero);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WlanInterfaceInfo
    {
        public Guid InterfaceGuid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Description;
        public int State;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Dot11Ssid
    {
        public uint Length;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Bytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WlanAssociationAttributes
    {
        public Dot11Ssid Ssid;
        public int BssType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public byte[] Bssid;
        public int PhyType;
        public uint PhyIndex;
        public uint SignalQuality;
        public uint RxRate;
        public uint TxRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WlanSecurityAttributes
    {
        [MarshalAs(UnmanagedType.Bool)] public bool SecurityEnabled;
        [MarshalAs(UnmanagedType.Bool)] public bool OneXEnabled;
        public int AuthAlgorithm;
        public int CipherAlgorithm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WlanConnectionAttributes
    {
        public int State;
        public int ConnectionMode;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string ProfileName;
        public WlanAssociationAttributes Association;
        public WlanSecurityAttributes Security;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WlanAvailableNetwork
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string ProfileName;
        public Dot11Ssid Ssid;
        public int BssType;
        public uint BssidCount;
        [MarshalAs(UnmanagedType.Bool)] public bool IsConnectable;
        public uint NotConnectableReason;
        public uint PhyTypeCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public int[] PhyTypes;
        [MarshalAs(UnmanagedType.Bool)] public bool MorePhyTypes;
        public uint SignalQuality;
        [MarshalAs(UnmanagedType.Bool)] public bool SecurityEnabled;
        public int DefaultAuthAlgorithm;
        public int DefaultCipherAlgorithm;
        public uint Flags;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WlanConnectionParameters
    {
        public int ConnectionMode;
        [MarshalAs(UnmanagedType.LPWStr)] public string ProfileName;
        public IntPtr Dot11Ssid;
        public IntPtr DesiredBssidList;
        public int BssType;
        public uint Flags;
    }

    [DllImport("wlanapi.dll")]
    private static extern uint WlanOpenHandle(uint clientVersion, IntPtr reserved,
        out uint negotiatedVersion, out IntPtr clientHandle);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanEnumInterfaces(IntPtr clientHandle, IntPtr reserved, out IntPtr interfaceList);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanQueryInterface(IntPtr clientHandle, ref Guid interfaceGuid,
        int opCode, IntPtr reserved, out uint dataSize, out IntPtr data, out int valueType);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanGetAvailableNetworkList(IntPtr clientHandle, ref Guid interfaceGuid,
        uint flags, IntPtr reserved, out IntPtr availableNetworkList);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanScan(IntPtr clientHandle, ref Guid interfaceGuid,
        IntPtr ssid, IntPtr ieData, IntPtr reserved);
    [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)]
    private static extern uint WlanConnect(IntPtr clientHandle, ref Guid interfaceGuid,
        ref WlanConnectionParameters connectionParameters, IntPtr reserved);
    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr memory);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanCloseHandle(IntPtr clientHandle, IntPtr reserved);
}
