#!/bin/sh

echo "=== MibExplorer Coding Center start ==="

BASE="$(cd "$(dirname "$0")" && pwd)"
cd "$BASE" || exit 1

export PATH=:/proc/boot:/sbin:/bin:/usr/bin:/usr/sbin:/net/mmx/bin:/net/mmx/usr/bin:/net/mmx/usr/sbin:/net/mmx/sbin:/net/mmx/mnt/app/armle/bin:/net/mmx/mnt/app/armle/sbin:/net/mmx/mnt/app/armle/usr/bin:/net/mmx/mnt/app/armle/usr/sbin
export LD_LIBRARY_PATH=/net/mmx/mnt/app/root/lib-target:/net/mmx/mnt/eso/lib:/net/mmx/eso/lib:/net/mmx/mnt/app/usr/lib:/net/mmx/mnt/app/armle/lib:/net/mmx/mnt/app/armle/lib/dll:/net/mmx/mnt/app/armle/usr/lib
export IPL_CONFIG_DIR=/etc/eso/production

chmod +x "$BASE/dumb_persistence_reader" 2>/dev/null || true

CODING_HEX=`./dumb_persistence_reader 0 4101 2>/dev/null`

if [ -z "$CODING_HEX" ]; then
    echo "MIBEXPLORER_ERROR=Unable to read 5F long coding from persistence key 4101"
    echo "=== MibExplorer Coding Center end ==="
    exit 1
fi

BYTE_COUNT=`echo "$CODING_HEX" | wc -c`
BYTE_COUNT=`expr "$BYTE_COUNT" - 1`
BYTE_COUNT=`expr "$BYTE_COUNT" / 2`

VIN="UNKNOWN"

if [ -f /net/rcc/dev/shmem/VIN.txt ]; then
    VIN=`cat /net/rcc/dev/shmem/VIN.txt 2>/dev/null`
    VIN=${VIN#VIN: }
fi

echo "MIBEXPLORER_CODING_HEX=$CODING_HEX"
echo "MIBEXPLORER_BYTE_COUNT=$BYTE_COUNT"
echo "MIBEXPLORER_VIN=$VIN"

echo "=== MibExplorer Coding Center end ==="
exit 0