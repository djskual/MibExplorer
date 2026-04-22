#!/bin/sh

# Type: ReadOnly
# Example Script Center package
# Exports version.txt to the SD card
# you can use until 3 lines of description

BASE_DIR=`pwd`
. "$BASE_DIR/lib/mibexplorer_sdk.sh"

mx_step "Template script start"

SD_ROOT=`mx_find_sd` || mx_fail "No SD card found"
mx_log "Using SD: $SD_ROOT"

mx_mount_sd_rw "$SD_ROOT"

OUT_DIR="$SD_ROOT/TemplateDump"
mx_safe_mkdir "$OUT_DIR"

mx_copy_if_exists "/net/rcc/dev/shmem/version.txt" "$OUT_DIR"

sync
mx_mount_sd_ro "$SD_ROOT"

mx_step "Template script end"

exit 0