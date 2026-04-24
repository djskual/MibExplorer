#!/bin/sh

# Type: ReadOnly
# Version: 1.0.0
# Author: DjSkual
# Extracts imageidmap.rs from skin0 to the SD card
# Useful for quick backup and inspection of the default skin assets

BASE_DIR=`pwd`

SRC="/net/mmx/mnt/app/eso/hmi/lsd/Resources/skin0/imageidmap.res"

echo "=== ExtractSkin0ImageIdMapToSd start ==="

SD_ROOT=""
if [ -d /net/mmx/fs/sda0 ]; then
    SD_ROOT="/net/mmx/fs/sda0"
elif [ -d /net/mmx/fs/sdb0 ]; then
    SD_ROOT="/net/mmx/fs/sdb0"
fi

if [ -z "$SD_ROOT" ]; then
    echo "[ERROR] No SD card found"
    exit 1
fi

echo "[INFO] Using SD root: $SD_ROOT"

mount -uw "$SD_ROOT" 2>/dev/null || true

OUT_DIR="$SD_ROOT/ScriptCenter/skin0"
mkdir -p "$OUT_DIR" 2>/dev/null

if [ ! -f "$SRC" ]; then
    echo "[ERROR] Source file not found: $SRC"
    mount -ur "$SD_ROOT" 2>/dev/null || true
    exit 1
fi

cp "$SRC" "$OUT_DIR/imageidmap.res" || {
    echo "[ERROR] Copy failed"
    mount -ur "$SD_ROOT" 2>/dev/null || true
    exit 1
}

sync 2>/dev/null || true
mount -ur "$SD_ROOT" 2>/dev/null || true

echo "[OK] Exported to: $OUT_DIR/images.mcf"
echo "=== ExtractSkin0ImageIdMapToSd end ==="

exit 0