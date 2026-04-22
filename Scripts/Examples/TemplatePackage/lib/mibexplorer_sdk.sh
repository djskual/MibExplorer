#!/bin/sh

# =========================
# MibExplorer Script SDK v1
# =========================

mx_log() {
    echo "$1"
}

mx_step() {
    echo "=================================================="
    echo "$1"
    echo "=================================================="
}

mx_fail() {
    echo "[ERROR] $1"
    exit 1
}

mx_require_file() {
    if [ ! -f "$1" ]; then
        mx_fail "Missing file: $1"
    fi
}

mx_require_cmd() {
    if [ ! -x "$1" ]; then
        mx_fail "Missing executable: $1"
    fi
}

mx_find_sd() {
    if [ -d /net/mmx/fs/sda0 ]; then
        echo /net/mmx/fs/sda0
        return 0
    fi
    if [ -d /net/mmx/fs/sdb0 ]; then
        echo /net/mmx/fs/sdb0
        return 0
    fi
    return 1
}

mx_mount_sd_rw() {
    mount -uw "$1" 2>/dev/null || true
}

mx_mount_sd_ro() {
    mount -ur "$1" 2>/dev/null || true
}

mx_safe_mkdir() {
    mkdir -p "$1" 2>/dev/null || true
}

mx_copy_if_exists() {
    SRC="$1"
    DST="$2"

    if [ -f "$SRC" ]; then
        cp "$SRC" "$DST"
        mx_log "[OK] copied: $SRC"
    else
        mx_log "[MISS] $SRC"
    fi
}