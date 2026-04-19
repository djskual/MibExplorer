# Release Notes

## v0.4.1

### Improved
- Improved SCP upload stability and performance for large folders:
  - Implemented batch upload using a single SCP session
  - Eliminated per-file SCP reconnects
  - Significantly more reliable transfers on QNX targets

### Fixed
- Fixed Script Center cleanup issue:
  - Prevented orphaned /tmp script folders when execution fails
  - Cleanup now correctly targets the actual uploaded path instead of generating a new timestamp

- Fixed unstable behavior during large folder uploads:
  - Removed excessive SCP reconnects causing random transfer failures

### Cleanup
- Improved internal robustness of ScriptExecutionService state handling