#!/bin/sh

echo "=== MibExplorer Coding Center write start ==="

BASE="$(cd "$(dirname "$0")" && pwd)"
cd "$BASE" || exit 1

export PATH=:/proc/boot:/sbin:/bin:/usr/bin:/usr/sbin:/net/mmx/bin:/net/mmx/usr/bin:/net/mmx/usr/sbin:/net/mmx/sbin:/net/mmx/mnt/app/armle/bin:/net/mmx/mnt/app/armle/sbin:/net/mmx/mnt/app/armle/usr/bin:/net/mmx/mnt/app/armle/usr/sbin
export LD_LIBRARY_PATH=/net/mmx/mnt/app/root/lib-target:/net/mmx/mnt/eso/lib:/net/mmx/eso/lib:/net/mmx/mnt/app/usr/lib:/net/mmx/mnt/app/armle/lib:/net/mmx/mnt/app/armle/lib/dll:/net/mmx/mnt/app/armle/usr/lib
export IPL_CONFIG_DIR=/etc/eso/production

chmod +x "$BASE/pc" 2>/dev/null || true
chmod +x "$BASE/dumb_persistence_reader" 2>/dev/null || true

TARGET_HEX="$1"

if [ -z "$TARGET_HEX" ]; then
    echo "MIBEXPLORER_ERROR=Missing target coding hex"
    echo "=== MibExplorer Coding Center write end ==="
    exit 1
fi

LEN=`echo "$TARGET_HEX" | wc -c`
LEN=`expr "$LEN" - 1`

if [ "$LEN" -ne 50 ]; then
    echo "MIBEXPLORER_ERROR=Invalid target coding length: $LEN"
    echo "=== MibExplorer Coding Center write end ==="
    exit 2
fi

BEFORE=`./dumb_persistence_reader 0 4101 2>/dev/null`
echo "MIBEXPLORER_BEFORE_HEX=$BEFORE"

echo "=== Writing full 4101 blob ==="
./pc b:0:4101 "$TARGET_HEX" 2>&1
WRITE_RC=$?

./pc b:0:1 0 2>&1
FLUSH_RC=$?

echo "MIBEXPLORER_WRITE_RC=$WRITE_RC"
echo "MIBEXPLORER_FLUSH_RC=$FLUSH_RC"

AFTER=`./dumb_persistence_reader 0 4101 2>/dev/null`
echo "MIBEXPLORER_AFTER_HEX=$AFTER"

if [ "$AFTER" = "$TARGET_HEX" ]; then
    echo "MIBEXPLORER_WRITE_RESULT=OK"
    echo "=== MibExplorer Coding Center write end ==="
    exit 0
fi

echo "MIBEXPLORER_ERROR=Readback mismatch after writing 4101"
echo "=== MibExplorer Coding Center write end ==="
exit 3