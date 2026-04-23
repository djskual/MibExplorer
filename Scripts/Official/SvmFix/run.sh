#!/bin/sh

# Type: Apply
# Version: 1.0.0
# SvmFix by DjSkual
# Generates and applies CfgAck and SvmInfo on the MIB
# Uses modifyE2P to finalize the SVM fix

BASE_DIR=`pwd`
. "$BASE_DIR/lib/mibexplorer_sdk.sh"

SVMTOOL="$BASE_DIR/sbin/svmtool"

mx_step "SvmFix start"

mx_require_cmd "$SVMTOOL"
mx_require_cmd "$MX_PATH_MODIFY_E2P"
mx_require_file "$MX_PATH_CFG_ACK_RAND"
mx_require_file "$MX_PATH_VERSION_TXT"

mx_mount_efs_rw
mx_cleanup_restore_efs_ro
mx_enable_cleanup_trap

mx_step "[1] Generate candidates"
cd "$BASE_DIR" || mx_fail "Unable to enter working directory"
"$SVMTOOL" "$MX_PATH_CFG_ACK_RAND" "$MX_PATH_VERSION_TXT" || mx_fail "svmtool failed"

mx_require_generated "$BASE_DIR/CfgAck_candidate.z"
mx_require_generated "$BASE_DIR/SvmInfo_candidate.z"

mx_step "[2] Apply CfgAck"
mx_replace_file "$BASE_DIR/CfgAck_candidate.z" "$MX_PATH_CFG_ACK"

mx_step "[3] Apply SvmInfo"
mx_replace_file "$BASE_DIR/SvmInfo_candidate.z" "$MX_PATH_SVMINFO"

mx_step "[4] Sync"
mx_sync

mx_step "[5] Trigger modifyE2P"
mx_apply_e2p || mx_fail "modifyE2P failed"

mx_step "[6] Final sync"
mx_sync

mx_step "SvmFix end"

exit 0