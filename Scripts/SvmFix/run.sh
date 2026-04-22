#!/bin/sh

# Type: Apply
# SvmFix v1.0 by DjSkual
# Generates and applies CfgAck and SvmInfo on the MIB
# Uses modifyE2P to finalize the SVM fix

echo "=== SvmFix start ==="

BASE_DIR=`pwd`
SVMTOOL="$BASE_DIR/sbin/svmtool"

SWDL_LOG="/net/rcc/mnt/efs-persist/SWDL/Log"
EFS_PERSIST="/net/rcc/mnt/efs-persist"

CFG_ACK_RAND="$SWDL_LOG/CfgAckRand.z"
CFG_ACK="$SWDL_LOG/CfgAck.z"
SVMINFO="$SWDL_LOG/SvmInfo.z"

VERSION_TXT="/net/rcc/dev/shmem/version.txt"
MODIFY_E2P="/net/rcc/usr/apps/modifyE2P"

mount_rw() {
    mount -uw "$EFS_PERSIST" 2>/dev/null || true
}

mount_ro() {
    mount -ur "$EFS_PERSIST" 2>/dev/null || true
}

cleanup() {
    mount_ro
}

trap 'cleanup' EXIT

echo "[1] Mount efs-persist RW"
mount_rw

echo "[2] Check prerequisites"

if [ ! -x "$SVMTOOL" ]; then
    echo "svmtool missing"
    exit 1
fi

if [ ! -x "$MODIFY_E2P" ]; then
    echo "modifyE2P missing"
    exit 1
fi

if [ ! -f "$CFG_ACK_RAND" ]; then
    echo "CfgAckRand.z missing"
    exit 1
fi

if [ ! -f "$VERSION_TXT" ]; then
    echo "version.txt missing"
    exit 1
fi

echo "[3] Generate candidates"
cd "$BASE_DIR" || exit 1
"$SVMTOOL" "$CFG_ACK_RAND" "$VERSION_TXT"

if [ ! -f "$BASE_DIR/CfgAck_candidate.z" ]; then
    echo "CfgAck_candidate.z missing"
    exit 1
fi

if [ ! -f "$BASE_DIR/SvmInfo_candidate.z" ]; then
    echo "SvmInfo_candidate.z missing"
    exit 1
fi

echo "[4] Apply CfgAck"
cp "$BASE_DIR/CfgAck_candidate.z" "$CFG_ACK" || exit 1

echo "[5] Apply SvmInfo"
cp "$BASE_DIR/SvmInfo_candidate.z" "$SVMINFO" || exit 1

echo "[6] Sync"
sync

echo "[7] Trigger modifyE2P"
on -f rcc "$MODIFY_E2P" w 3f0 00 00 01 || exit 1

echo "[8] Final sync"
sync

echo "[9] Done"
echo "=== SvmFix end ==="

exit 0