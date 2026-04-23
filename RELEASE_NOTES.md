# Release Notes

## Added
- Added **manifest v2** support for Official scripts
  - Official scripts now use structured manifest entries:
    - `Name`
    - `Version`
    - `Sha256`

- Added script version support
  - Script headers now support `Version: x.y.z`
  - Script Center parses script version metadata
  - Runtime and design mode now share the same header parsing behavior

- Added `Generate-ScriptManifest.ps1`
  - Generates `Scripts/Official/manifest.json`
  - Reads versions from script headers
  - Computes SHA-256 for single scripts
  - Computes stable package SHA-256 for package scripts

- Added automatic official update detection
  - Script Center checks for Official script updates when opened
  - `Update Official` now shows a visual indicator when updates are available
  - Offline checks fail silently without interrupting the user

## Improved
- Improved Official script update workflow
  - Uses remote/local manifest comparison
  - Downloads only new or changed scripts
  - Uses SHA-256 to detect real script changes
  - Keeps `Custom` scripts untouched

- Improved Official scripts synchronization
  - `Official` now behaves as a managed mirror of the remote repository
  - Local Official scripts removed from the remote manifest are removed locally too

- Improved Script Center metadata handling
  - Headers now follow:
    - `Type`
    - `Version`
    - description lines
  - Better foundation for future changelog/version display

- Improved update indicator layout
  - Cleaner placement on the `Update Official` button
  - Stable button height and alignment

## Fixed
- Fixed Official script updates being downloaded unnecessarily
- Fixed missing local manifest copy issues
- Fixed Official script removal behavior after remote manifest changes
- Fixed update indicator positioning in Script Center
- Fixed Script Center header parsing to support versioned scripts

## Cleanup
- Updated Official scripts to include version headers
- Updated template package documentation
- Added tooling foundation for safer Official script releases
- Prepared Script Center for future script version/changelog support