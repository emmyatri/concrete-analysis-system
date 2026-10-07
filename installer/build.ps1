<#
  Builds the Vulcan CBAS installer from a fresh checkout (SCRUM-116). One command, from the repo root:

      powershell -ExecutionPolicy Bypass -File installer\build.ps1

  Output: installer\output\VulcanCBAS-Setup-<version>.exe
  Needs: Node.js and the .NET 10 SDK (see Prerequisites in the README). Installs Inno Setup if it's missing.
#>
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # downloads are ~10x faster without PowerShell's progress bar
$root = Split-Path $PSScriptRoot -Parent

function Step([string]$text) { Write-Host "`n==> $text" -ForegroundColor Cyan }

# Runs a program in a folder and stops the script if it fails.
function Invoke-In([string]$folder, [string]$program, [string[]]$arguments) {
  Push-Location $folder
  try {
    & $program @arguments
    if ($LASTEXITCODE -ne 0) { throw "$program $($arguments -join ' ') failed (exit code $LASTEXITCODE)" }
  } finally { Pop-Location }
}

function Find-Iscc {
  @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
}

Step 'Checking tools'
foreach ($tool in 'node', 'npm', 'dotnet') {
  if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
    throw "$tool isn't installed. See Prerequisites in the README."
  }
}

Step '1/5 Building the screens (Angular)'
$npmCommand = if (Test-Path "$root\src\Vulcan.Web\node_modules") { 'install' } else { 'ci' }
Invoke-In "$root\src\Vulcan.Web" 'npm' @($npmCommand)
Invoke-In "$root\src\Vulcan.Web" 'npm' @('run', 'build')

Step '2/5 Publishing the app with its own .NET runtime'
Invoke-In $root 'dotnet' @('publish', 'src/Vulcan.Desktop', '-p:PublishProfile=win-x64-selfcontained')

Step '3/5 Offline WebView2 runtime (bundled in the installer)'
$webView2 = "$PSScriptRoot\redist\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"
if ((Test-Path $webView2) -and (Get-Item $webView2).Length -gt 50MB) {
  Write-Host 'Already downloaded.'
} else {
  New-Item -ItemType Directory -Force (Split-Path $webView2) | Out-Null
  Invoke-WebRequest 'https://go.microsoft.com/fwlink/?linkid=2124701' -OutFile $webView2
  if ((Get-Item $webView2).Length -lt 50MB) { throw 'The WebView2 download looks wrong. Check the link in this script.' }
}

Step '4/5 Inno Setup'
$iscc = Find-Iscc
if (-not $iscc) {
  winget install --id JRSoftware.InnoSetup -e --accept-source-agreements --accept-package-agreements
  $iscc = Find-Iscc
  if (-not $iscc) { throw 'Inno Setup could not be installed. Install it from https://jrsoftware.org/isdl.php and run this again.' }
}
Write-Host "Using $iscc"

Step '5/5 Building the installer'
Invoke-In $PSScriptRoot $iscc @('vulcan.iss')

$setup = Get-ChildItem "$PSScriptRoot\output\*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host "`nDone: $($setup.FullName)" -ForegroundColor Green
