# Release Notes
## Added

## Improved
- Changed SSH connection banner from Audi MIB to VW MIB.
- Updated SSH.NET from 2025.1.0 to 2026.0.0, including upstream security fixes.

## Fixed
- Fixed QNX SSH SFTP subsystem by using internal-sftp instead of the external sftp-server.
- Updated all four SCP client constructors to use explicit POSIX/QNX remote-path quoting (`RemotePathTransformation.ShellQuote`).

## Cleanup
- Removed unused external sftp-server binary from SSH payload.
