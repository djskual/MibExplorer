MibExplorer - project baseline

What is included:
- VS2022 solution (.sln)
- WPF .NET 8 desktop project
- dark theme inspired by McfEditor
- clean project structure (Core / Models / Services / ViewModels / Views / Themes / Assets)
- explorer shell ready for SSH integration
- sample design-time/data-mode remote tree so the UI is immediately usable

Current state:
- SSH is not implemented yet on purpose
- commands are already placed in the UI and command flow
- next step is to replace DesignMibConnectionService with a real SSH-backed service

Recommended next step:
- add SSH.NET (Renci.SshNet)
- create a real SshMibConnectionService implementing IMibConnectionService
- keep the current UI unchanged
