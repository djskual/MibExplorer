# Release Notes

## Added
- Introduced structured partial class architecture for `MainViewModel`
- Introduced structured partial class architecture for `SshMibConnectionService`
- Added dedicated internal models container for extract/mapping logic (`ExtractModels`)

## Improved
- Significantly improved code readability and maintainability
- Reduced size and complexity of core files (`MainViewModel`, `SshMibConnectionService`)
- Better separation of responsibilities across features:
  - Connection
  - Explorer
  - Transfer
  - Remote operations
  - Extract
  - Mapping
- Prepared internal architecture for upcoming features:
  - Folder upload (recursive)
  - Mapping replay (dirty names support)
  - Safe folder replace operations

## Fixed
- Restored original Explorer behavior after refactor:
  - TreeView loading correctly restored
  - ListView content population fixed
- Fixed selection state issues affecting "Selected item" UI panel

## Cleanup
- Removed monolithic structure from `MainViewModel`
- Removed monolithic structure from `SshMibConnectionService`
- Eliminated duplicated helper methods
- Reorganized internal extract and mapping models into a dedicated partial file
- Cleaned unused or redundant code paths introduced during early development