# Release Notes
## Added

## Improved
- Changed SSH connection banner from Audi MIB to VW MIB.
- Updated SSH.NET from 2025.1.0 to 2026.0.0, including upstream security fixes.
- Add RW/RO mount management for SD partitions sda0 and sdb0.
- Serialize writable operations to prevent conflicting mount changes.
- Validate directory symlink targets before uploading files.

## Fixed
- Fixed QNX SSH SFTP subsystem by using internal-sftp instead of the external sftp-server.
- Updated all four SCP client constructors to use explicit POSIX/QNX remote-path quoting (`RemotePathTransformation.ShellQuote`).
- Preserve the initial RW/RO mount state during remote file operations.
- Allow file uploads through directory symlinks such as /tmp and /root.
- Support direct writes to /tmp and /dev/shmem without remounting.

## Cleanup
- Removed unused external sftp-server binary from SSH payload.
