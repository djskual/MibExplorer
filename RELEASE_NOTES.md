# MibExplorer v0.1.0

## 🚀 First stable release

This version marks the first stable release of MibExplorer, with a fully functional file explorer over SSH for MIB2 / MIB2.5 systems.

---

## ✨ New features

### 📁 Folder upload with name mapping
- Added support for uploading folders with correct Linux filename restoration
- Uses `.mibexplorer-map.json` generated during extraction
- Ensures full round-trip integrity (extract → modify → re-upload)

### 🧠 Smart mapping generation
- Mapping file is now created **only when needed**
- No unnecessary JSON files for clean folders

### 🖱️ Context menus
- Added right-click support in:
  - TreeView (folders)
  - ListView (files & folders)
- Context-aware actions (auto enable/disable)
- Automatic selection on right-click

---

## 🔧 Improvements

### 🌳 TreeView behavior
- TreeView now stays synchronized with ListView navigation
- Expands and selects correct nodes when navigating from ListView
- Lazy loading added on node expansion (fixes "Loading..." placeholder issue)

### 📂 Explorer refresh
- Fixed selection refresh issues after operations
- Actions now correctly update when selecting items
- No more need to change folder to refresh state

### 🧭 Navigation consistency
- Double-click navigation now fully synced with TreeView
- Cleaner and more predictable navigation flow

### 🧾 Menu improvements
- Menu items aligned with available features
- Added missing actions:
  - Upload folder
  - Replace
- Reorganized menus:
  - `Connection Help` moved to `Help`
- Renamed "Planned actions" → "Actions"

### 🎨 UI fixes
- Fixed TreeView visual regression (restored default WPF style)
- Improved overall UI consistency

---

## 🛠️ Stability

- Safer handling of selection state
- Improved robustness of folder operations
- Better separation between UI state and filesystem operations

---

## 📌 Notes

- `.mibexplorer-map.json` is automatically ignored during upload
- No changes are made to the MIB unless explicitly requested by the user
- All operations remain safe (no unintended filesystem writes)

---

## 🔜 Next steps

- SD card bootstrap (SSH + WiFi auto setup)
- Guided operations (backup, skin install, etc.)
- MibExplorerAgent for enhanced safety and performance

---

## 🙌 Thanks

This release focuses on stability, predictability, and usability — laying the foundation for future advanced features.