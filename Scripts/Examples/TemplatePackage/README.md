# Template Script Package

This is a minimal Script Center package template for MibExplorer.

## Structure

- `run.sh` → main script executed on MIB
- `lib/` → shared helper functions (SDK)

## Usage

1. Copy this folder
2. Rename it to your script name
3. Modify `run.sh`
4. Keep the SDK import:

```
BASE_DIR=`pwd`
. "$BASE_DIR/lib/mibexplorer_sdk.sh"
```

### Script Types

You should declare your script type:
ReadOnly → no modification
Apply → modifies system files
Dangerous → advanced / risky operations

### Description

You can add until 3 lines of description

### Notes

Scripts run as root on MIB
Must be QNX compatible
Use LF line endings only

---

