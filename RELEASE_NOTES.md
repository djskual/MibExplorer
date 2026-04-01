# Release Notes

## Added
- SSH foundation layer for MIB remote access
- Extended connection settings with private key authentication support
- Extended app settings with SSH-related persistence fields
- SSH key generation contract and key generation result models
- SSH key generation tool in Tools menu
- Automatic RSA key pair generation for MIB SSH access
- Connection Help dialog in Tools menu
- Host field help button for MIB SSH IP guidance
- Real SSH connection support for prepared MIB units
- Test connection action in main window
- Live remote filesystem loading from MIB root over SSH

## Improved
- SSH key generation now uses Toolbox-style filenames (`id_rsa` / `id_rsa.pub`)
- Generated SSH keys are now opened automatically in the local `Keys` folder
- Connection workflow guidance is integrated directly into the main window
- Main workspace now switches from placeholder mode to live remote data after successful SSH login
- Runtime explorer now browses real MIB folders instead of UI-only placeholder content
- Remote directory browsing compatibility improved for QNX-based MIB systems by parsing `ls -la`
- Folder navigation and refresh flow are now stable after SSH connection

## Fixed
- Remote root directory now displays correctly after SSH login
- Refresh action no longer throws runtime exceptions during remote browsing
- Symlink entries are now handled correctly in the remote explorer
- Folder content loading now fails more safely and reports errors through the status bar

## Cleanup
- Removed temporary SSH debug code used during listing diagnostics
- Simplified SSH integration to its production browsing path

## Technical
- Added SSH.NET dependency for SSH transport
- Added BouncyCastle dependency for RSA key generation
- Introduced `SshMibConnectionService` as the runtime SSH backend
- Kept design-time explorer service separated from the live SSH implementation
- Improved SSH connection lifecycle cleanup on connection failure