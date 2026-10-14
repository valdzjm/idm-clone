$ErrorActionPreference = 'Stop'
$manifestPath = Join-Path $PSScriptRoot 'bin\host.json'
if (!(Test-Path -LiteralPath $manifestPath)) { throw 'Run build.ps1 and prepare.mjs first.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$hostName = if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'bin\LocalDownloadHost.v04.exe')) { 'LocalDownloadHost.v04.exe' } elseif (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'bin\LocalDownloadHost.v03.exe')) { 'LocalDownloadHost.v03.exe' } elseif (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'bin\LocalDownloadHost.v02.exe')) { 'LocalDownloadHost.v02.exe' } else { 'LocalDownloadHost.exe' }
$manifest.path = Join-Path $PSScriptRoot "bin\$hostName"
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
if (!(Test-Path -LiteralPath $manifest.path)) { throw 'Native host executable is missing.' }
$registryPath = 'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.localdownload.manager'
New-Item -Path $registryPath -Force | Out-Null
Set-Item -LiteralPath $registryPath -Value $manifestPath
Write-Output 'Registered Local Download Manager for this Windows user. Load the extension folder in Chrome.'
