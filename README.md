[English](#tyranoscriptmemoryunlocker-eng)
# TyranoScriptMemoryUnlocker
[![Build](https://img.shields.io/github/actions/workflow/status/ha-ves/tsmu/release.yml)](https://github.com/ha-ves/tsmu/actions/workflows/release.yml)
[![Release](https://img.shields.io/github/v/release/ha-ves/tsmu?include_prereleases)](https://github.com/ha-ves/tsmu/releases)
[![Downloads](https://img.shields.io/github/downloads/ha-ves/tsmu/total)](https://github.com/ha-ves/tsmu/releases)
[![Last commit](https://img.shields.io/github/last-commit/ha-ves/tsmu)](https://github.com/ha-ves/tsmu/commits)
[![License: AGPL-3.0-or-later](https://img.shields.io/badge/License-AGPL--3.0--or--later-blue.svg)](https://www.gnu.org/licenses/agpl-3.0)

**TyranoScriptMemoryUnlocker (TSMU)** �́ATyranoScript���Q�[����CG�E��z���[�h�̃R���e���c�����ׂĉ������c�[���ł��B�Q�[���� app.asar �t�@�C�����ǂݍ��܂�A����\��CG�E��z�ɉ����ăZ�[�u�t�@�C�����X�V����܂��B

### **�����F**
<u>���̃c�[���́A**TyranoScript V400** �ȍ~�̓���� [CG & ��z���[�h�@�\](https://tyrano.jp/usage/tech/cg) ���g�p���Ă���Q�[���ł̂ݓ��삵�܂��B</u>

## �@�\
- TyranoScript���Q�[����CG�E��z���[�h�̃R���e���c�����ׂĉ������
- �ύX�O�Ɍ��̃Z�[�u�t�@�C�����o�b�N�A�b�v
- �e�X�g���s���[�h�Ή��i�t�@�C����ύX�����A���s���e�̂ݕ\���j
- �ڍׂȃ��b�Z�[�W�E���O�o��

## �K�v��
- .NET 9 SDK�i�\�[�X����r���h����ꍇ�j
- �Q�[���� `app.asar` �t�@�C���i�ʏ�� `resources/` �t�H���_���j
- �Q�[���̃Z�[�u�t�@�C���i�ʏ�̓Q�[���̃g�b�v �t�H���_���j

## �g����

### ���J�ςݎ��s�t�@�C���̎g�p
�ŐV�̎��s�t�@�C���� [Releases �y�[�W](https://github.com/ha-ves/tsmu/releases) ����_�E�����[�h�ł��܂��B

�_�E�����[�h�܂��̓r���h������A�ȉ��̂悤�Ɏ��s���܂��F

```
tsmu -a <app.asar�̃p�X> -s <save.sav�̃p�X> [--dry] [-v|-vv]
```

### dotnet run�i�\�[�X������s�j
```
dotnet run --project TyranoScriptMemoryUnlocker \
    -a <app.asar�̃p�X> \
    -s <save.sav�̃p�X> [--dry] [-v|-vv]
```

### �I�v�V����
- `-a, --asar`   �Q�[���́u.asar�v�t�@�C���ւ̃p�X�i�K�{�j
- `-s, --sav`    �Z�[�u�t�@�C���ւ̃p�X�i�K�{�j
- `--dry`        �e�X�g���s���[�h�i�ύX�Ȃ��j
- `-v, -vv`      �ڍׂȃ��b�Z�[�W��\���i�ő�2�i�K�j

## �g�p��
```
tsmu -a resources/app.asar -s save.sav
```
---
# ���C�Z���X
���̃v���O�����́uAGPLv3-and-later�v���C�Z���X�̂��ƂŒ񋟂���Ă��܂��B�ڍׂ� [LICENSE](LICENSE) ���Q�Ƃ��Ă��������B

# �Ɛӎ���
**�{�\�t�g�E�F�A�͓Ɨ������v���W�F�N�g�ł���ATyranoScript�Ƃ͈�؊֌W����܂���BTyranoScript�̃R�[�h�����s�܂��͊܂�ł��܂���BTyranoScript�ɂ���Đ������ꂽ�t�@�C����ǂݎ��E�X�V���܂����ATyranoScript�v���W�F�N�g�̈ꕔ�ł͂���܂���B**

---

# TyranoScriptMemoryUnlocker Eng

**TyranoScriptMemoryUnlocker (TSMU)** is a utility for unlocking all CGs and replay scenes in save files of games built with TyranoScript. It works by analyzing the game's `app.asar` archive and updating the save file to mark all unlockable content as unlocked.

### **Note:**
<u>This tool only works for games using the specific [CG & Memory Mode feature](https://tyranoscript.com/usage/tech/cg) on **TyranoScript V400** or later.</u>

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
Download the latest executable from the [Releases page](https://github.com/ha-ves/tsmu/releases).

After downloading or building, run:

```
tsmu -a <path-to-app.asar> -s <path-to-save.sav> [--dry] [-v|-vv]
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
tsmu -a resources/app.asar -s save.sav
```
---
# License
This program is licensed under the GNU Affero General Public License v3 or later. See [LICENSE](LICENSE).

# Disclaimer
**This software is an independent project and is not affiliated with, endorsed by, or sponsored by TyranoScript or its creators. It does not execute or include TyranoScript code. It reads and updates files produced by TyranoScript but is not part of the TyranoScript project.**
