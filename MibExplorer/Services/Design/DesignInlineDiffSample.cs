namespace MibExplorer.Services.Design;

public static class DesignInlineDiffSample
{
    public const string RemotePath = "/eso/system/inline-diff-sample.cfg";
    public const string FileName = "inline-diff-sample.cfg";

    public static string GetContent()
    {
        return
"""
# Inline diff live test sample
# Open this file in the editor, then modify it manually to test inline diff rendering.

[General]
SystemName=MIB2_MAIN_UNIT
Variant=STD
Region=EU
Language=fr_FR
BootMode=Normal
BootDelayMs=250
LogLevel=Info
Theme=Dark

[Network]
WifiMode=Hotspot
WifiSsid=MIB-TEST
WifiChannel=6
Dhcp=true
IpAddress=192.168.1.10
Netmask=255.255.255.0
Gateway=192.168.1.1
Dns1=8.8.8.8
Dns2=1.1.1.1

[Paths]
Root=/eso
ConfigDir=/eso/system/config
CacheDir=/mnt/efs-system/cache
LogDir=/tsd/logs
SkinDir=/eso/hmi/lsd/Resources/skin0
MapDir=/mnt/efs-system/nav/maps/2024_01_EU
LongPathExample=/mnt/efs-system/some/really/long/path/used/to/verify/horizontal/scrolling/in/the/editor/window/with/enough/length/to/exceed/the/visible/area/and/produce/a/realistic/test_case

[Editor]
TabSample1=Column1	Column2	Column3	Column4
TabSample2=ValueA	1234	true	ready
TabSample3=Left	Middle	Right	End

[Services]
Service01=inetd:on
Service02=telnetd:off
Service03=sshd:off
Service04=ftpd:off
Service05=logger:on
Service06=navd:on
Service07=hmid:on
Service08=diagnostics:on

[Vehicles]
Vehicle01=Golf7GTE
Vehicle02=PassatB8GTE
Vehicle03=TiguanAD1
Vehicle04=Arteon3H
Vehicle05=PoloAW
Vehicle06=Touran5T

[Rules]
Rule01=allow:/eso/system/*
Rule02=allow:/mnt/efs-system/*
Rule03=deny:/net/*
Rule04=prompt:/tsd/*
Rule05=readonly:/eso/hmi/lsd/Resources/skin0/images.mcf

[Todo]
Item01=Change Variant from STD to HIGH
Item02=Change LogLevel from Info to Debug
Item03=Modify one tabbed line
Item04=Add one new service line
Item05=Delete one vehicle line
Item06=Change the long path line

[Checksum]
Value=INLINE-DIFF-TEST-BASELINE
""";
    }
}