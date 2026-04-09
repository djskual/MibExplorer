## Remote Shell Console

A new interactive remote shell console has been added to MibExplorer.

This console connects directly to the MIB using the existing SSH connection and allows executing commands in a persistent remote shell session.

---

### Features

- dedicated remote shell console window
- real persistent SSH shell session (no local emulation)
- interactive command execution
- live stdout / stderr output
- command history navigation (Up / Down)
- auto-scroll on new output
- clear console (Ctrl+L)
- copy all output
- save log to file
- themed context menu integration
- dark theme integration

---

### Improvements

- single console instance (reopen focuses existing window)
- responsive output buffer with automatic trimming
- proper handling of SSH connection loss
- input disabled when connection is unavailable
- improved usability on small screens (independent window behavior)

---

### Notes

This is the first iteration of the shell console.

Further improvements (output rendering, colorization, enhanced terminal behavior) will be introduced in future updates.