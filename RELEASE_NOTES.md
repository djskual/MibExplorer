# Release Notes
## Added
- Added Reboot MIB action to the Actions menu.
- Execute sync before sending the RCC reboot command to save pending changes.
- Added a confirmation dialog to prevent accidental reboots.
- Automatically disconnect the SSH session after initiating the reboot.
- Added optional automatic Wi-Fi hotspot and SSH reconnection after MIB reboot (enabled by default, with a 3-minute timeout and cancellation).
- Added support for file transfers to RCC temporary memory (`/net/rcc/dev/shmem`), while preserving existing system partition protections.

## Improved
- Improved reboot behavior when Windows Wi-Fi location access is unavailable: offer Location settings, reboot without reconnect, or cancel.

## Fixed
- Fixed Qnet symbolic link resolution between MMX and RCC, preventing incorrect directory navigation.
- Fixed RCC temporary directory resolution (`/net/rcc/tmp` → `/net/rcc/dev/shmem`).
- Fixed stale file listings when directory loading fails.
- Fixed file explorer navigation to correctly preserve the target node and display resolved paths.
- Improved file browser responsiveness by moving remote directory listings and SSH connection checks off the UI thread.
- Optimized folder tree expansion by reusing directory listings and reducing unnecessary SSH operations.

## Cleanup
