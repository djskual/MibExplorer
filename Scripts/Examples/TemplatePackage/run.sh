#!/bin/sh

# Type: ReadOnly
# Version: 1.0.0
# Example Script Center package using the MibExplorer SDK
# Exports version.txt to the SD card
# Demonstrates standard SDK helpers and workflow

BASE_DIR=`pwd`
. "$BASE_DIR/lib/mibexplorer_sdk.sh"

mx_step "Template script start"

SD_ROOT=`mx_require_sd`
mx_info "Using SD: $SD_ROOT"

mx_mount_sd_rw "$SD_ROOT"
mx_cleanup_restore_sd_ro "$SD_ROOT"
mx_enable_cleanup_trap

OUT_DIR=`mx_prepare_export_dir "$SD_ROOT" "TemplateDump"`
mx_info "Output dir: $OUT_DIR"

mx_copy_to_dir_if_exists "$MX_PATH_VERSION_TXT" "$OUT_DIR"

mx_sync

mx_step "Template script end"

exit 0