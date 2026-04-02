# Release Notes

## Added

- Recursive folder upload (Upload Folder)
- Safe replace folder mechanism:
  - temporary upload directory
  - backup of existing folder
  - atomic swap
  - automatic cleanup
- Confirmation dialog when replacing an existing folder
- Support for recursive folder deletion

## Improved

- File system write operations now fully respect RW/RO mount lifecycle
- Stability of long operations (no unexpected read-only state during upload)
- Upload process now ignores `.mibexplorer-map.json` files
- Delete operation now works consistently for both files and folders
- Improved user confirmation messages for destructive operations

## Fixed

- Fixed read-only filesystem errors during recursive upload
- Fixed mount state issues during nested operations
- Fixed progress reporting compatibility in design mode
- Fixed inability to delete uploaded folders

## Cleanup

- Introduced mount-safe internal operations (WithoutMount methods)
- Simplified and stabilized upload/replace logic
- Removed redundant mount cycles during recursive operations
