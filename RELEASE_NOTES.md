# Release Notes v0.4.4

## 🚀 Script Center major update

### 🧩 Official script distribution system

- Added dynamic **official script download system**
- Scripts are now fetched directly from GitHub (no longer bundled with the app)
- Introduced `manifest.json` to manage available scripts
- New **"Update Official"** button in Script Center

👉 This allows:
- instant script updates without releasing a new app version
- easier script sharing and maintenance
- cleaner application releases

---

### 📁 Scripts structure redesign

- Scripts are now organized into:
  - `Scripts/Official/` → managed by MibExplorer
  - `Scripts/Custom/` → user scripts
- Examples moved to `Scripts/Examples/`
- Release packages no longer include scripts

---

### 🎨 Script Center UI overhaul

- Improved script list readability and layout
- Added **official script indicator (🛡)**
- Long script names now use **ellipsis trimming**
- Removed horizontal overflow issues

---

### 🧠 Script type visual system

Script types are now represented with compact icons:

- 👁 ReadOnly
- ⚙ Apply
- ⚠ Dangerous
- • Unknown

- Added tooltips for better clarity
- Full type label still visible in details panel

---

### 🧰 Script Center SDK improvements

- Improved script template structure
- Added helper utilities for script creation
- Better documentation for contributors

---

### 🧼 Cleanup

- Removed scripts from release package
- Improved overall Script Center maintainability
- Reduced UI clutter and improved consistency

---

## ⚠️ Notes

- Scripts are now downloaded on demand
- Internet connection required for updating official scripts
- Existing custom scripts remain fully supported

---

## 💬 Contribution

You can now contribute scripts easily:

- Add scripts to the repository
- Submit a PR
- They will be available instantly via Script Center

---

This update significantly improves Script Center flexibility, usability, and long-term maintainability.