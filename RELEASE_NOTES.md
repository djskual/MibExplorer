# Release Notes

## Added
- New **SVM Fix Script Center package**
  - Fully offline SVM fix (no ODIS required)
  - Automatic generation of:
    - `CfgAck`
    - `SvmInfo`
  - Uses integrated `svmtool` binary
  - Direct apply on MIB (no SD dependency)

- Scripts are now included directly in the release
  - Available in `Scripts/` folder next to the executable
  - Ready to use with Script Center

- New **Script Center SDK**
  - Lightweight helper library for script development (`mibexplorer_sdk.sh`)
  - Standardized logging, execution flow, and file handling
  - QNX-compatible utilities

- New **Template Script Package**
  - Ready-to-use structure for creating Script Center packages
  - Includes SDK integration and usage examples

- Script metadata support (header-based)
  - Script type parsed from first comment line (`Type: ReadOnly`, `Apply`, `Dangerous`)
  - Script description parsed from following comment lines
  - Visual type badge displayed in Script Center UI

## Improved
- Reorganized Scripts structure:
  - Production-ready scripts are now at the root of `Scripts/`
  - Example scripts moved to `Scripts/Examples/`
  - Added SDK template inside Examples
  - Clear separation between usable tools and development samples

- Script development workflow:
  - Easier creation of compatible scripts
  - Reduced risk of errors on MIB
  - Consistent structure across scripts

- Script Center UI:
  - Cleaner script model (removed unused fields)
  - Added script type display in list and details panel
  - Improved readability and script identification

- Release packaging:
  - Automatic inclusion of Script Center packages
  - Example scripts are excluded from release artifacts

## Notes
- After applying SVM fix, DTC may still appear active in some tools
  - This is due to diagnostic session caching (e.g. ODIS)
  - Clearing DTC or reconnecting to the ECU refreshes the state

- Scripts are executed with root privileges on the MIB
  - Use with caution

- If you create useful scripts and would like to share them, feel free to submit a PR by adding them to the `Scripts/` folder  
  We welcome community contributions and may include selected scripts in future releases

- The Script Center SDK is optional but recommended for all new scripts