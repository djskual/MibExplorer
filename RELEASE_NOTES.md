## ✨ Major Feature – Remote File Editor & Advanced Diff Viewer

This release introduces a **complete remote file editing workflow**, including a powerful **side-by-side diff viewer**.

---

## 🧑‍💻 Remote File Editor (New)

MibExplorer now allows you to **open, edit and save files directly on the MIB over SSH**.

### Features

* Open files from the explorer (double-click or context menu)
* Full text editing over SSH
* Explicit save with overwrite
* Atomic save (temporary file + replace)
* RW mount handling for writable paths
* Read-only mode for protected areas
* Reload file support
* Unsaved changes protection (Save / Discard / Cancel)

---

## 🔍 Advanced Diff Viewer (New)

Before saving changes, you can now **compare the original file with your modifications**.

### Capabilities

* Side-by-side comparison (Original vs Current)
* Line-level and token-level diff
* Accurate detection of:

  * additions
  * deletions
  * modifications
* Smart alignment using LCS-based algorithm
* Automatic merging of similar add/remove pairs into modifications
* Git-like grouping of repeated change markers
* Navigation between differences
* Collapsible unchanged sections

---

## 🎛️ Diff Options

* Ignore whitespace changes
* Show invisible characters (spaces, tabs)
* Collapse unchanged sections

---

## 📏 Whitespace & Tab Handling

Major improvements to whitespace rendering:

* Unified tab handling across editor and diff
* Consistent tab size (4 spaces)
* Correct visual alignment for column-based text
* No mismatch between editor and diff rendering

---

## 🖥️ UI / UX Improvements

* Editor and diff windows are independent
* Loading indicator with animation
* Improved status feedback (loading / ready)
* Better visual consistency across views

---

## ✅ Result

This update introduces a **safe, reliable and fully visual editing workflow**:

* edit files directly on the MIB
* validate changes before saving
* trust the visual alignment and diff accuracy

---

⚠️ Modifying MIB files can be risky. Always verify changes before applying them.
