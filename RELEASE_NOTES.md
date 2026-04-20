# Release Notes

## Added
- Smart auto-scroll ("follow mode") for Script Center execution log
- Smart auto-scroll ("follow mode") for Remote Shell output

## Improved
- Script Center and Remote Shell now behave like real terminals:
  - automatically follow output when at the bottom
  - preserve user scroll position when navigating logs
  - resume follow mode when returning to the bottom
- Improved readability and usability for long-running scripts and sessions

## Fixed
- Fixed Script Center log not updating after script start
- Fixed UI thread marshaling issue causing shell output to stop displaying
- Fixed potential infinite wait when remote shell closed before exit code detection
- Added safe handling of shell output processing to prevent silent failures

## Cleanup
- Improved ScriptExecutionService shell lifecycle management
- Improved reliability of SSH output streaming and UI synchronization