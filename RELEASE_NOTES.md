# Release Notes

## 🚀 Script Center Release

This version introduces the first stable implementation of the **Script Center**, enabling direct execution of custom scripts on the MIB over SSH with a fully integrated workflow.

---

## ✨ Added

- **Script Center (stable release)**
  - Run scripts directly on the MIB via SSH
  - Automatic upload, execution, and cleanup
  - Integrated execution log viewer
  - Support for script packages (run.sh + binaries)

- **Copy Log button**
  - Quickly copy execution logs to clipboard
  - Ideal for debugging and sharing results

---

## 🔧 Improved

- Execution log clarity
  - Added remote workspace path information
  - More readable and structured output

- Status feedback
  - Displays currently running script name

- File extraction reliability
  - Improved handling of QNX-specific file types
  - Unknown entries are now properly downloaded
  - No more missing files during recursive extraction

- Overall Script Center stability and UX polish

---

## 🐛 Fixed

- Large folder download issues
  - Fixed incomplete extraction caused by unhandled entry types

- Remote listing inconsistencies on QNX systems

---

## 🧹 Cleanup

- Internal refactoring of Script Center commands and state handling
- Minor UI adjustments for better usability

---

## 💡 Notes

Script Center is now considered **stable and production-ready**.

It provides a solid foundation for advanced workflows such as:
- MIB diagnostics
- SVM operations
- Custom tooling deployment

---

Next steps will focus on:
- deeper integration with advanced tools
- further automation capabilities