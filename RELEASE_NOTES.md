## 🚗 Major Feature: Coding Center

MibExplorer now includes a fully integrated **Coding Center** for direct Volkswagen MIB 5F long coding management.

This new module combines ODIS-style usability, VCDS-style raw access, and advanced engineering workflows directly inside MibExplorer.

---

## ✨ Added

### Coding Center Core

- Direct 5F long coding read from MIB
- Automatic VIN detection
- Internal coding catalog decoding
- Full long coding visualization
- Persistent per-vehicle coding history

### Editing Modes

#### Features View (ODIS-style)

- Human readable coding options
- Dropdown and checkbox editing
- Current vs new value comparison

#### Raw Coding View (VCDS-style)

- Byte-by-byte coding access
- Bit / multibit decoding
- Direct HEX byte editing
- Direct binary byte editing
- Full synchronization with all other views

#### Changes View

- Live modified long coding generation
- Clear coding diff display
- Byte old/new comparison
- Direct long coding paste/edit support

### Apply / Restore

- Safe full 4101 blob coding write
- Automatic snapshot before Apply
- Snapshot restore system
- Readback verification after write
- Persistence confirmed after reboot

---

## 🛠 Improved

- Better command locking during loading / write operations
- Cleaner status workflow during coding actions
- Better synchronization between editor modes
- Improved overall Coding Center UI layout and spacing

---

## 🔒 Safety

- Full backup-first workflow
- Reversible coding changes
- Verified write/readback logic
- No partial byte writes required

---

## 📦 Result

MibExplorer is now not only a file / shell / script tool, but also a complete standalone MIB coding platform.