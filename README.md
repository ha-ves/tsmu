# TyranoScriptMemoryUnlocker

**TyranoScriptMemoryUnlocker (TSMU)** is a utility for unlocking all CGs and replay scenes in save files of games built with TyranoScript. It works by analyzing the game's `app.asar` archive and updating the save file to mark all unlockable content as unlocked.

### **Note:**
This tool only works for games using the specific [CG & Memory Mode features](https://tyrano.jp/usage/tech/cg) on **TyranoScript V400** or later.

## Features
- Unlock all CGs and replay scenes in TyranoScript game save files
- Backs up your original save file before making changes
- Supports dry-run mode (shows what would be done, without modifying files)
- Verbose logging for debugging and traceability

## Requirements
- .NET 9 SDK (for building from source)
- The game's `app.asar` file (usually in the `resources/` folder)
- The game's save file (usually in the game root folder)

## Usage

### Using the Published Executable
After downloading or building the executable (e.g. `TyranoScriptMemoryUnlocker.exe`):

```
TyranoScriptMemoryUnlocker.exe -a <path-to-app.asar> -s <path-to-save.sav> [--dry] [-v|-vv]
```

### Using dotnet run (from source)
```
dotnet run --project TyranoScriptMemoryUnlocker \
    -a <path-to-app.asar> \
    -s <path-to-save.sav> [--dry] [-v|-vv]
```

### Options
- `-a, --asar`   Path to the app.asar file (required)
- `-s, --sav`    Path to the save file (required)
- `--dry`        Dry run mode (no changes made)
- `-v, -vv`      Increase verbosity (up to 2 levels)

## Example
```
TyranoScriptMemoryUnlocker.exe -a resources/app.asar -s save.sav
```
---
# License
This program is licensed under the GNU Affero General Public License v3 or later. See [LICENSE](LICENSE).

# Disclaimer
**This software is an independent project and is not affiliated with, endorsed by, or sponsored by TyranoScript or its creators. It does not execute or include TyranoScript code. It reads and updates files produced by TyranoScript but is not part of the TyranoScript project.**
