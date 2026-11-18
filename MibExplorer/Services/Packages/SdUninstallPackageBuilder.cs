using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MibExplorer.Services
{
    public sealed class SdUninstallPackageBuilder
    {
        private const string DummyFileName = "dummy.txt";
        private const string PackageNamePrefix = "MibExplorerSSHUninstaller";

        public string BuildPackage()
        {
            string appBaseDir = AppContext.BaseDirectory;

            string tempRoot = Path.Combine(
                Path.GetTempPath(),
                $"MibExplorer_SDUninstall_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}");

            string packageRoot = Path.Combine(tempRoot, PackageNamePrefix);
            Directory.CreateDirectory(packageRoot);

            string swdlAutorunPath = Path.Combine(packageRoot, "Swdlautorun.txt");
            File.WriteAllText(swdlAutorunPath, string.Empty, new UTF8Encoding(false));

            try
            {
                string mibExplorerDir = Path.Combine(packageRoot, "MibExplorer");
                string finalDir = Path.Combine(mibExplorerDir, "final");
                string scriptsDir = Path.Combine(mibExplorerDir, "scripts");
                string gemDir = Path.Combine(mibExplorerDir, "GEM");

                Directory.CreateDirectory(finalDir);
                Directory.CreateDirectory(scriptsDir);
                Directory.CreateDirectory(gemDir);

                string packageDummyPath = Path.Combine(gemDir, DummyFileName);
                WriteUtf8NoBomLf(packageDummyPath, "MibExplorer SSH uninstall trigger\n");

                string uninstallScriptPath = Path.Combine(scriptsDir, "uninstall_ssh.sh");
                WriteUtf8NoBomLf(uninstallScriptPath, UninstallSshScript);

                string finishBootScriptPath = Path.Combine(scriptsDir, "finish_ssh_boot.sh");
                WriteUtf8NoBomLf(finishBootScriptPath, FinishSshBootScript);

                string finalScriptPath = Path.Combine(finalDir, "finalScript.sh");
                WriteUtf8NoBomLf(finalScriptPath, FinalScriptSh);

                string finalScriptSha1 = ComputeSha1Lower(finalScriptPath);

                string hashesTxtPath = Path.Combine(finalDir, "hashes.txt");
                string hashesTxtContent =
                    $"FileName = \"finalScript.sh\"\r\n" +
                    $"FileSize = \"{new FileInfo(finalScriptPath).Length}\"\r\n" +
                    $"CheckSum = \"{finalScriptSha1}\"\r\n";
                WriteUtf8NoBomCrlf(hashesTxtPath, hashesTxtContent);

                string hashesTxtSha1 = ComputeSha1Lower(hashesTxtPath);

                string dummySha1 = ComputeSha1Lower(packageDummyPath);
                long dummySize = new FileInfo(packageDummyPath).Length;

                string metainfoPath = Path.Combine(packageRoot, "metainfo2.txt");
                string metainfoContent = BuildMetainfo(
                    finalScriptSha1,
                    hashesTxtSha1,
                    new FileInfo(hashesTxtPath).Length,
                    dummySha1,
                    dummySize);

                WriteUtf8NoBomCrlf(metainfoPath, metainfoContent);

                string zipPath = Path.Combine(
                    appBaseDir,
                    $"{PackageNamePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

                if (File.Exists(zipPath))
                    File.Delete(zipPath);

                ZipFile.CreateFromDirectory(packageRoot, zipPath, CompressionLevel.Optimal, false);

                return zipPath;
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        private static string ComputeSha1Lower(string filePath)
        {
            using var sha1 = SHA1.Create();
            using var stream = File.OpenRead(filePath);
            return BitConverter.ToString(sha1.ComputeHash(stream))
                .Replace("-", "")
                .ToLowerInvariant();
        }

        private static string BuildMetainfo(
            string finalScriptSha1,
            string hashesTxtSha1,
            long hashesTxtSize,
            string dummySha1,
            long dummySize)
        {
            return
                "[common]\r\n" +
                "vendor = \"ESO\"\r\n" +
                "variant = \"FMU-H-*-*-*\"\r\n" +
                "variant2 = \"FM2-*-*-*-*\"\r\n" +
                "variant3 = \"QC2-*-*-*-*\"\r\n" +
                "variant4 = \"FMQ-*-*-*-*\"\r\n" +
                "region = \"RoW\"\r\n" +
                "MetafileChecksum = \"37259e4758d7c843f316aaaa306cced5211049cf\"\r\n" +
                "\r\n" +
                "[common_Release_1]\r\n" +
                "variant = \"FMU-H-*-*-*\"\r\n" +
                "region = \"RoW\"\r\n" +
                "name = \"MIB1 navigation database\"\r\n" +
                "path = \"./Mib1\"\r\n" +
                "\r\n" +
                "[common_Release_2]\r\n" +
                "variant = \"FM2-*-*-*-*\"\r\n" +
                "variant2 = \"QC2-*-*-*-*\"\r\n" +
                "variant3 = \"FMQ-*-*-*-*\"\r\n" +
                "region = \"RoW\"\r\n" +
                "name = \"MIB2 navigation database\"\r\n" +
                "path = \"./Mib2\"\r\n" +
                "\r\n" +
                "[Signature]\r\n" +
                "signature1 = \"11583e2be1780d5ee04eb62c71e0d2f1\"\r\n" +
                "signature2 = \"dab4162103aaf3a6497f8fd30e97c290\"\r\n" +
                "signature3 = \"ee7d5c8d35bf53c29a8bbc2474c42175\"\r\n" +
                "signature4 = \"ba89fea84694df7b8c3da7de41b82da6\"\r\n" +
                "signature5 = \"cb39043600b6de3fe728adcba7148652\"\r\n" +
                "signature6 = \"b6b7989079d3f2f44bcec54ef59212a2\"\r\n" +
                "signature7 = \"133f9224b6bcc4492e6818b1475a7d83\"\r\n" +
                "signature8 = \"0dde6b0489314cf4924be9e2e91db990\"\r\n" +
                "\r\n" +
                "[common]\r\n" +
                "skipSaveTrainName = \"true\"\r\n" +
                "vendor = \"MibExplorer\"\r\n" +
                "skipCheckSignatureAndVariant = \"true\"\r\n" +
                "region = \"Europe\"\r\n" +
                "region2 = \"RoW\"\r\n" +
                "region3 = \"USA\"\r\n" +
                "region4 = \"Japan\"\r\n" +
                "region5 = \"China\"\r\n" +
                "region6 = \"Taiwan\"\r\n" +
                "variant = \"FM?-*-*-*-*\"\r\n" +
                "release = \"MibExplorer SSH Uninstaller\"\r\n" +
                "skipMetaCRC = \"true\"\r\n" +
                "skipFileCopyCrc = \"true\"\r\n" +
                "skipCheckVariant = \"true\"\r\n" +
                "skipCheckRegion = \"true\"\r\n" +
                $"FinalScript = \"./MibExplorer/final/finalScript.sh\"\r\n" +
                $"FinalScriptChecksum = \"{finalScriptSha1}\"\r\n" +
                "FinalScriptMaxTime = \"60\"\r\n" +
                "FinalScriptName = \"Final Script\"\r\n" +
                "\r\n" +
                "[MibExplorer\\final\\Dir]\r\n" +
                $"FileSize = \"{hashesTxtSize}\"\r\n" +
                $"CheckSum = \"{hashesTxtSha1}\"\r\n" +
                "\r\n" +
                "[MibExplorer]\r\n" +
                "VendorInfo = \"MibExplorer\"\r\n" +
                "DeviceDescription = \"MibExplorer SSH Uninstaller\"\r\n" +
                "DownloadGroup = \"MibExplorer\"\r\n" +
                "\r\n" +
                "[MibExplorer\\GEM\\0\\default\\File]\r\n" +
                "CheckSumSize = \"524288\"\r\n" +
                $"CheckSum = \"{dummySha1}\"\r\n" +
                $"FileSize = \"{dummySize}\"\r\n" +
                "Version = \"1\"\r\n" +
                "Source = \"../../dummy.txt\"\r\n" +
                "Destination = \"/net/mmx/mnt/app/eso/hmi/engdefs/dummy.txt\"\r\n" +
                "DisplayName = \"MibExplorer SSH Uninstaller\"\r\n" +
                "DeleteDestinationDirBeforeCopy = \"false\"\r\n" +
                "UpdateOnlyExisting = \"false\"\r\n";
        }

        private static void TryDeleteDirectory(string directoryPath)
        {
            try
            {
                if (Directory.Exists(directoryPath))
                    Directory.Delete(directoryPath, true);
            }
            catch
            {
            }
        }

        private static void WriteUtf8NoBomLf(string path, string content)
        {
            File.WriteAllText(path, NormalizeToLf(content), new UTF8Encoding(false));
        }

        private static void WriteUtf8NoBomCrlf(string path, string content)
        {
            File.WriteAllText(path, NormalizeToCrlf(content), new UTF8Encoding(false));
        }

        private static string NormalizeToLf(string content)
        {
            return content.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static string NormalizeToCrlf(string content)
        {
            string lf = NormalizeToLf(content);
            return lf.Replace("\n", "\r\n");
        }

        private const string FinishSshBootScript =
@"#!/bin/sh
export PATH=/proc/boot:/bin:/usr/bin:/usr/sbin:/sbin:/mnt/app/media/gracenote/bin:/mnt/app/armle/bin:/mnt/app/armle/sbin:/mnt/app/armle/usr/bin:/mnt/app/armle/usr/sbin:$PATH
export LD_LIBRARY_PATH=/mnt/app/root/lib-target:/eso/lib:/mnt/app/usr/lib:/mnt/app/armle/lib:/mnt/app/armle/lib/dll:/mnt/app/armle/usr/lib

SSH_DIR=""/mnt/app/eso/hmi/engdefs/scripts/ssh""
ROOT_HOME=""/mnt/app/root""
ROOT_SSH_DIR=""$ROOT_HOME/.ssh""
ROOT_AUTH_KEYS=""$ROOT_SSH_DIR/authorized_keys""
ROOT_PROFILE=""$ROOT_HOME/.profile""
ROOT_SCP=""$ROOT_HOME/scp""
INETD_CONF=""/mnt/system/etc/inetd.conf""
INETD_CONF_BU=""/mnt/system/etc/inetd.conf.bu""
PF_GLOB=""/mnt/system/etc/pf*.conf""
SELF_SCRIPT=""/mnt/app/eso/hmi/engdefs/scripts/ssh/finish_ssh_boot.sh""
SDINFO_FILE=""$SSH_DIR/finish_boot_sd_path.txt""
EFS_PERSIST=""/net/rcc/mnt/efs-persist""
FILECOPYINFO_DIR=""$EFS_PERSIST/SWDL/FileCopyInfo""
MIBEXPLORER_INFO_FILE=""$FILECOPYINFO_DIR/MibExplorer.info""

RUNTIME_SD=""""
LOG=""/tmp/finish_ssh_boot.log""

# Resolve the SD card used during SWDL, with fallback auto-detect
if [ -f ""$SDINFO_FILE"" ]; then
    read RUNTIME_SD < ""$SDINFO_FILE""
fi

if [ -z ""$RUNTIME_SD"" ]; then
    for CAND in /net/mmx/fs/sda0 /net/mmx/fs/sdb0; do
        if [ -d ""$CAND"" ]; then
            RUNTIME_SD=""$CAND""
            break
        fi
    done
fi

# Wait a little for the SD to become available after boot
if [ -n ""$RUNTIME_SD"" ]; then
    for i in 1 2 3 4 5 6 7 8 9 10; do
        if [ -d ""$RUNTIME_SD"" ]; then
            break
        fi
        sleep 2
    done
fi

# Prefer logging on the same SD card used for the update
if [ -n ""$RUNTIME_SD"" ] && [ -d ""$RUNTIME_SD"" ]; then
    mount -uw ""$RUNTIME_SD"" >/dev/null 2>&1
    mkdir -p ""$RUNTIME_SD/MibExplorer/output"" >/dev/null 2>&1
    LOG=""$RUNTIME_SD/MibExplorer/output/finish_ssh_boot.log""
fi

echo ""=== finish_ssh_uninstall start ==="" > ""$LOG""
date >> ""$LOG""

sleep 15

mount -uw /net/mmx/mnt/system >> ""$LOG"" 2>&1
mount -uw /net/mmx/mnt/app >> ""$LOG"" 2>&1
mount -uw ""$EFS_PERSIST"" >> ""$LOG"" 2>&1

# Remove our SWDL FileCopyInfo entry
if [ -f ""$MIBEXPLORER_INFO_FILE"" ]; then
    rm -f ""$MIBEXPLORER_INFO_FILE"" >> ""$LOG"" 2>&1
    if [ -f ""$MIBEXPLORER_INFO_FILE"" ]; then
        echo ""Failed to remove $MIBEXPLORER_INFO_FILE"" >> ""$LOG""
    else
        echo ""Removed $MIBEXPLORER_INFO_FILE"" >> ""$LOG""
    fi
else
    echo ""No MibExplorer.info found in FileCopyInfo"" >> ""$LOG""
fi

# Restore inetd from backup when available, otherwise remove SSH line
if [ -f ""$INETD_CONF_BU"" ]; then
    mv -f ""$INETD_CONF_BU"" ""$INETD_CONF"" >> ""$LOG"" 2>&1
    echo ""inetd restored from backup"" >> ""$LOG""
elif [ -f ""$INETD_CONF"" ]; then
    cp -p ""$INETD_CONF"" ""$INETD_CONF.mibexplorer.tmp"" >> ""$LOG"" 2>&1

    if [ -f ""$INETD_CONF.mibexplorer.tmp"" ]; then
        sed -i -r 's:^.*start_sshd.*\n*::p' ""$INETD_CONF.mibexplorer.tmp"" >> ""$LOG"" 2>&1
        cp -p ""$INETD_CONF.mibexplorer.tmp"" ""$INETD_CONF"" >> ""$LOG"" 2>&1
        rm -f ""$INETD_CONF.mibexplorer.tmp"" >> ""$LOG"" 2>&1
        echo ""inetd cleaned without backup"" >> ""$LOG""
    else
        echo ""inetd fallback cleanup failed: temp copy not created"" >> ""$LOG""
    fi
fi

# Restore firewall configs from backups when available
for PF in $PF_GLOB ; do
    if [ -f ""${PF}.bu"" ]; then
        mv -f ""${PF}.bu"" ""$PF"" >> ""$LOG"" 2>&1
        echo ""Restored $PF from backup and removed backup"" >> ""$LOG""
    fi
done

if [ -f /mnt/system/etc/pf.mlan0.conf ]; then
    /mnt/app/armle/sbin/pfctl -F all -f /mnt/system/etc/pf.mlan0.conf >> ""$LOG"" 2>&1
    echo ""pfctl reload attempted"" >> ""$LOG""
fi

# Restart inetd
slay -v inetd >> ""$LOG"" 2>&1
sleep 1
inetd >> ""$LOG"" 2>&1
echo ""inetd restart attempted"" >> ""$LOG""

# Remove SSH files
rm -f ""$ROOT_AUTH_KEYS"" >> ""$LOG"" 2>&1
rm -f ""$ROOT_PROFILE"" >> ""$LOG"" 2>&1
rm -f ""$ROOT_SCP"" >> ""$LOG"" 2>&1
rm -rf ""$ROOT_SSH_DIR"" >> ""$LOG"" 2>&1

rm -rf ""$SSH_DIR/etc"" >> ""$LOG"" 2>&1
rm -rf ""$SSH_DIR/usr"" >> ""$LOG"" 2>&1
rm -f ""$SSH_DIR/finish_boot_sd_path.txt"" >> ""$LOG"" 2>&1

echo ""SSH payload removed"" >> ""$LOG""

rm -rf ""$SSH_DIR"" >> ""$LOG"" 2>&1

echo ""SSH files removed"" >> ""$LOG""

if [ -f ""$INETD_CONF"" ] && grep -qF ""start_sshd"" ""$INETD_CONF""; then
    echo ""Uninstall validation failed: inetd still references start_sshd"" >> ""$LOG""
fi

if [ -e ""$ROOT_AUTH_KEYS"" ]; then
    echo ""Uninstall validation failed: authorized_keys still present"" >> ""$LOG""
fi

if [ -e ""$ROOT_PROFILE"" ]; then
    echo ""Uninstall validation failed: root .profile still present"" >> ""$LOG""
fi

if [ -e ""$ROOT_SCP"" ]; then
    echo ""Uninstall validation failed: scp wrapper still present"" >> ""$LOG""
fi

if [ -d ""$ROOT_SSH_DIR"" ]; then
    echo ""Uninstall validation failed: root .ssh directory still present"" >> ""$LOG""
fi

if [ -d ""$SSH_DIR"" ]; then
    echo ""Uninstall validation failed: SSH install directory still present"" >> ""$LOG""
fi

if [ -e ""$MIBEXPLORER_INFO_FILE"" ]; then
    echo ""Uninstall validation failed: MibExplorer.info still present"" >> ""$LOG""
fi

mount -ur /net/mmx/mnt/system >> ""$LOG"" 2>&1
mount -ur /net/mmx/mnt/app >> ""$LOG"" 2>&1
mount -ur ""$EFS_PERSIST"" >> ""$LOG"" 2>&1

echo ""=== finish_ssh_uninstall end ==="" >> ""$LOG""

if [ -n ""$RUNTIME_SD"" ] && [ -d ""$RUNTIME_SD"" ]; then
    mount -ur ""$RUNTIME_SD"" >> ""$LOG"" 2>&1
fi

exit 0
";

        private const string FinalScriptSh =
@"#!/bin/ksh

echo FinalScript for HW ${1} on medium ${2}...

VOLUME=""${2}""

if [ -z ""$VOLUME"" ]; then
    if [[ -d /net/mmx/fs/sda0 ]]
    then
        echo SDA0 found
        export VOLUME=/net/mmx/fs/sda0
    elif [[ -d /net/mmx/fs/sdb0 ]]
    then
        echo SDB0 found
        export VOLUME=/net/mmx/fs/sdb0
    else
        echo No SD-cards found.
        exit 0
    fi
fi

on -f mmx /bin/mount -uw $VOLUME

OUT=$VOLUME/MibExplorer/output
LOGFILE=$OUT/uninstall_final.txt

mkdir -p $OUT

{
    echo ""FinalScript executed""
    echo ""HW arg: ${1}""
    echo ""MEDIA_PATH: $VOLUME""
    date
} > $LOGFILE

AUTORUN_FILE=""$VOLUME/Swdlautorun.txt""
AUTORUN_DONE_FILE=""$VOLUME/_Swdlautorun.txt""

if [ -f ""$AUTORUN_FILE"" ]; then
    mv ""$AUTORUN_FILE"" ""$AUTORUN_DONE_FILE"" >> ""$LOGFILE"" 2>&1
fi

/bin/sh $VOLUME/MibExplorer/scripts/uninstall_ssh.sh $VOLUME $OUT >> $LOGFILE 2>&1
RESULT=$?

echo ""uninstall_ssh.sh exit code: $RESULT"" >> $LOGFILE

if [ ""$RESULT"" -eq 0 ]; then
    echo ""OK"" > $OUT/status.txt
else
    echo ""ERROR"" > $OUT/status.txt
fi

on -f mmx /bin/mount -ur $VOLUME

echo Done.
touch /tmp/SWDLScript.Result
";

        private const string UninstallSshScript =
@"#!/bin/sh
export PATH=/proc/boot:/bin:/usr/bin:/usr/sbin:/sbin:/mnt/app/media/gracenote/bin:/mnt/app/armle/bin:/mnt/app/armle/sbin:/mnt/app/armle/usr/bin:/mnt/app/armle/usr/sbin:$PATH
export LD_LIBRARY_PATH=/net/mmx/mnt/app/root/lib-target:/net/mmx/eso/lib:/net/mmx/mnt/app/usr/lib:/net/mmx/mnt/app/armle/lib:/net/mmx/mnt/app/armle/lib/dll:/net/mmx/mnt/app/armle/usr/lib

MEDIA_PATH=""$1""
OUT=""$2""

if [ -z ""$OUT"" ]; then
    OUT=""/tmp""
fi

if [ -z ""$MEDIA_PATH"" ]; then
    echo ""MEDIA_PATH missing"" > ""$OUT/uninstall_ssh_error.txt""
    exit 1
fi

APP_ROOT=""/net/mmx/mnt/app""
SYSTEM_ROOT=""/net/mmx/mnt/system""
SSH_DIR=""/net/mmx/mnt/app/eso/hmi/engdefs/scripts/ssh""
STARTUP_FILE=""$SYSTEM_ROOT/etc/boot/startup.sh""
STARTUP_NEW=""$SYSTEM_ROOT/etc/boot/startup.sh.mibexplorer.uninstall.new""
FINISH_BOOT_SCRIPT_SD=""$MEDIA_PATH/MibExplorer/scripts/finish_ssh_boot.sh""
FINISH_BOOT_SCRIPT_DST=""$SSH_DIR/finish_ssh_boot.sh""
FINISH_BOOT_SDINFO=""$SSH_DIR/finish_boot_sd_path.txt""

ERRORS=0

log()
{
    echo ""$1"" >> ""$OUT/uninstall_ssh_log.txt""
}

run_cmd()
{
    ""$@"" >> ""$OUT/uninstall_ssh_log.txt"" 2>&1
    RC=$?
    if [ $RC -ne 0 ]; then
        ERRORS=1
        echo ""Command failed ($RC): $*"" >> ""$OUT/uninstall_ssh_log.txt""
    fi
    return $RC
}

{
    echo ""uninstall_ssh.sh started""
    echo ""MEDIA_PATH=$MEDIA_PATH""
    echo ""APP_ROOT=$APP_ROOT""
    echo ""SYSTEM_ROOT=$SYSTEM_ROOT""
    echo ""SSH_DIR=$SSH_DIR""
    echo ""STARTUP_FILE=$STARTUP_FILE""
    date
} > ""$OUT/uninstall_ssh_log.txt""

run_cmd on -f mmx /bin/mount -uw ""$APP_ROOT""
run_cmd on -f mmx /bin/mount -uw ""$SYSTEM_ROOT""

# Clean SWDL trigger files on MIB side
run_cmd rm -f /net/mmx/mnt/app/eso/hmi/engdefs/dummy.txt
run_cmd rm -f /net/mmx/mnt/app/eso/hmi/engdefs/dummy.txt.checksum
run_cmd rm -f /net/mmx/mnt/app/eso/hmi/engdefs/dummy.txt.fileinfo

run_cmd rm -f /net/mmx/mnt/app/eso/hmi/engdefs/id_rsa.pub
run_cmd rm -f /net/mmx/mnt/app/eso/hmi/engdefs/id_rsa.pub.checksum
run_cmd rm -f /net/mmx/mnt/app/eso/hmi/engdefs/id_rsa.pub.fileinfo

log ""Removed staged SWDL dummy and SSH public key files""

# Ensure ssh dir exists so finisher can be dropped even for Toolbox installs
run_cmd mkdir -p ""$SSH_DIR""

if [ -f ""$FINISH_BOOT_SCRIPT_SD"" ]; then
    run_cmd cp -pv ""$FINISH_BOOT_SCRIPT_SD"" ""$FINISH_BOOT_SCRIPT_DST""
    run_cmd chmod 755 ""$FINISH_BOOT_SCRIPT_DST""
    echo ""$MEDIA_PATH"" > ""$FINISH_BOOT_SDINFO""

    if [ ! -f ""$FINISH_BOOT_SCRIPT_DST"" ]; then
        log ""Failed to copy uninstall finisher to $FINISH_BOOT_SCRIPT_DST""
        exit 1
    fi

    if [ ! -f ""$FINISH_BOOT_SDINFO"" ]; then
        log ""Failed to create uninstall SD info file: $FINISH_BOOT_SDINFO""
        exit 1
    fi

    log ""Copied uninstall finisher to $FINISH_BOOT_SCRIPT_DST""
else
    log ""Missing uninstall finisher on SD: $FINISH_BOOT_SCRIPT_SD""
    exit 1
fi

# Patch startup if hook is missing

if [ ! -f ""$STARTUP_FILE"" ]; then
    log ""Missing startup.sh: $STARTUP_FILE""
    exit 1
fi

if grep -qF ""finish_ssh_boot.sh"" ""$STARTUP_FILE""; then
    log ""startup.sh already contains a finish_ssh_boot hook""
else
    TMP_ORIG=""$OUT/startup_uninstall.orig.tmp""
    TMP_PATCH=""$OUT/startup_uninstall.patched""

    HOOK_INSERTED=0
    IN_DCIVIDEO_BLOCK=0

    run_cmd cp -p ""$STARTUP_FILE"" ""$TMP_ORIG""

    if [ ! -f ""$TMP_ORIG"" ]; then
        log ""Failed to copy startup.sh for uninstall patch""
        exit 1
    fi

    : > ""$TMP_PATCH""

    while IFS= read -r line || [ -n ""$line"" ]; do

        if [ ""$line"" = ""    # QNX VNC CLIENT"" ] && [ ""$HOOK_INSERTED"" -eq 0 ]; then
            echo ""                # MIBEXPLORER SSH FINISHER"" >> ""$TMP_PATCH""
            echo ""                if [ -f /mnt/app/eso/hmi/engdefs/scripts/ssh/finish_ssh_boot.sh ]; then"" >> ""$TMP_PATCH""
            echo ""                    chmod 0755 /mnt/app/eso/hmi/engdefs/scripts/ssh/finish_ssh_boot.sh"" >> ""$TMP_PATCH""
            echo ""                    /bin/sh /mnt/app/eso/hmi/engdefs/scripts/ssh/finish_ssh_boot.sh &"" >> ""$TMP_PATCH""
            echo ""                fi"" >> ""$TMP_PATCH""
            HOOK_INSERTED=1
        fi

        echo ""$line"" >> ""$TMP_PATCH""

        if [ ""$line"" = ""    # DCIVIDEO: Kombi Map"" ]; then
            IN_DCIVIDEO_BLOCK=1
            continue
        fi

        if [ ""$IN_DCIVIDEO_BLOCK"" -eq 1 ] && [ ""$HOOK_INSERTED"" -eq 0 ]; then
            if [ ""$line"" = ""    fi"" ]; then
                echo ""                # MIBEXPLORER SSH FINISHER"" >> ""$TMP_PATCH""
                echo ""                if [ -f /mnt/app/eso/hmi/engdefs/scripts/ssh/finish_ssh_boot.sh ]; then"" >> ""$TMP_PATCH""
                echo ""                    chmod 0755 /mnt/app/eso/hmi/engdefs/scripts/ssh/finish_ssh_boot.sh"" >> ""$TMP_PATCH""
                echo ""                    /bin/sh /mnt/app/eso/hmi/engdefs/scripts/ssh/finish_ssh_boot.sh &"" >> ""$TMP_PATCH""
                echo ""                fi"" >> ""$TMP_PATCH""
                HOOK_INSERTED=1
                IN_DCIVIDEO_BLOCK=0
            fi
        fi

    done < ""$TMP_ORIG""

    if [ ""$HOOK_INSERTED"" -eq 1 ] && grep -qF ""finish_ssh_boot.sh"" ""$TMP_PATCH""; then
        run_cmd rm -f ""$STARTUP_NEW""
        run_cmd cp ""$TMP_PATCH"" ""$STARTUP_NEW""

        if [ ! -f ""$STARTUP_NEW"" ]; then
            log ""Failed to create temporary startup.sh for uninstall""
            exit 1
        fi

        run_cmd chmod 755 ""$STARTUP_NEW""
        RC=$?

        if [ $RC -ne 0 ]; then
            log ""Failed to chmod temporary startup.sh for uninstall""
            exit 1
        fi

        run_cmd mv -f ""$STARTUP_NEW"" ""$STARTUP_FILE""
        RC=$?

        if [ $RC -ne 0 ]; then
            log ""Failed to replace startup.sh for uninstall""
            exit 1
        fi

        run_cmd rm -f ""$TMP_ORIG""
        run_cmd rm -f ""$TMP_PATCH""
        log ""startup.sh patched for uninstall finisher""
    else
        log ""Failed to patch startup.sh for uninstall finisher""
        exit 1
    fi
fi

run_cmd on -f mmx /bin/mount -ur ""$APP_ROOT""
run_cmd on -f mmx /bin/mount -ur ""$SYSTEM_ROOT""

if [ ""$ERRORS"" -ne 0 ]; then
    log ""Uninstall finished with errors""
    exit 1
fi

log ""Done.""
exit 0
";
    }
}
