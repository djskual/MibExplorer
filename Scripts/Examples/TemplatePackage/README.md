# Template Script Package

This is a minimal Script Center package template for MibExplorer.

It is designed to help script authors create compatible and readable Script Center packages using the MibExplorer SDK.

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
````
## Script Header Convention

Each script should start with:

```
#!/bin/sh
# Type: ReadOnly
# Description line 1
# Description line 2
# Description line 3
```

## Rules

The first commented line must always define the script type:
Type: ReadOnly
Type: Apply
Type: Dangerous
You can add 1, 2, or 3 description lines after the type line
Script Center reads the script type and description from this header

## Script Types

ReadOnly → does not modify the MIB
Apply → modifies files or system state on the MIB
Dangerous → advanced or risky operations

## SDK Notes

The MibExplorer SDK provides helpers for:

 - logging
 - SD detection
 - common MIB paths
 - mounts
 - sync
 - file copy / backup / replace
 - cleanup registration

## Cleanup Notes

Script Center executes packages in an isolated temporary workspace and normally cleans it automatically.

This means:

files created inside the Script Center working directory usually do not need manual cleanup
if your script writes outside its working directory (for example to SD card, /mnt/app, or /mnt/efs-persist), your script should handle cleanup or restoration if needed

## Notes

Scripts run as root on MIB
Scripts must remain QNX compatible
Use LF line endings only