## Remote Shell Console

MibExplorer now includes a built-in interactive remote shell console.

The console connects directly to the MIB through the existing SSH connection and runs commands inside a real persistent remote shell session.

---

### Features

- dedicated remote shell console window
- real persistent SSH shell session (no local emulation)
- interactive command execution
- live remote output inside MibExplorer
- command history navigation (Up / Down)
- clear console with Ctrl+L shortcut
- copy all output
- save log to file
- themed context menus
- single console instance management
- independent window behavior for better usability on small screens

---

### Console rendering

- rich formatted shell output
- terminal-like prompt and command flow on the same line
- prompt-aware styling with separate coloring for host and path
- distinct rendering for:
  - prompt
  - typed commands
  - normal output
  - shell errors
  - warnings
  - internal shell status messages
- improved readability with PowerShell-like visual hierarchy adapted to the MibExplorer dark theme

---

### Stability and usability

- proper handling of SSH connection loss
- input automatically disabled when the shell becomes unavailable
- output buffer protection with automatic trimming
- auto-scroll and focus restoration improvements
- copy/save actions remain available with formatted output

---

### Notes

This console is designed as a lightweight integrated remote shell for MIB debugging and file system work.

It is not intended to emulate a full terminal, but it now provides a clean, responsive and readable shell experience directly inside MibExplorer.