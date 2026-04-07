# MibExplorer v0.2.0

## 🚀 Major Feature: SSH SD Update Generator

MibExplorer now supports generating a complete SWDL SD Update package to install SSH on MIB2 / MIB2.5 systems.

### Features:
- Full package generation (no external files required)
- Embedded SSH payload (sshd)
- Automatic RSA key pair generation
- Public key included in the update (GEM payload)
- SWDL-compatible structure (Toolbox-like)
- Correct SHA1 checksums for:
  - finalScript.sh
  - hashes.txt
  - payload files
- Correct encoding:
  - UTF-8 without BOM
  - LF for shell scripts
  - CRLF for SWDL metadata
- Output ZIP generated next to the application

---

## 🔐 SSH Installation System

### Full Install:
- Deploys SSH payload to MIB filesystem
- Generates host keys on first boot
- Automatically configures:
  - inetd (SSH service)
  - firewall rules (pf*.conf)
- Patches startup.sh safely:
  - Insert before VNC block if present
  - Fallback after DCIVIDEO block
  - Atomic replacement (safe write)
  - Automatic backup
- Uses a boot-time finalizer (finish_ssh_boot.sh)

### Boot Finalizer:
- Runs once after SWDL update
- Generates missing SSH host keys
- Applies inetd and firewall configuration
- Restarts inetd safely
- Logs execution to SD card when available
- Automatically removes itself after success
- Supports SD auto-detection (sda0 / sdb0)

---

## 🔁 New Feature: SSH Key Update Mode

MibExplorer can now update SSH keys without reinstalling SSH.

### Behavior:
- Detects existing SSH installation (compatible with Toolbox)
- Replaces only `authorized_keys`
- Does NOT:
  - reinstall payload
  - modify startup.sh
  - modify firewall or inetd
- Cleans SWDL temporary files:
  - id_rsa.pub
  - id_rsa.pub.checksum
  - id_rsa.pub.fileinfo

### Use case:
- Lost private key
- Key rotation
- Quick repair without full reinstall

---

## 💾 SD Update Reliability Improvements

- Automatic SD card detection (sda0 / sdb0)
- Safe mount/unmount handling
- Detailed logging:
  - install_final.txt
  - install_ssh_log.txt
  - finish_ssh_boot.log
- Clean temporary files after successful install
- Preserve only essential backup (startup.sh.original)

---

## 🌐 Automatic MIB IP Detection

MibExplorer can now automatically detect the correct MIB IP address.

### Detection logic:
1. DNS suffix containing "mib" (e.g. mibhigh)
2. Gateway in 10.173.189.x range
3. Gateway in 10.173.x.x range
4. Fallback to any 10.x.x.x gateway

### Behavior:
- Fills Host field automatically
- Sets Port to 22
- Displays interface and network details

---

## 🗂️ Filesystem Improvements (QNX / MIB)

- Full support for symbolic links (symlinks)
- Symlinks are now treated as navigable entries
- Correct navigation for system paths like `/root`, `/etc`, etc.
- Hidden files and directories (starting with `.`) are now visible
- Fixed missing entries caused by symlink traversal issues
- Improved parsing of `ls -la` output

---

## 🧠 UI / UX Improvements

- `?` button now triggers automatic IP detection
- Help menu updated to use detection instead of static instructions
- Improved user guidance with confirmation dialogs
- Better error messages when detection fails

---

## 🔧 Internal Improvements

- New SWDL package builder
- Embedded payload handling
- Robust script generation system
- Safe startup patching mechanism
- Improved install/update logic separation
- Better error handling and logging
- Encoding normalization across all generated files

---

## ⚠️ Notes

- The PC must be connected to the MIB Wi-Fi hotspot before using IP detection
- Gateway IP is used as SSH target
- A reboot may be required after install
- For recovery or broken SSH cases, a dedicated SD reinstall/uninstall package is recommended