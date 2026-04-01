# Release Notes

## Added
- Persistence for SSH key paths across sessions
- SSH connection settings extended with private key path configuration in Settings window

## Improved
- Main connection panel now reflects SSH workflow by displaying the active private key path
- SSH connection now uses the last known private key path instead of a fixed default location
- Connection settings (host, port, username, key path) are restored automatically on startup
- Settings changes are applied immediately to the running session without restart
- SSH workflow simplified to focus on private key authentication only

## Cleanup
- Removed password input from the main connection UI
- Simplified SSH settings to expose only relevant fields for normal usage
- Removed workspace folder and public key export path from Settings window
