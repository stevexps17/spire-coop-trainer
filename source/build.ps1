param([string]$GameBin='C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64')
$ErrorActionPreference='Stop'
$dest=Join-Path $PSScriptRoot '..\HostGold.dll'
$refs=@('System.Private.CoreLib','System.Runtime','System.Collections','System.Linq','System.Console','System.Threading','System.Threading.Tasks','System.IO','System.IO.FileSystem','System.Security.Cryptography','System.Runtime.Extensions','System.Reflection','System.Text.Json','System.Memory','GodotSharp','sts2','0Harmony')
$args=@('-nologo','-target:library','-langversion:10','-nullable:disable','-nostdlib+',('-out:'+$dest))
$args+= $refs | ForEach-Object { '-r:'+(Join-Path $gameBin ($_+'.dll')) }
$args+=Join-Path $PSScriptRoot 'HostGold.cs'
$args+=Join-Path $PSScriptRoot 'Browser.cs'
$args+=Join-Path $PSScriptRoot 'Settings.cs'
$args+=Join-Path $PSScriptRoot 'Managers.cs'
$args+=Join-Path $PSScriptRoot 'Localization.cs'
$args+=Join-Path $PSScriptRoot 'Targets.cs'
$args+=Join-Path $PSScriptRoot 'Art.cs'
$args+=Join-Path $PSScriptRoot 'Resize.cs'
$args+=Get-ChildItem (Join-Path $PSScriptRoot "i18n") -Filter "*.json" | ForEach-Object { "-resource:"+$_.FullName+",HostGold.i18n."+$_.Name }
$sdkLine = & dotnet --list-sdks | Select-Object -Last 1
if (!$sdkLine -or $sdkLine -notmatch '^([^ ]+) \[(.+)\]$') { throw 'Install a .NET SDK first.' }
$compiler = Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn/bincore/csc.dll'
& dotnet $compiler @args
if($LASTEXITCODE -ne 0){throw 'Compilation failed'}



