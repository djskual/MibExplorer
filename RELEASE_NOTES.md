# Release Notes
## Added
- Remote file upload over SCP
- Live transfer progress reporting during uploads
- Remote file deletion with confirmation dialog
- Extract operations now generate a persistent `.mibexplorer-map.json` file to preserve original remote path names

## Improved
- File transfer progress model is now reused for both download and upload operations
- Uploaded files now appear immediately after transfer by refreshing the destination folder
- File management now supports upload, download and delete operations
- Delete operations now use writable mount helpers and automatic read-only restoration
- Prevented unnecessary confirmation dialogs when attempting to delete non-writable paths
- Downloaded and extracted files are now automatically renamed to safe Windows-compatible local names when needed
- Extracted folders now preserve a reliable mapping between sanitized local names and original remote names

## Fixed
- Fixed download and extract issues caused by remote file names containing characters invalid on Windows

## Cleanup

## Technical
- Added writable mount resolution and safe write-operation helpers for upcoming upload and replace features
- Introduced temporary and backup remote path helpers for safer file replacement workflow
- Upload operations now use writable mount helpers and automatic read-only restoration
- Improved shell command safety by properly escaping all remote paths

## UX
- File operations now validate permissions before prompting user confirmation
