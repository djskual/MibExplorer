# Release Notes
## Added
- Explicit rename action for remote files and folders
- Live SSH connection state indicator with automatic connect/disconnect button toggle
- Added new App Icons

## Improved
- The main connection button now switches between Connect and Disconnect based on the current SSH session state
- The application now monitors SSH connectivity and updates the UI when the remote connection is lost

## Technical
- Added remote rename support through the SSH service layer
- Added periodic SSH heartbeat checks to keep connection state synchronized with the UI
