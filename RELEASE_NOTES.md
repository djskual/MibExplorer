# Release Notes

## Added
- Download action now retrieves remote files from the MIB over SCP

## Improved
- File operations workflow now starts with a real download capability for selected files
- SSH backend now includes SCP connection support for reliable file transfer
- Download operations now remain responsive during long transfers
- Status bar now reflects actual transfer progress and transferred size
- Main window layout has been simplified by removing the obsolete right-side panel

## Technical
- Introduced reusable file transfer progress model for future upload and extract operations
- Switched remote file transfer implementation to SCP for MIB compatibility