#!/bin/sh

echo "=== AdaptationsCenter Read start ==="

BASE="$(cd "$(dirname "$0")" && pwd)"
cd "$BASE" || exit 1

export PATH=:/proc/boot:/sbin:/bin:/usr/bin:/usr/sbin:/net/mmx/bin:/net/mmx/usr/bin:/net/mmx/usr/sbin:/net/mmx/sbin:/net/mmx/mnt/app/armle/bin:/net/mmx/mnt/app/armle/sbin:/net/mmx/mnt/app/armle/usr/bin:/net/mmx/mnt/app/armle/usr/sbin
export LD_LIBRARY_PATH=/net/mmx/mnt/app/root/lib-target:/net/mmx/mnt/eso/lib:/net/mmx/eso/lib:/net/mmx/mnt/app/usr/lib:/net/mmx/mnt/app/armle/lib:/net/mmx/mnt/app/armle/lib/dll:/net/mmx/mnt/app/armle/usr/lib
export IPL_CONFIG_DIR=/etc/eso/production

chmod +x "$BASE/pc" 2>/dev/null || true

KEYS_FILE="$BASE/keys.txt"

if [ ! -f "$KEYS_FILE" ]; then
    echo "MIBEXPLORER_ERROR=keys.txt missing"
    echo "=== AdaptationsCenter Read end ==="
    exit 1
fi

echo "BASE=$BASE"
echo "PWD=`pwd`"
echo "IPL_CONFIG_DIR=$IPL_CONFIG_DIR"

while IFS=';' read PART KEY TYPE
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

    VALUE=`cd "$BASE" && ./pc ${PC_TYPE}:${PART}:${KEY} 2>&1`

    if [ "$TYPE" = "blob" ]; then
        VALUE=`echo "$VALUE" | awk '
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

        if [ -z "$VALUE" ]; then
            VALUE="BLOB_PARSE_FAILED"
        fi
    fi

    echo "MIBEXPLORER_ADAPT;$PART;$KEY;$TYPE;$VALUE"

done < "$KEYS_FILE"

echo "=== AdaptationsCenter Read end ==="
exit 0