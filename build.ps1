# Builds GH3 Launcher, and with -Package its installer, into dist\.
#
#   powershell -NoProfile -File build.ps1 [-Game <GH3 folder>]
#       dist\GH3 Launcher.exe; with -Game it is also copied there (close the launcher first).
#   powershell -NoProfile -File build.ps1 -Package -Game <GH3 folder>
#       dist\GH3 Launcher Setup.exe, bundling from that folder: Honeycomb-GUI\ (without its owner's
#       own data: QBDebug\keys_user*.txt, Backups\) with its ffmpeg/ffprobe/ffplay, and
#       x360ce_x86.exe + xinput1_3.dll. x360ce.ini is the blank one in installer\.
#
# Needs the .NET 10 SDK and a Honeycomb-GUI folder to compile against (-Honeycomb <folder>, else
# the game's, else ..\..\Honeycomb-GUI). The InputPlus plugin is rebuilt when Visual Studio's C++
# tools are installed; otherwise plugin\InputPlus.dll is used as it is.
param([string]$Game = '', [switch]$Package, [string]$Honeycomb = '')
$ErrorActionPreference = 'Continue'
$here = $PSScriptRoot
function Fail($m) { Write-Host "ERROR: $m" -ForegroundColor Red; exit 1 }
$dist = Join-Path $here 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
if ($Game) { $Game = (Resolve-Path $Game).Path.TrimEnd('\') }

# Honeycomb to compile against (at run time the launcher loads it from the game folder)
if (-not $Honeycomb) {
    $Honeycomb = if ($Game -and (Test-Path (Join-Path $Game 'Honeycomb-GUI\GH-Toolkit-Core.dll'))) { Join-Path $Game 'Honeycomb-GUI' } else { Join-Path $here '..\..\Honeycomb-GUI' }
}
if (-not (Test-Path (Join-Path $Honeycomb 'GH-Toolkit-Core.dll'))) { Fail 'Honeycomb-GUI not found (GH-Toolkit-Core.dll): pass -Honeycomb <folder>' }
$Honeycomb = (Resolve-Path $Honeycomb).Path

# 1. the plugin (needs Visual Studio's C++ tools; the last built dll is kept otherwise)
if (Test-Path "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe") {
    & cmd /c "`"$here\plugin\build.cmd`"" | Out-Null
    if ($LASTEXITCODE) { Fail "plugin build ($LASTEXITCODE)" }
} elseif (-not (Test-Path (Join-Path $here 'plugin\InputPlus.dll'))) { Fail 'no Visual Studio C++ and no plugin\InputPlus.dll' }

# 2. the icon (drawn by make_icon.ps1: nothing of the game)
if (-not (Test-Path (Join-Path $here 'host\app.ico'))) { & (Join-Path $here 'make_icon.ps1') }
Copy-Item (Join-Path $here 'host\app.ico') (Join-Path $here 'installer\app.ico') -Force

# 3. the launcher: one self-contained exe (the song tool is inside it)
$out = Join-Path $here 'host\bin\publish'
& dotnet publish (Join-Path $here 'host\GH3Launcher.csproj') -c Release -o $out --nologo -v q "-p:HoneycombDir=$Honeycomb"
if ($LASTEXITCODE) { Fail "launcher publish ($LASTEXITCODE)" }
$launcher = Join-Path $dist 'GH3 Launcher.exe'
Copy-Item (Join-Path $out 'GH3 Launcher.exe') $launcher -Force
Write-Host ("launcher: {0} ({1:N1} MB)" -f $launcher, ((Get-Item $launcher).Length / 1MB))

if ($Game -and -not $Package) {
    $dest = Join-Path $Game 'GH3 Launcher.exe'
    $running = Get-Process -Name 'GH3 Launcher' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $dest }
    if ($running) { Fail 'close GH3 Launcher before building' }
    Copy-Item $launcher $dest -Force
    Write-Host "copied to $dest"
}
if (-not $Package) { exit 0 }
if (-not $Game) { Fail '-Package needs -Game <GH3 folder> (Honeycomb-GUI, FFmpeg and x360ce are taken from it)' }

# 4. the installer's own exe
$sout = Join-Path $here 'installer\bin\publish'
& dotnet publish (Join-Path $here 'installer\GH3LauncherSetup.csproj') -c Release -o $sout --nologo -v q
if ($LASTEXITCODE) { Fail "installer publish ($LASTEXITCODE)" }
$stub = Join-Path $sout 'GH3 Launcher Setup.exe'

# 5. what it installs
$stage = Join-Path $dist 'payload'
if (Test-Path $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $stage 'x360ce'), (Join-Path $stage 'Licenses') | Out-Null
Copy-Item $launcher $stage
$hcSrc = Join-Path $Game 'Honeycomb-GUI'
foreach ($f in 'GH-Toolkit-Core.dll', 'ffmpeg.exe', 'ffprobe.exe', 'ffplay.exe') { if (-not (Test-Path (Join-Path $hcSrc $f))) { Fail "missing $hcSrc\$f" } }
& robocopy $hcSrc (Join-Path $stage 'Honeycomb-GUI') /E /NFL /NDL /NJH /NJS /NP /XD Backups /XF keys_user.txt 'keys_qs_user*.txt' '*.pdb' 'GH3LauncherSongTool.*' | Out-Null
if ($LASTEXITCODE -ge 8) { Fail "robocopy Honeycomb-GUI ($LASTEXITCODE)" }
foreach ($f in 'x360ce_x86.exe', 'xinput1_3.dll') {
    if (-not (Test-Path (Join-Path $Game $f))) { Fail "missing $Game\$f" }
    Copy-Item (Join-Path $Game $f) (Join-Path $stage 'x360ce')
}
# the dll must be x360ce's own (other xinput1_3.dll, like BetterGH3's "Force all as guitar", map pads differently)
$xi = (Get-Item (Join-Path $Game 'xinput1_3.dll')).VersionInfo
if ($xi.ProductName -notlike '*Controller Emulator*') { Fail "$Game\xinput1_3.dll is not x360ce's (put x360ce's back first)" }
Write-Host ("x360ce: app {0}, xinput1_3.dll {1}" -f (Get-Item (Join-Path $Game 'x360ce_x86.exe')).VersionInfo.FileVersion, $xi.FileVersion)
Copy-Item (Join-Path $here 'installer\x360ce.ini') (Join-Path $stage 'x360ce')
Copy-Item (Join-Path $here 'LICENSE') (Join-Path $stage 'Licenses\LICENSE.txt')
Copy-Item (Join-Path $here 'THIRD-PARTY-NOTICES.md') (Join-Path $stage 'Licenses\THIRD-PARTY-NOTICES.txt')
Copy-Item (Join-Path $here 'licenses\*.txt') (Join-Path $stage 'Licenses')

# 6. zip it and append it to the installer: [exe][zip]["GH3LPAY1"][offset][length]
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $dist 'payload.zip'
if (Test-Path $zip) { Remove-Item -LiteralPath $zip -Force }
Write-Host 'compressing the payload...'
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)
$setup = Join-Path $dist 'GH3 Launcher Setup.exe'
$fs = [IO.File]::Create($setup)
try {
    $in = [IO.File]::OpenRead($stub); $in.CopyTo($fs); $in.Dispose()
    $offset = $fs.Position
    $in = [IO.File]::OpenRead($zip); $in.CopyTo($fs); $length = $fs.Position - $offset; $in.Dispose()
    $trailer = New-Object byte[] 24
    [Text.Encoding]::ASCII.GetBytes('GH3LPAY1').CopyTo($trailer, 0)
    [BitConverter]::GetBytes([long]$offset).CopyTo($trailer, 8)
    [BitConverter]::GetBytes([long]$length).CopyTo($trailer, 16)
    $fs.Write($trailer, 0, 24)
} finally { $fs.Dispose() }
Remove-Item -LiteralPath $zip -Force
Remove-Item -LiteralPath $stage -Recurse -Force
Write-Host ("installer: {0} ({1:N1} MB)" -f $setup, ((Get-Item $setup).Length / 1MB))
