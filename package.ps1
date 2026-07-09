# Relativity 릴리즈 zip을 만드는 Windows 패키징 스크립트 (Release DLL 빌드 후 GameData만 압축)
#requires -Version 5.1
[CmdletBinding()]
param(
    # Version label used in the output zip name. Keep in sync with src/Relativity.csproj <Version> and the CHANGELOG.
    [string]$Version = "0.1.0-beta"
)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "==> dotnet build -c Release" -ForegroundColor Cyan
dotnet build "$repo/src/Relativity.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$dll = Join-Path $repo "GameData/Relativity/Plugins/Relativity.dll"
if (-not (Test-Path $dll)) { throw "DLL not found at $dll — check KSPBT_ModPluginFolder output" }
Write-Host "==> built $((Get-Item $dll).Length) bytes: $dll" -ForegroundColor Green

# Stage a clean copy of GameData/Relativity (drop runtime cruft + editor sources).
$stage = Join-Path $repo "bin/package"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
$dest = Join-Path $stage "GameData/Relativity"
New-Item -ItemType Directory -Path $dest -Force | Out-Null

Copy-Item (Join-Path $repo "GameData/Relativity/*") $dest -Recurse -Force
# Ship the license inside the mod folder so CKAN installs it too.
Copy-Item (Join-Path $repo "LICENSE") (Join-Path $dest "LICENSE") -Force

# Strip things a player must never receive.
Get-ChildItem $dest -Recurse -Force -Include `
    "PluginData", "*.ConfigCache", "*.ConfigSHA", "*.pdb", "*-decompiled", ".DS_Store" |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

$zip = Join-Path $repo "bin/Relativity-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage "GameData") -DestinationPath $zip
Write-Host "==> packaged $zip" -ForegroundColor Green
Write-Host "    Contents:" -ForegroundColor Cyan
Expand-Archive -Path $zip -DestinationPath (Join-Path $repo "bin/_verify") -Force
Get-ChildItem (Join-Path $repo "bin/_verify") -Recurse -File | ForEach-Object { "      " + $_.FullName.Substring((Join-Path $repo 'bin/_verify').Length + 1) }
Remove-Item (Join-Path $repo "bin/_verify") -Recurse -Force
