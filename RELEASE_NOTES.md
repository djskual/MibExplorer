# Release Notes
## Added
- Safe remote file replacement using temporary upload, backup and final swap

## Improved
- Upload now automatically switches to safe replace mode when the target file already exists
- Existing remote files are no longer overwritten directly during upload
- File and folder actions are now properly disabled until an SSH connection is established
- Tree and content views now use smoother fine-grained scrolling for trackpad and mouse wheel input
- Selected item information is now displayed as a contextual banner under the Remote Explorer
- Improved layout by removing redundant information from the left panel
- More UI adjustments

## Fixed

## Cleanup
- Removed outdated toolbox help text from the connection panel for a cleaner UI
- Removed selected item block from the connection panel to improve UI clarity

## Technical
- Added remote existence checks to support intelligent upload and replace behavior
- Replace operations now use temporary and backup remote paths for safer file updates

## UX
- Prevented inactive file operations from appearing available before connecting to the MIB
