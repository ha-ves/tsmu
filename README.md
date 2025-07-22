[English](#tyranoscriptmemoryunlocker-eng)
# TyranoScriptMemoryUnlocker

**TyranoScriptMemoryUnlocker (TSMU)** は、TyranoScript製ゲームのCG・回想モードのコンテンツをすべて解放するツールです。ゲームの app.asar ファイルが読み込まれ、解放可能なCG・回想に応じてセーブファイルが更新されます。

### **メモ：**
<u>このツールは、**TyranoScript V400** 以降の特定の [CG & 回想モード機能](https://tyrano.jp/usage/tech/cg) を使用しているゲームでのみ動作します。</u>

## 機能
- TyranoScript製ゲームのCG・回想モードのコンテンツをすべて解放する
- 変更前に元のセーブファイルをバックアップ
- テスト実行モード対応（ファイルを変更せず、実行内容のみ表示）
- 詳細なメッセージ・ログ出力

## 必要環境
- .NET 9 SDK（ソースからビルドする場合）
- ゲームの `app.asar` ファイル（通常は `resources/` フォルダ内）
- ゲームのセーブファイル（通常はゲームのトップ フォルダ内）

## 使い方

### 公開済み実行ファイルの使用
最新の実行ファイルは [Releases ページ](https://github.com/ha-ves/tsmu/releases) からダウンロードできます。

ダウンロードまたはビルドした後、以下のように実行します：

```
tsmu -a <app.asarのパス> -s <save.savのパス> [--dry] [-v|-vv]
```

### dotnet run（ソースから実行）
```
dotnet run --project TyranoScriptMemoryUnlocker \
    -a <app.asarのパス> \
    -s <save.savのパス> [--dry] [-v|-vv]
```

### オプション
- `-a, --asar`   ゲームの「.asar」ファイルへのパス（必須）
- `-s, --sav`    セーブファイルへのパス（必須）
- `--dry`        テスト実行モード（変更なし）
- `-v, -vv`      詳細なメッセージを表示（最大2段階）

## 使用例
```
tsmu -a resources/app.asar -s save.sav
```
---
# ライセンス
このプログラムは「AGPLv3-and-later」ライセンスのもとで提供されています。詳細は [LICENSE](LICENSE) を参照してください。

# 免責事項
**本ソフトウェアは独立したプロジェクトであり、TyranoScriptとは一切関係ありません。TyranoScriptのコードを実行または含んでいません。TyranoScriptによって生成されたファイルを読み取り・更新しますが、TyranoScriptプロジェクトの一部ではありません。**

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
