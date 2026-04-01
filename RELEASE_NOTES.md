# Release Notes
## Added
- SSH foundation layer (preparation for upcoming MIB connection support)
- ConnectionSettings extended with private key authentication support
- AppSettings extended with SSH-related persistence fields
- ISshKeyService contract and SSH key generation models
- SSH key generation tool (Tools → Generate SSH Keys)
- Automatic creation of RSA key pair for MIB SSH access
- Integrated workflow guidance after key generation
- Connection Help dialog in Tools menu
- Host field help button for MIB SSH IP guidance

## Improved
- SSH key generation now uses Toolbox-style filenames (id_rsa / id_rsa.pub)
- Generated SSH keys are opened automatically in the local Keys folder after creation
- Connection workflow guidance is now integrated directly in the main window

## Fixed

## Cleanup

## Technical
- Added SSH.NET dependency (future SSH/SCP integration)
