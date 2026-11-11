# Release Notes
## Added
- Add an integrated interactive xterm-style QNX remote shell with inline input, ANSI rendering, paste, copy and terminal resize.

## Improved
- Embed HTML, JavaScript, CSS, xterm.js and third-party license notices directly in the single-file MibExplorer executable.
- Polish RemoteShell clipboard shortcuts (Ctrl+C/Ctrl+V), context menu, Ctrl+L, and toolbar button order.
- Refine xterm.js terminal scrolling and dark-theme integration.
- Changed SSH connection banner from Audi MIB to VW MIB.
- Updated SSH.NET from 2025.1.0 to 2026.0.0, including upstream security fixes.
- Add RW/RO mount management for SD partitions sda0 and sdb0.
- Serialize writable operations to prevent conflicting mount changes.
- Validate directory symlink targets before uploading files.
- Support directory symlinks across file and folder upload, replace, rename, delete and extraction operations.
- Resolve the actual destination mount before writable operations.
- Preserve existing RW/RO mount behavior when operating through symbolic links.

## Fixed
- Fix WebView2 terminal clipboard integration and the unused black strip below the console.
- Fixed QNX SSH SFTP subsystem by using internal-sftp instead of the external sftp-server.
- Updated all four SCP client constructors to use explicit POSIX/QNX remote-path quoting (`RemotePathTransformation.ShellQuote`).
- Preserve the initial RW/RO mount state during remote file operations.
- Allow file uploads through directory symlinks such as /tmp and /root.
- Support direct writes to /tmp and /dev/shmem without remounting.
- Preserve original file permissions, UID and GID when replacing remote files.
- Resolve symbolic links dynamically for remote file operations.
- Fix Replace, Rename and Delete through symbolic directory paths.
- Prevent accidental replacement of symbolic links to files.
- Fix explorer refresh after renaming or deleting selected folders and symlinks.

## Cleanup
- Remove the one-time terminal asset download script; bundle the local xterm.js assets and licenses with the application.
- Removed unused external sftp-server binary from SSH payload.
