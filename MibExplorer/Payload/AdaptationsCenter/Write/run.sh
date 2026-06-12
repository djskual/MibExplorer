#!/bin/sh

echo "=== MibExplorer Adaptations Center write start ==="

BASE="$(cd "$(dirname "$0")" && pwd)"
cd "$BASE" || exit 1

export PATH=:/proc/boot:/sbin:/bin:/usr/bin:/usr/sbin:/net/mmx/bin:/net/mmx/usr/bin:/net/mmx/usr/sbin:/net/mmx/sbin:/net/mmx/mnt/app/armle/bin:/net/mmx/mnt/app/armle/sbin:/net/mmx/mnt/app/armle/usr/bin:/net/mmx/mnt/app/armle/usr/sbin
export LD_LIBRARY_PATH=/net/mmx/mnt/app/root/lib-target:/net/mmx/mnt/eso/lib:/net/mmx/eso/lib:/net/mmx/mnt/app/usr/lib:/net/mmx/mnt/app/armle/lib:/net/mmx/mnt/app/armle/lib/dll:/net/mmx/mnt/app/armle/usr/lib
export IPL_CONFIG_DIR=/etc/eso/production

chmod +x "$BASE/pc" 2>/dev/null || true

WRITES_FILE="$BASE/writes.txt"
MODE_FILE="$BASE/mode.txt"

MODE="write"

if [ -f "$MODE_FILE" ]; then
    MODE=`cat "$MODE_FILE" 2>/dev/null`
fi

if [ "$MODE" != "write" ] && [ "$MODE" != "preflight" ]; then
    echo "MIBEXPLORER_ERROR=invalid write mode: $MODE"
    echo "=== MibExplorer Adaptations Center write end ==="
    exit 1
fi

if [ ! -f "$WRITES_FILE" ]; then
    echo "MIBEXPLORER_ERROR=writes.txt missing"
    echo "=== MibExplorer Adaptations Center write end ==="
    exit 1
fi

echo "BASE=$BASE"
echo "PWD=`pwd`"
echo "IPL_CONFIG_DIR=$IPL_CONFIG_DIR"
echo "MIBEXPLORER_WRITE_MODE=$MODE"

WRITE_FAILED=0

while IFS=';' read PART KEY TYPE EXPECTED VALUE
do
    [ -z "$PART" ] && continue
    [ -z "$KEY" ] && continue
    [ -z "$TYPE" ] && continue

    PC_TYPE="$TYPE"

    if [ "$TYPE" = "int" ]; then
        PC_TYPE="i"
    elif [ "$TYPE" = "bool" ]; then
        PC_TYPE="i"
    elif [ "$TYPE" = "enum" ]; then
        PC_TYPE="i"
    elif [ "$TYPE" = "string" ]; then
        PC_TYPE="s"
    elif [ "$TYPE" = "blob" ]; then
        PC_TYPE="b"
    fi

    CURRENT=`cd "$BASE" && ./pc ${PC_TYPE}:${PART}:${KEY} 2>&1`

    if echo "$CURRENT" | grep -q "TIMEOUT\|TYPE_MISMATCH\|DOES_NOT_EXIST\|error:"; then
        WRITE_FAILED=1
        CURRENT_ONE_LINE=`echo "$CURRENT" | tr '\r\n' '  '`
        echo "MIBEXPLORER_CURRENT;$PART;$KEY;$TYPE;$CURRENT_ONE_LINE"
        echo "MIBEXPLORER_EXPECTED;$PART;$KEY;$TYPE;$EXPECTED"
        echo "MIBEXPLORER_TARGET;$PART;$KEY;$TYPE;$VALUE"
        echo "MIBEXPLORER_WRITE_FAILED;$PART;$KEY;$TYPE;preflight read failed: $CURRENT_ONE_LINE"
        continue
    fi

    if [ "$TYPE" = "blob" ]; then
        CURRENT=`echo "$CURRENT" | awk '
        {
            for (i = 1; i <= NF; i++) {
                token = $i

                if (index(token, ":") > 0) {
                    continue
                }

                if (length(token) == 2 && token ~ /^[0-9A-Fa-f][0-9A-Fa-f]$/) {
                    if (out == "") {
                        out = token
                    } else {
                        out = out " " token
                    }
                }
            }
        }
        END {
            print out
        }'`
    fi

    echo "MIBEXPLORER_CURRENT;$PART;$KEY;$TYPE;$CURRENT"
    echo "MIBEXPLORER_EXPECTED;$PART;$KEY;$TYPE;$EXPECTED"
    echo "MIBEXPLORER_TARGET;$PART;$KEY;$TYPE;$VALUE"

    if [ "$CURRENT" != "$EXPECTED" ]; then
        WRITE_FAILED=1
        echo "MIBEXPLORER_WRITE_FAILED;$PART;$KEY;$TYPE;preflight mismatch current '$CURRENT' expected '$EXPECTED'"
        continue
    fi

    if [ "$MODE" = "preflight" ]; then
        echo "MIBEXPLORER_WRITE_OK;$PART;$KEY;$TYPE"
        continue
    fi

    echo "MIBEXPLORER_WRITE_START;$PART;$KEY;$TYPE"

    WRITE_OUTPUT=`cd "$BASE" && ./pc ${PC_TYPE}:${PART}:${KEY} "$VALUE" 2>&1`
    WRITE_RC=$?

    echo "MIBEXPLORER_WRITE_RC;$PART;$KEY;$TYPE;$WRITE_RC"

    if [ -n "$WRITE_OUTPUT" ]; then
        echo "MIBEXPLORER_WRITE_OUTPUT;$PART;$KEY;$TYPE;$WRITE_OUTPUT"
    fi

    FLUSH_OUTPUT=`cd "$BASE" && ./pc b:0:1 0 2>&1`
    FLUSH_RC=$?

    echo "MIBEXPLORER_FLUSH_RC;$PART;$KEY;$TYPE;$FLUSH_RC"

    if [ -n "$FLUSH_OUTPUT" ]; then
        echo "MIBEXPLORER_FLUSH_OUTPUT;$PART;$KEY;$TYPE;$FLUSH_OUTPUT"
    fi

    READBACK=`cd "$BASE" && ./pc ${PC_TYPE}:${PART}:${KEY} 2>&1`

    if echo "$READBACK" | grep -q "TIMEOUT\|TYPE_MISMATCH\|DOES_NOT_EXIST\|error:"; then
        WRITE_FAILED=1
        READBACK_ONE_LINE=`echo "$READBACK" | tr '\r\n' '  '`
        echo "MIBEXPLORER_READBACK;$PART;$KEY;$TYPE;$READBACK_ONE_LINE"
        echo "MIBEXPLORER_WRITE_FAILED;$PART;$KEY;$TYPE;readback failed: $READBACK_ONE_LINE"
        continue
    fi

    if [ "$TYPE" = "blob" ]; then
        READBACK=`echo "$READBACK" | awk '
        {
            for (i = 1; i <= NF; i++) {
                token = $i

                if (index(token, ":") > 0) {
                    continue
                }

                if (length(token) == 2 && token ~ /^[0-9A-Fa-f][0-9A-Fa-f]$/) {
                    if (out == "") {
                        out = token
                    } else {
                        out = out " " token
                    }
                }
            }
        }
        END {
            print out
        }'`
    fi

    echo "MIBEXPLORER_READBACK;$PART;$KEY;$TYPE;$READBACK"

    if [ "$WRITE_RC" -ne 0 ]; then
        WRITE_FAILED=1
        echo "MIBEXPLORER_WRITE_FAILED;$PART;$KEY;$TYPE;write rc $WRITE_RC"
        continue
    fi

    if [ "$FLUSH_RC" -ne 0 ]; then
        WRITE_FAILED=1
        echo "MIBEXPLORER_WRITE_FAILED;$PART;$KEY;$TYPE;flush rc $FLUSH_RC"
        continue
    fi

    if [ "$READBACK" = "$VALUE" ]; then
        echo "MIBEXPLORER_WRITE_OK;$PART;$KEY;$TYPE"
    else
        WRITE_FAILED=1
        echo "MIBEXPLORER_WRITE_FAILED;$PART;$KEY;$TYPE;readback mismatch"
    fi

done < "$WRITES_FILE"

if [ "$WRITE_FAILED" -ne 0 ]; then
    echo "MIBEXPLORER_ERROR=One or more adaptation writes failed"
    echo "=== MibExplorer Adaptations Center write end ==="
    exit 2
fi

echo "MIBEXPLORER_WRITE_RESULT=OK"
echo "=== MibExplorer Adaptations Center write end ==="
exit 0