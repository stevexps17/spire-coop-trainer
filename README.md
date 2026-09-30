# Spire Co-op Trainer · 尖塔联机修改器

A host-side Slay the Spire 2 mod for granting gold, cards and relics outside combat.
杀戮尖塔 2 单人及联机房主工具，支持非战斗状态下增加金币、卡牌和遗物。

## Features / 功能
- Choose one or multiple recipients / 单选或多选生效玩家
- Search cards and filter by class / 卡牌搜索与职业筛选
- Remove one card from your own deck using the native selector (experimental) / 原生选牌删牌，仅自己（测试）
- Draggable, resizable panel and configurable icon, hotkey and scaling / 可拖动、缩放的面板与图标设置
- Optional ModConfig, RitsuLib and JmcModLib integration
- 16 languages following the game language

English · 简体中文 · 繁體中文 · Deutsch · Español (Latinoamérica) · Français · Bahasa Indonesia · Italiano · 日本語 · 한국어 · Polski · Português (Brasil) · Русский · Español (España) · ไทย · Türkçe

## Compatibility / 兼容性
Version 0.7.0 targets Steam public-beta v0.111.0 and checks the exact game assembly hash. Other builds may be disabled. Multiplayer grants and card removal are experimental; live multiplayer verification is incomplete.
当前版本针对 Steam public-beta v0.111.0，校验游戏文件版本。其他版本可能禁用功能。联机发放及删牌仍处于测试阶段，尚未完成充分的实机联机验证。

## Build / 构建
Install PowerShell, a .NET SDK, and a legally obtained local copy of the supported game. Game binaries and assets are not distributed in this repository.
需要 PowerShell、.NET SDK 和受支持版本的本地游戏。仓库不包含游戏程序或素材。

```powershell
./source/build.ps1 -GameBin 'D:/SteamLibrary/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64'
```

The script produces `HostGold.dll` beside `HostGold.json`. Copy these two files into one `mods/HostGold` directory, then restart the game. Avoid duplicate installations with the same mod ID.
构建后将 HostGold.dll 和 HostGold.json 放入同一个 mods/HostGold 目录并重启游戏。避免重复安装同 ID 模组。

See [使用说明](使用说明.md) and [支持语言](支持语言.md) for details.

## Experimental combat assists / 战斗辅助（测试）
Select recipients, then enable Auto Energy or Life Protection independently. Energy below 3 is replenished to 10 during idle player-action phases (affects X-cost cards). Life Protection replenishes native Buffer, not absolute invincibility or healing. Disable stops replenishment; already granted effects remain. New runs clear selections. Errors/timeouts disable assists for the rest of the run. Live multiplayer execution is not yet verified.
选择玩家后分别启用自动补能或生命保护。低于3能量时在玩家操作阶段空闲时补到10（影响X费牌）；保护补充原版缓冲，不保证绝对不死，也不回血。关闭只停止补充，已有效果保留；新对局清空开关。错误或超时后本局停用。尚未实机联机验证。
