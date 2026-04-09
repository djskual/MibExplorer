# MibExplorer v0.2.0

## 🚀 Major Feature: SSH SD Update Generator

MibExplorer now supports generating a complete SWDL SD Update package to install SSH on MIB2 / MIB2.5 systems.

### Features:

* Full package generation (no external files required)
* Embedded SSH payload (sshd)
* Automatic RSA key pair generation
* Public key included in the update (GEM payload)
* SWDL-compatible structure (Toolbox-like)
* Correct SHA1 checksums for:

  * finalScript.sh
  * hashes.txt
  * payload files
* Correct encoding:

  * UTF-8 without BOM
  * LF for shell scripts
  * CRLF for SWDL metadata
* Output ZIP generated next to the application

---

## 🔐 SSH Installation System

### Full Install:

* Deploys SSH payload to MIB filesystem
* Generates host keys on first boot
* Automatically configures:
  * inetd (SSH service)
  * firewall rules (pf*.conf)
* Patches startup.sh safely:
  * Insert before VNC block if present
  * Fallback after DCIVIDEO block
  * Atomic replacement (safe write)
  * Automatic backup
* Uses a boot-time finalizer (`finish_ssh_boot.sh`)
* Cleans SWDL version tracking entry after install:
  * removes `MibExplorer.info` from FileCopyInfo

### Boot Finalizer:

* Runs once after SWDL update
* Generates missing SSH host keys
* Applies inetd and firewall configuration
* Restarts inetd safely
* Logs execution to SD card when available
* Automatically removes itself after success
* Supports SD auto-detection (sda0 / sdb0)

---

## 🔁 SSH Key Update Mode

MibExplorer can now update SSH keys without reinstalling SSH.

### Behavior:

* Detects existing SSH installation (Toolbox-compatible)
* Replaces only `authorized_keys`
* Does NOT:

  * reinstall payload
  * modify startup.sh
  * modify firewall or inetd
* Cleans SWDL temporary files:

  * id_rsa.pub
  * id_rsa.pub.checksum
  * id_rsa.pub.fileinfo

### Use case:

* Lost private key
* Key rotation
* Quick repair without full reinstall

---

## 🧹 NEW: SSH Uninstall SD Package

MibExplorer now supports full SSH removal via SWDL.

### Uninstall process:

* Triggered via dedicated SD package (dummy SWDL payload)
* Uses same robust architecture as installer

### Behavior:

* Removes all SSH components:
  * payload (sshd binaries and configs)
  * `/root/.ssh`
  * `authorized_keys`
  * `scp` wrapper
  * root `.profile`

* Restores system configuration:
  * `inetd.conf` restored from `.bu` (or cleaned if missing)
  * firewall rules restored from `.bu`

* Cleans SWDL artifacts:
  * `dummy.txt*`
  * `id_rsa.pub*` (defensive cleanup)

* Removes SWDL version tracking entry:
  * `/net/rcc/mnt/efs-persist/SWDL/FileCopyInfo/MibExplorer.info`

* Uses post-reboot finisher for safe execution

### Important:

* `startup.sh` hook is intentionally preserved
* System remains clean and ready for reinstall
* Behavior aligned with Toolbox uninstall logic

---

## 💾 SD Update Reliability Improvements

* Automatic SD card detection (sda0 / sdb0)
* Logging always targets the SAME SD used during SWDL
* Safe mount/unmount handling
* Detailed logs:

  * install_final.txt
  * install_ssh_log.txt
  * finish_ssh_boot.log
* Clean temporary files after execution
* Minimal persistent footprint

---

## 🌐 Automatic MIB IP Detection

Improved and fully reliable automatic detection of the MIB SSH IP.

### Detection logic:

1. Detect active Wi-Fi interface (MIB hotspot)
2. Extract local IPv4 configuration
3. Use:
   - Default gateway (preferred)
   - or DHCP server as fallback if gateway is not available
4. Validate detected IP using TCP connection on port 22

### Behavior:

* Automatically fills Host field
* Sets Port to 22
* Ensures detected IP is reachable via SSH
* Works even without internet access
* Eliminates false positives and unreliable detections

---

## 🗂️ Filesystem Improvements (QNX / MIB)

* Full support for symbolic links (symlinks)
* Symlinks are now navigable
* Correct handling of system paths (`/root`, `/etc`, etc.)
* Hidden files (starting with `.`) are now visible
* Fixed missing entries due to symlink traversal
* Improved parsing of `ls -la`

---

## 🧠 UI / UX Improvements

* `?` button now triggers automatic IP detection
* Updated help content
* Improved user guidance
* Better error handling and feedback

---

## 🔧 Internal Improvements

* New SWDL uninstall builder
* Improved install/update/uninstall separation
* Robust script generation system
* Defensive cleanup mechanisms
* Stronger validation and logging
* Encoding normalization across all generated files

---

## ⚠️ Notes

* PC must be connected to MIB Wi-Fi hotspot for IP detection
* Gateway IP is used for SSH access
* A reboot is required after install/uninstall
* If SSH is broken, use uninstall + reinstall SD workflow

---

## 🧩 Summary

MibExplorer now provides a complete SSH lifecycle:

INSTALL → UPDATE → UNINSTALL

Fully autonomous, safe, and reversible.
