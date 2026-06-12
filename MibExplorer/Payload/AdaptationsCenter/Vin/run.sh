#!/bin/sh

echo "=== MibExplorer Adaptations Center VIN read start ==="

VIN="UNKNOWN"

if [ -f /net/rcc/dev/shmem/VIN.txt ]; then
    VIN=`cat /net/rcc/dev/shmem/VIN.txt 2>/dev/null`
    VIN=${VIN#VIN: }
fi

if [ -z "$VIN" ]; then
    VIN="UNKNOWN"
fi

echo "MIBEXPLORER_VIN=$VIN"

echo "=== MibExplorer Adaptations Center VIN read end ==="
exit 0