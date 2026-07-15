# Relativity 릴리즈 zip을 만드는 Windows 패키징 스크립트 (Release DLL 빌드 후 GameData만 압축)
#requires -Version 5.1
[CmdletBinding()]
param(
    # Version label used in the output zip name. Keep in sync with src/Relativity.csproj <Version> and the CHANGELOG.
    [string]$Version = "1.1.0"
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
    "PluginData", "*.ConfigCache", "*.ConfigSHA", "*.pdb", "*-decompiled", ".DS_Store", "variants" |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# Release gate in the STAGED copy only: debugMode must never ship enabled, whatever the dev
# working cfg currently says (it gets flipped on and off during test sessions).
$cfgPath = Join-Path $dest "relativity.cfg"
$cfgText = Get-Content $cfgPath -Raw
$patched = $cfgText -replace '(?m)^(\s*debugMode\s*=\s*)true', '${1}false'
if ($patched -ne $cfgText) {
    Set-Content $cfgPath $patched -Encoding utf8 -NoNewline
    Write-Host "==> staged cfg: debugMode forced to false for release" -ForegroundColor Yellow
}

$zip = Join-Path $repo "bin/Relativity-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }

# Zip with forward-slash entry names. PowerShell 5.1's Compress-Archive writes
# backslash separators, which break CKAN and non-Windows extraction — so build the
# archive via .NET, forcing '/' in every entry path.
Add-Type -AssemblyName System.IO.Compression | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null
$stageRoot = (Resolve-Path $stage).Path.TrimEnd('\', '/')
$fs = [System.IO.File]::Open($zip, [System.IO.FileMode]::Create)
$archive = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -Path $stage -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($stageRoot.Length + 1) -replace '\\', '/'   # force '/'
        $entry = $archive.CreateEntry($rel, [System.IO.Compression.CompressionLevel]::Optimal)
        $out = $entry.Open()
        $in = [System.IO.File]::OpenRead($_.FullName)
        try { $in.CopyTo($out) } finally { $in.Dispose(); $out.Dispose() }
    }
} finally {
    $archive.Dispose(); $fs.Dispose()
}
Write-Host "==> packaged $zip" -ForegroundColor Green
Write-Host "    Contents (entry paths — must use '/'):" -ForegroundColor Cyan
$reader = [System.IO.Compression.ZipFile]::OpenRead($zip)
try { $reader.Entries | ForEach-Object { "      " + $_.FullName } } finally { $reader.Dispose() }
