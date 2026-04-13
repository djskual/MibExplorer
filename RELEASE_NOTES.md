### ✨ New — Inline Diff (Editor)

MibExplorer now includes a **fully integrated inline diff system** directly inside the remote file editor.

This brings a VS Code-like experience for reviewing changes without leaving the editor.

#### Features

* Line-level highlighting (added / modified)
* Inline token-level diff highlighting
* Dedicated left gutter markers column
* Fully synchronized with the diff engine
* Toggle inline diff on/off instantly

#### Visual Improvements

* Consistent color scheme with the diff viewer
* Clear separation between content and markers
* Improved readability for large files

---

### 🧭 New — Diff Navigation

Navigate between changes directly inside the editor.

#### Features

* Previous / Next controls
* Diff position indicator (`n / total`)
* Smart navigation behavior:

  * follows caret position
  * correct behavior between diffs
  * cyclic navigation

---

### 🛠️ Improvements — Editor Behavior

* Fixed horizontal scrolling issues
* Removed artificial padding hacks
* Correct caret positioning (End key & mouse clicks)
* Improved trackpad scrolling behavior
* Stable rendering with long lines and large files

---

### 🔍 Diff Engine Enhancements

* Reuse of diff segments inside the editor
* Extended segment model with source offsets
* Fully backward compatible with existing diff viewer

---

### 🧱 Project Structure Improvements

Improved internal structure for better readability and maintainability.

#### Views

* Introduced a dedicated `FileEditor` module
* Moved:

  * FileEditorWindow
  * FileDiffWindow
* Improved separation of concerns

#### Services

Reorganized into clear submodules:

* `Connection`
* `Shell`
* `Packages`
* `Security`
* `Network`

#### Dialogs

* Extracted MessageBox system into a dedicated folder

---

### 🎯 Result

* Cleaner architecture
* Improved developer experience
* Better feature separation
* More scalable project structure
