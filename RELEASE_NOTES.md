# Release Notes

## Added
- Official Script integrity system (SHA-256 based)
- Validation before execution of Official scripts
- Detection of locally modified Official scripts
- Targeted restore workflow for Official scripts
- Ability to restore only the affected script/package
- Author metadata support in Script Center (Author: header)
- Display of Author in Script Details panel
- Detailed Official script synchronization log
- Per-script update status:
    - [ADDED]
    - [UPDATED]
    - [REMOVED]
    - [UNCHANGED]
- Version transition display during updates (e.g. v1.0.0 → v1.0.1)

## Improved
- Script Details panel layout (Type / Version / Author)
- Script list tooltip formatting (multi-line, clearer content)
- Official script update transparency with detailed logs
- Script execution safety with clear integrity feedback
- User guidance when a script is modified (restore prompt)

## Fixed
- Crash when running a script after refresh (SelectedScript becoming null)
- Crash when restoring a script after RefreshScripts()
- Incorrect tooltip formatting due to WPF StringFormat behavior

## Cleanup
- Refactored script header parsing (Type / Version / Author / Description)
- Improved Script Center metadata model for future extensions
- Removed unnecessary UI refresh calls causing state loss
