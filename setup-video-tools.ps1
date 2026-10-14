$ErrorActionPreference = 'Stop'
$toolsRoot = Join-Path $PSScriptRoot 'bin\tools'
New-Item -ItemType Directory -Path $toolsRoot -Force | Out-Null
$release = Invoke-RestMethod -Uri 'https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest'
$asset = $release.assets | Where-Object name -EQ 'yt-dlp.exe'
$checksumAsset = $release.assets | Where-Object name -EQ 'SHA2-256SUMS'
if (!$asset -or !$checksumAsset) { throw 'Official release assets were not found.' }
$checksumPath = Join-Path $toolsRoot 'SHA2-256SUMS'
Invoke-WebRequest -UseBasicParsing -Uri $checksumAsset.browser_download_url -OutFile $checksumPath
$exe = Join-Path $toolsRoot 'yt-dlp.exe'
if (!(Test-Path -LiteralPath "$exe.download")) { Invoke-WebRequest -UseBasicParsing -Uri $asset.browser_download_url -OutFile "$exe.download" }
$checksumLine = Get-Content -LiteralPath $checksumPath | Where-Object { $_ -match '\s+\*?yt-dlp\.exe$' }
if (!$checksumLine) { throw 'Official executable checksum is missing.' }
$expected = ($checksumLine -split '\s+')[0]
$sha = [System.Security.Cryptography.SHA256]::Create()
$stream = [System.IO.File]::OpenRead("$exe.download")
try { $actual = [System.BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') } finally { $stream.Dispose(); $sha.Dispose() }
if ($actual -ne $expected) { throw 'Downloaded executable checksum did not match.' }
Move-Item -LiteralPath "$exe.download" -Destination $exe -Force
$ffmpeg = Get-Command ffmpeg -ErrorAction Stop
$node = Get-Command node -ErrorAction Stop
@{ version=$release.tag_name; ytDlp=$exe; ffmpeg=$ffmpeg.Source; node=$node.Source; sha256=$actual } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $toolsRoot 'config.json') -Encoding UTF8
Write-Output "Verified yt-dlp $($release.tag_name); configured existing FFmpeg and Node."
