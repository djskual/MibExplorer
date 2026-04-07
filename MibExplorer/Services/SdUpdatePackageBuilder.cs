using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MibExplorer.Services
{
    public sealed class SdUpdatePackageBuilder
    {
        private const string KeysFolderName = "Keys";
        private const string PrivateKeyFileName = "id_rsa";
        private const string PackageNamePrefix = "MibExplorerSSHInstaller";

        public string BuildPackage()
        {
            string appBaseDir = AppContext.BaseDirectory;

            string keysDir = Path.Combine(appBaseDir, KeysFolderName);
            Directory.CreateDirectory(keysDir);

            string privateKeyPath = Path.Combine(keysDir, PrivateKeyFileName);

            string tempRoot = Path.Combine(
                Path.GetTempPath(),
                $"MibExplorer_SDUpdate_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}");

            string packageRoot = Path.Combine(tempRoot, PackageNamePrefix);
            Directory.CreateDirectory(packageRoot);

            string swdlAutorunPath = Path.Combine(packageRoot, "Swdlautorun.txt");
            File.WriteAllText(swdlAutorunPath, string.Empty, new UTF8Encoding(false));

            try
            {
                string publicKeyText = EnsurePrivateKeyAndGetPublicKey(privateKeyPath);

                string mibExplorerDir = Path.Combine(packageRoot, "MibExplorer");
                string finalDir = Path.Combine(mibExplorerDir, "final");
                string scriptsDir = Path.Combine(mibExplorerDir, "scripts");
                string gemDir = Path.Combine(mibExplorerDir, "GEM");

                Directory.CreateDirectory(finalDir);
                Directory.CreateDirectory(scriptsDir);
                Directory.CreateDirectory(gemDir);

                string packagePublicKeyPath = Path.Combine(gemDir, "id_rsa.pub");
                WriteUtf8NoBomLf(packagePublicKeyPath, EnsureEndsWithLf(publicKeyText));

                string packageSshdDir = Path.Combine(gemDir, "sshd");
                Directory.CreateDirectory(packageSshdDir);

                ExtractEmbeddedZip("MibExplorer.Payload.sshd.zip", packageSshdDir);

                string installScriptPath = Path.Combine(scriptsDir, "install_ssh.sh");
                WriteUtf8NoBomLf(installScriptPath, InstallSshScript);

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

                string publicKeySha1 = ComputeSha1Lower(packagePublicKeyPath);
                long publicKeySize = new FileInfo(packagePublicKeyPath).Length;

                string metainfoPath = Path.Combine(packageRoot, "metainfo2.txt");
                string metainfoContent = BuildMetainfo(
                    finalScriptSha1,
                    hashesTxtSha1,
                    new FileInfo(hashesTxtPath).Length,
                    publicKeySha1,
                    publicKeySize);

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

        private static string EnsurePrivateKeyAndGetPublicKey(string privateKeyPath)
        {
            if (!File.Exists(privateKeyPath))
            {
                using RSA rsa = RSA.Create(2048);

                byte[] privateKey = rsa.ExportPkcs8PrivateKey();
                string privatePem = PemEncode("PRIVATE KEY", privateKey);
                WriteUtf8NoBomLf(privateKeyPath, privatePem);

                return ExportOpenSshPublicKey(rsa, "mibexplorer");
            }

            string existingPrivatePem = File.ReadAllText(privateKeyPath, Encoding.UTF8);

            using RSA rsaFromPem = RSA.Create();
            rsaFromPem.ImportFromPem(existingPrivatePem);

            return ExportOpenSshPublicKey(rsaFromPem, "mibexplorer");
        }

        private static string PemEncode(string label, byte[] data)
        {
            string base64 = Convert.ToBase64String(data);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"-----BEGIN {label}-----");

            for (int i = 0; i < base64.Length; i += 64)
            {
                int len = Math.Min(64, base64.Length - i);
                sb.AppendLine(base64.Substring(i, len));
            }

            sb.AppendLine($"-----END {label}-----");
            return NormalizeToLf(sb.ToString());
        }

        private static string ExportOpenSshPublicKey(RSA rsa, string comment)
        {
            RSAParameters p = rsa.ExportParameters(false);

            if (p.Exponent is null || p.Modulus is null)
                throw new InvalidOperationException("Unable to export RSA public key parameters.");

            using MemoryStream ms = new MemoryStream();
            using BinaryWriter bw = new BinaryWriter(ms);

            WriteSshString(bw, "ssh-rsa");
            WriteMpint(bw, p.Exponent);
            WriteMpint(bw, p.Modulus);

            string blob = Convert.ToBase64String(ms.ToArray());
            return $"ssh-rsa {blob} {comment}";
        }

        private static void WriteSshString(BinaryWriter bw, string value)
        {
            byte[] data = Encoding.ASCII.GetBytes(value);
            WriteUInt32BigEndian(bw, (uint)data.Length);
            bw.Write(data);
        }

        private static void WriteMpint(BinaryWriter bw, byte[] value)
        {
            byte[] data = TrimLeadingZeros(value);

            if (data.Length == 0)
            {
                WriteUInt32BigEndian(bw, 0);
                return;
            }

            if ((data[0] & 0x80) != 0)
                data = new byte[] { 0x00 }.Concat(data).ToArray();

            WriteUInt32BigEndian(bw, (uint)data.Length);
            bw.Write(data);
        }

        private static byte[] TrimLeadingZeros(byte[] value)
        {
            int i = 0;
            while (i < value.Length && value[i] == 0)
                i++;

            return i == value.Length ? Array.Empty<byte>() : value[i..];
        }

        private static void WriteUInt32BigEndian(BinaryWriter bw, uint value)
        {
            bw.Write(new[]
            {
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF)
            });
        }

        private static string ComputeSha1Lower(string filePath)
        {
            using var sha1 = SHA1.Create();
            using var stream = File.OpenRead(filePath);
            return BitConverter.ToString(sha1.ComputeHash(stream))
                .Replace("-", "")
                .ToLowerInvariant();
        }

        private static string ComputeSha1Upper(string filePath)
        {
            using var sha1 = SHA1.Create();
            using var stream = File.OpenRead(filePath);
            return BitConverter.ToString(sha1.ComputeHash(stream))
                .Replace("-", "")
                .ToUpperInvariant();
        }

        private static string BuildMetainfo(
            string finalScriptSha1,
            string hashesTxtSha1,
            long hashesTxtSize,
            string publicKeySha1,
            long publicKeySize)
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
                "release = \"MibExplorer SSH Installer\"\r\n" +
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
                "DeviceDescription = \"MibExplorer SSH Installer\"\r\n" +
                "DownloadGroup = \"MibExplorer\"\r\n" +
                "\r\n" +
                "[MibExplorer\\GEM\\0\\default\\File]\r\n" +
                "CheckSumSize = \"524288\"\r\n" +
                $"CheckSum = \"{publicKeySha1}\"\r\n" +
                $"FileSize = \"{publicKeySize}\"\r\n" +
                "Version = \"1\"\r\n" +
                "Source = \"../../id_rsa.pub\"\r\n" +
                "Destination = \"/net/mmx/mnt/app/eso/hmi/engdefs/id_rsa.pub\"\r\n" +
                "DisplayName = \"MibExplorer SSH Installer\"\r\n" +
                "DeleteDestinationDirBeforeCopy = \"false\"\r\n" +
                "UpdateOnlyExisting = \"false\"\r\n";
        }

        private static void ExtractEmbeddedZip(string resourceName, string destinationDir)
        {
            var assembly = typeof(SdUpdatePackageBuilder).Assembly;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new Exception($"Resource not found: {resourceName}");

            using var archive = new ZipArchive(stream);
            archive.ExtractToDirectory(destinationDir, true);
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

        private static string EnsureEndsWithLf(string content)
        {
            string lf = NormalizeToLf(content);
            return lf.EndsWith("\n", StringComparison.Ordinal) ? lf : lf + "\n";
        }

        private const string FinishSshBootScript =
@"#!/bin/sh
export PATH=/proc/boot:/bin:/usr/bin:/usr/sbin:/sbin:/mnt/app/media/gracenote/bin:/mnt/app/armle/bin:/mnt/app/armle/sbin:/mnt/app/armle/usr/bin:/mnt/app/armle/usr/sbin:$PATH
export LD_LIBRARY_PATH=/mnt/app/root/lib-target:/eso/lib:/mnt/app/usr/lib:/mnt/app/armle/lib:/mnt/app/armle/lib/dll:/mnt/app/armle/usr/lib

SSH_DIR=""/mnt/app/eso/hmi/engdefs/scripts/ssh""
SSH_KEYGEN=""$SSH_DIR/usr/bin/ssh-keygen""
ROOT_HOME=""/mnt/app/root""
ROOT_SSH_DIR=""$ROOT_HOME/.ssh""
ROOT_AUTH_KEYS=""$ROOT_SSH_DIR/authorized_keys""
ROOT_PROFILE=""$ROOT_HOME/.profile""
ROOT_SCP=""$ROOT_HOME/scp""
INETD_CONF=""/mnt/system/etc/inetd.conf""
INETD_CONF_BU=""/mnt/system/etc/inetd.conf.bu""
PF_GLOB=""/mnt/system/etc/pf*.conf""
PF_MAIN=""/mnt/system/etc/pf.mlan0.conf""
SELF_SCRIPT=""/mnt/app/eso/hmi/engdefs/scripts/ssh/finish_ssh_boot.sh""
SDINFO_FILE=""$SSH_DIR/finish_boot_sd_path.txt""

RUNTIME_SD=""""
LOG=""/tmp/finish_ssh_boot.log""

export PATH=""$SSH_DIR/usr/bin:$SSH_DIR/usr/sbin:$PATH""

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

echo ""=== finish_ssh_boot start ==="" > ""$LOG""
date >> ""$LOG""
echo ""RUNTIME_SD=$RUNTIME_SD"" >> ""$LOG""

# Let normal boot settle down first
sleep 30

waitfor /mnt/app/eso 20 >> ""$LOG"" 2>&1
waitfor /dev/ptyp0 20 >> ""$LOG"" 2>&1

mount -uw /net/mmx/mnt/system >> ""$LOG"" 2>&1
mount -uw /net/mmx/mnt/app >> ""$LOG"" 2>&1

sed -ir 's:\r$::g' ""$SSH_DIR/usr/sbin/start_sshd"" >> ""$LOG"" 2>&1
sed -ir 's:\r$::g' ""$SSH_DIR/etc/banner.txt"" >> ""$LOG"" 2>&1
sed -ir 's:\r$::g' ""$ROOT_SCP"" >> ""$LOG"" 2>&1

if [ -f ""$SSH_KEYGEN"" ]; then
    if [ ! -f ""$SSH_DIR/etc/ssh_host_dsa_key"" ]; then
        PATH=""$SSH_DIR/usr/bin:$SSH_DIR/usr/sbin:$PATH"" LD_LIBRARY_PATH=""$SSH_DIR/usr/lib:$LD_LIBRARY_PATH"" ""$SSH_KEYGEN"" -t dsa -N '' -f ""$SSH_DIR/etc/ssh_host_dsa_key"" >> ""$LOG"" 2>&1
    fi

    if [ ! -f ""$SSH_DIR/etc/ssh_host_rsa_key"" ]; then
        PATH=""$SSH_DIR/usr/bin:$SSH_DIR/usr/sbin:$PATH"" LD_LIBRARY_PATH=""$SSH_DIR/usr/lib:$LD_LIBRARY_PATH"" ""$SSH_KEYGEN"" -t rsa -N '' -f ""$SSH_DIR/etc/ssh_host_rsa_key"" -b 1024 >> ""$LOG"" 2>&1
    fi

    if [ ! -f ""$SSH_DIR/etc/ssh_host_key"" ]; then
        PATH=""$SSH_DIR/usr/bin:$SSH_DIR/usr/sbin:$PATH"" LD_LIBRARY_PATH=""$SSH_DIR/usr/lib:$LD_LIBRARY_PATH"" ""$SSH_KEYGEN"" -t rsa -N '' -f ""$SSH_DIR/etc/ssh_host_key"" -b 1024 >> ""$LOG"" 2>&1
    fi
else
    echo ""Missing ssh-keygen: $SSH_KEYGEN"" >> ""$LOG""
fi

if [ -f ""$ROOT_AUTH_KEYS"" ]; then
    chmod 700 ""$ROOT_SSH_DIR"" >> ""$LOG"" 2>&1
    chmod 644 ""$ROOT_AUTH_KEYS"" >> ""$LOG"" 2>&1
fi

echo ""export PATH=$PATH"" > ""$ROOT_PROFILE"" 2>> ""$LOG""
echo ""export LD_LIBRARY_PATH=$LD_LIBRARY_PATH"" >> ""$ROOT_PROFILE"" 2>> ""$LOG""
echo ""PS1='\${USER}@mmx:\${PWD}> '"" >> ""$ROOT_PROFILE"" 2>> ""$LOG""
echo ""export PS1"" >> ""$ROOT_PROFILE"" 2>> ""$LOG""

if [ ! -f ""$INETD_CONF_BU"" ] && [ -f ""$INETD_CONF"" ]; then
    cp -pv ""$INETD_CONF"" ""$INETD_CONF_BU"" >> ""$LOG"" 2>&1
fi

if [ -f ""$INETD_CONF"" ]; then
    cp -p ""$INETD_CONF"" ""$INETD_CONF.mibexplorer.tmp"" >> ""$LOG"" 2>&1
    sed -i -r 's:^.*sshd.*\n*::p' ""$INETD_CONF.mibexplorer.tmp"" >> ""$LOG"" 2>&1
    echo ""ssh        stream tcp nowait root $SSH_DIR/usr/sbin/start_sshd in.sshd"" >> ""$INETD_CONF.mibexplorer.tmp""
    cp -p ""$INETD_CONF.mibexplorer.tmp"" ""$INETD_CONF"" >> ""$LOG"" 2>&1
    rm -f ""$INETD_CONF.mibexplorer.tmp"" >> ""$LOG"" 2>&1
fi

echo ""Add firewall configuration"" >> ""$LOG""
for PF in $PF_GLOB ; do
    if [ ! -f ""${PF}.bu"" ]; then
        cp -pv ""$PF"" ""${PF}.bu"" >> ""$LOG"" 2>&1
    fi
    cp -p ""${PF}.bu"" ""$PF"" >> ""$LOG"" 2>&1

    sed -i -r 's:^(.* port domain keep .*)$:\1\n\n# SSH Access:' ""$PF"" >> ""$LOG"" 2>&1

    if grep -q '\$dbg_if' ""$PF""; then
        sed -i -r 's:^(# SSH Access)$:\1\npass in quick on \$dbg_if proto tcp from any to (\$dbg_if) port 22 keep state allow-opts:' ""$PF"" >> ""$LOG"" 2>&1
    fi
    if grep -q '\$wlan_if' ""$PF""; then
        sed -i -r 's:^(# SSH Access)$:\1\npass in quick on \$wlan_if proto tcp from any to (\$wlan_if) port 22 keep state allow-opts:' ""$PF"" >> ""$LOG"" 2>&1
    fi
    if grep -q '\$ext_if' ""$PF""; then
        sed -i -r 's:^(# SSH Access)$:\1\npass in quick on \$ext_if proto tcp from any to (\$ext_if) port 22 keep state allow-opts:' ""$PF"" >> ""$LOG"" 2>&1
    fi
    if grep -q '\$ppp_if' ""$PF""; then
        sed -i -r 's:^(# SSH Access)$:\1\npass in quick on \$ppp_if proto tcp from any to (\$ppp_if) port 22 keep state allow-opts:' ""$PF"" >> ""$LOG"" 2>&1
    fi

    echo ""Updated $PF"" >> ""$LOG""
done

if [ -f ""$PF_MAIN"" ]; then
    /mnt/app/armle/sbin/pfctl -F all -f ""$PF_MAIN"" >> ""$LOG"" 2>&1
    echo ""pfctl reload attempted"" >> ""$LOG""
else
    echo ""PF main config not found: $PF_MAIN"" >> ""$LOG""
fi

slay -v inetd >> ""$LOG"" 2>&1
sleep 1
inetd >> ""$LOG"" 2>&1
echo ""inetd restart attempted"" >> ""$LOG""

if [ -f ""$SSH_DIR/etc/ssh_host_dsa_key"" ] && \
   [ -f ""$SSH_DIR/etc/ssh_host_rsa_key"" ] && \
   [ -f ""$SSH_DIR/etc/ssh_host_key"" ] && \
   [ -f ""$ROOT_AUTH_KEYS"" ] && \
   grep -qF ""$SSH_DIR/usr/sbin/start_sshd in.sshd"" ""$INETD_CONF""; then

    echo ""Boot finisher success, cleaning up"" >> ""$LOG""

    rm -f ""$SELF_SCRIPT"" >> ""$LOG"" 2>&1
    rm -f ""$SDINFO_FILE"" >> ""$LOG"" 2>&1

else
    echo ""Boot finisher incomplete, keeping self for next boot"" >> ""$LOG""
fi

mount -ur /net/mmx/mnt/system >> ""$LOG"" 2>&1
mount -ur /net/mmx/mnt/app >> ""$LOG"" 2>&1

echo ""=== finish_ssh_boot end ==="" >> ""$LOG""

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

# Make SD writable for logging and result files
on -f mmx /bin/mount -uw $VOLUME

OUT=$VOLUME/MibExplorer/output
LOGFILE=$OUT/install_final.txt

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

/bin/sh $VOLUME/MibExplorer/scripts/install_ssh.sh $VOLUME $OUT >> $LOGFILE 2>&1
RESULT=$?

echo ""install_ssh.sh exit code: $RESULT"" >> $LOGFILE

if [ ""$RESULT"" -eq 0 ]; then
    echo ""OK"" > $OUT/status.txt
else
    echo ""ERROR"" > $OUT/status.txt
fi

# Make the SD readonly again
on -f mmx /bin/mount -ur $VOLUME

echo Done.
touch /tmp/SWDLScript.Result
";

        private const string InstallSshScript =
@"#!/bin/sh
export PATH=/proc/boot:/bin:/usr/bin:/usr/sbin:/sbin:/mnt/app/media/gracenote/bin:/mnt/app/armle/bin:/mnt/app/armle/sbin:/mnt/app/armle/usr/bin:/mnt/app/armle/usr/sbin:$PATH
export LD_LIBRARY_PATH=/net/mmx/mnt/app/root/lib-target:/net/mmx/eso/lib:/net/mmx/mnt/app/usr/lib:/net/mmx/mnt/app/armle/lib:/net/mmx/mnt/app/armle/lib/dll:/net/mmx/mnt/app/armle/usr/lib

MEDIA_PATH=""$1""
OUT=""$2""

if [ -z ""$OUT"" ]; then
    OUT=""/tmp""
fi

if [ -z ""$MEDIA_PATH"" ]; then
    echo ""MEDIA_PATH missing"" > ""$OUT/install_ssh_error.txt""
    exit 1
fi

PAYLOAD_DIR=""$MEDIA_PATH/MibExplorer/GEM/sshd""

PUB_KEY_PATH_SD=""$MEDIA_PATH/MibExplorer/GEM/id_rsa.pub""
PUB_KEY_PATH_SWDL=""/net/mmx/mnt/app/eso/hmi/engdefs/id_rsa.pub""
PUB_KEY_CHECKSUM_SWDL=""/net/mmx/mnt/app/eso/hmi/engdefs/id_rsa.pub.checksum""
PUB_KEY_FILEINFO_SWDL=""/net/mmx/mnt/app/eso/hmi/engdefs/id_rsa.pub.fileinfo""
PUB_KEY_PATH=""""

APP_ROOT=""/net/mmx/mnt/app""
SYSTEM_ROOT=""/net/mmx/mnt/system""
ROOT_HOME=""/net/mmx/mnt/app/root""
ROOT_SSH_DIR=""$ROOT_HOME/.ssh""
ROOT_AUTH_KEYS=""$ROOT_SSH_DIR/authorized_keys""
ROOT_PROFILE=""$ROOT_HOME/.profile""
ROOT_SCP=""$ROOT_HOME/scp""

SSD_INSTALL_DIR=""/net/mmx/mnt/app/eso/hmi/engdefs/scripts/ssh""

FINISH_BOOT_SCRIPT_SD=""$MEDIA_PATH/MibExplorer/scripts/finish_ssh_boot.sh""
FINISH_BOOT_SCRIPT_DST=""$SSD_INSTALL_DIR/finish_ssh_boot.sh""
FINISH_BOOT_SDINFO=""$SSD_INSTALL_DIR/finish_boot_sd_path.txt""
STARTUP_FILE=""$SYSTEM_ROOT/etc/boot/startup.sh""
STARTUP_NEW=""$SYSTEM_ROOT/etc/boot/startup.sh.mibexplorer.new""
STARTUP_BACKUP_SD=""$OUT/startup.sh.original""
SSH_START_SCRIPT=""$SSD_INSTALL_DIR/usr/sbin/start_sshd""
SSH_CONFIG_FILE=""$SSD_INSTALL_DIR/etc/sshd_config""

SSH_ALREADY_INSTALLED=0
ERRORS=0

log()
{
    echo ""$1"" >> ""$OUT/install_ssh_log.txt""
}

run_cmd()
{
    ""$@"" >> ""$OUT/install_ssh_log.txt"" 2>&1
    RC=$?
    if [ $RC -ne 0 ]; then
        ERRORS=1
        echo ""Command failed ($RC): $*"" >> ""$OUT/install_ssh_log.txt""
    fi
    return $RC
}

{
    echo ""install_ssh.sh started""
    echo ""MEDIA_PATH=$MEDIA_PATH""
    echo ""PAYLOAD_DIR=$PAYLOAD_DIR""
    echo ""PUB_KEY_PATH_SWDL=$PUB_KEY_PATH_SWDL""
    echo ""PUB_KEY_PATH_SD=$PUB_KEY_PATH_SD""
    echo ""APP_ROOT=$APP_ROOT""
    echo ""SYSTEM_ROOT=$SYSTEM_ROOT""
    echo ""ROOT_HOME=$ROOT_HOME""
    echo ""SSD_INSTALL_DIR=$SSD_INSTALL_DIR""
    echo ""SSH_START_SCRIPT=$SSH_START_SCRIPT""
    echo ""SSH_CONFIG_FILE=$SSH_CONFIG_FILE""
    date
} > ""$OUT/install_ssh_log.txt""

SSH_ALREADY_INSTALLED=0

if [ -f ""$SSH_START_SCRIPT"" ] && [ -f ""$SSH_CONFIG_FILE"" ] && [ -f ""$ROOT_AUTH_KEYS"" ]; then
    SSH_ALREADY_INSTALLED=1
    log ""Detected existing SSH installation (payload + authorized_keys) -> key update mode""
else
    log ""No existing SSH installation detected -> full install mode""
    if [ -d ""$SSD_INSTALL_DIR"" ]; then
        log ""Existing SSH directory detected but installation looks incomplete - full install/repair will run""
    fi
fi

log ""SSH_ALREADY_INSTALLED=$SSH_ALREADY_INSTALLED""

# Remount real SWDL paths RW
run_cmd on -f mmx /bin/mount -uw ""$APP_ROOT""
run_cmd on -f mmx /bin/mount -uw ""$SYSTEM_ROOT""

if [ ""$SSH_ALREADY_INSTALLED"" -eq 0 ]; then

    log ""Preparing SSH payload and boot finisher...""

    if [ ! -d ""$PAYLOAD_DIR"" ]; then
        log ""Missing payload dir for full install: $PAYLOAD_DIR""
        exit 1
    fi

    run_cmd rm -rf ""$SSD_INSTALL_DIR""
    run_cmd mkdir -p ""$SSD_INSTALL_DIR/etc""

    run_cmd cp -prv ""$PAYLOAD_DIR/etc/."" ""$SSD_INSTALL_DIR/etc/""
    run_cmd cp -prv ""$PAYLOAD_DIR/usr"" ""$SSD_INSTALL_DIR/""

    if [ -d ""$SSD_INSTALL_DIR/usr/bin"" ]; then
        run_cmd chmod 755 ""$SSD_INSTALL_DIR/usr/bin/""*
    fi

    if [ -d ""$SSD_INSTALL_DIR/usr/sbin"" ]; then
        run_cmd chmod 755 ""$SSD_INSTALL_DIR/usr/sbin/""*
    fi

    if [ -f ""$PAYLOAD_DIR/scp_wrapper"" ]; then
        run_cmd cp -pv ""$PAYLOAD_DIR/scp_wrapper"" ""$ROOT_SCP""
        run_cmd chmod 755 ""$ROOT_SCP""
    else
        log ""Missing scp_wrapper in payload""
        ERRORS=1
    fi

    if [ -f ""$FINISH_BOOT_SCRIPT_SD"" ]; then
        run_cmd cp -pv ""$FINISH_BOOT_SCRIPT_SD"" ""$FINISH_BOOT_SCRIPT_DST""
        run_cmd chmod 755 ""$FINISH_BOOT_SCRIPT_DST""
        echo ""$MEDIA_PATH"" > ""$FINISH_BOOT_SDINFO""
    else
        log ""Missing finish_ssh_boot.sh on SD: $FINISH_BOOT_SCRIPT_SD""
        ERRORS=1
    fi
fi

if [ -f ""$PUB_KEY_PATH_SWDL"" ]; then
    PUB_KEY_PATH=""$PUB_KEY_PATH_SWDL""
    log ""Using SWDL-installed public key: $PUB_KEY_PATH""
elif [ -f ""$PUB_KEY_PATH_SD"" ]; then
    PUB_KEY_PATH=""$PUB_KEY_PATH_SD""
    log ""Using SD public key fallback: $PUB_KEY_PATH""
else
    PUB_KEY_PATH=""""
fi

if [ -n ""$PUB_KEY_PATH"" ]; then
    run_cmd mkdir -p ""$ROOT_SSH_DIR""
    run_cmd chmod 700 ""$ROOT_SSH_DIR""
    run_cmd cp -pv ""$PUB_KEY_PATH"" ""$ROOT_AUTH_KEYS""
    run_cmd chmod 644 ""$ROOT_AUTH_KEYS""
    log ""SSH public key install attempted""

    if [ ""$PUB_KEY_PATH"" = ""$PUB_KEY_PATH_SWDL"" ]; then
        run_cmd rm -f ""$PUB_KEY_PATH_SWDL""
        run_cmd rm -f ""$PUB_KEY_CHECKSUM_SWDL""
        run_cmd rm -f ""$PUB_KEY_FILEINFO_SWDL""
        log ""Removed staged SWDL public key and residual metadata files""
    fi
else
    log ""SSH public key missing - password login only""
fi

if [ ""$SSH_ALREADY_INSTALLED"" -eq 0 ]; then

    if [ ! -f ""$STARTUP_FILE"" ]; then
        ERRORS=1
        log ""Missing startup.sh: $STARTUP_FILE""

    elif grep -qF ""# MIBEXPLORER SSH FINISHER"" ""$STARTUP_FILE""; then
        log ""startup.sh already contains MibExplorer hook""

    else

        TMP_ORIG=""$OUT/startup.sh.orig.tmp""
        TMP_PATCH=""$OUT/startup.sh.patched""

        HOOK_INSERTED=0
        IN_DCIVIDEO_BLOCK=0

        if [ ! -f ""$STARTUP_BACKUP_SD"" ]; then
            run_cmd cp -p ""$STARTUP_FILE"" ""$STARTUP_BACKUP_SD""
        fi

        run_cmd cp -p ""$STARTUP_FILE"" ""$TMP_ORIG""

        if [ ! -f ""$TMP_ORIG"" ]; then
            ERRORS=1
            log ""Failed to copy startup.sh to SD""
        else
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

            if [ ""$HOOK_INSERTED"" -eq 1 ]; then
                if grep -qF ""finish_ssh_boot.sh"" ""$TMP_PATCH""; then

                    run_cmd rm -f ""$STARTUP_NEW""
                    run_cmd cp ""$TMP_PATCH"" ""$STARTUP_NEW""

                    if [ ! -f ""$STARTUP_NEW"" ]; then
                        ERRORS=1
                        log ""Failed to create temporary startup.sh on MIB""
                    else
                        run_cmd chmod 755 ""$STARTUP_NEW""
                        RC=$?

                        if [ $RC -ne 0 ]; then
                            ERRORS=1
                            log ""Failed to chmod temporary startup.sh on MIB""
                        else
                            run_cmd mv -f ""$STARTUP_NEW"" ""$STARTUP_FILE""
                            RC=$?

                            if [ $RC -ne 0 ]; then
                                ERRORS=1
                                log ""Failed to replace startup.sh""
                            else
                                run_cmd rm -f ""$TMP_ORIG""
                                run_cmd rm -f ""$TMP_PATCH""
                                log ""startup.sh replaced safely via temporary file""
                            fi
                        fi
                    fi

                else
                    ERRORS=1
                    log ""Patched file does not contain hook (sanity check failed)""
                fi
            else
                ERRORS=1
                log ""Failed to locate startup.sh insertion point""
            fi
        fi
    fi

else
    log ""Key update mode: startup.sh, inetd, firewall and payload left untouched""
fi

# Validate critical outputs
if [ ""$SSH_ALREADY_INSTALLED"" -eq 0 ]; then

    if [ ! -f ""$SSH_CONFIG_FILE"" ]; then
        log ""Validation failed: missing $SSH_CONFIG_FILE""
        ERRORS=1
    fi

    if [ ! -f ""$SSH_START_SCRIPT"" ]; then
        log ""Validation failed: missing $SSH_START_SCRIPT""
        ERRORS=1
    fi

fi

if [ ! -f ""$ROOT_AUTH_KEYS"" ]; then
    log ""Validation failed: missing $ROOT_AUTH_KEYS""
    ERRORS=1
fi

# Remount back RO
run_cmd on -f mmx /bin/mount -ur ""$APP_ROOT""
run_cmd on -f mmx /bin/mount -ur ""$SYSTEM_ROOT""

if [ ""$ERRORS"" -ne 0 ]; then
    log ""Install finished with errors""
    exit 1
fi

log ""Done.""
exit 0
";
    }
}
