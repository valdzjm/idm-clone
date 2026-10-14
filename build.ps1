param([switch]$Versioned)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework C# compiler is required.' }
$out = Join-Path $projectRoot 'bin'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$core = Join-Path $projectRoot 'desktop\Core.cs'
$desktopName = if ($Versioned) { 'LocalDownload.v04.exe' } else { 'LocalDownload.exe' }
$hostName = if ($Versioned) { 'LocalDownloadHost.v04.exe' } else { 'LocalDownloadHost.exe' }
$refs = @('/r:System.Net.Http.dll', '/r:System.Web.Extensions.dll', '/r:System.Core.dll', '/r:System.Security.dll')
& $compiler /nologo /target:winexe /optimize+ "/out:$out\$desktopName" @refs /r:System.Windows.Forms.dll /r:System.Drawing.dll $core (Join-Path $projectRoot 'desktop\Desktop.cs') (Join-Path $projectRoot 'desktop\VideoPage.cs') (Join-Path $projectRoot 'desktop\WindowFocus.cs')
if ($LASTEXITCODE) { throw 'Desktop compilation failed.' }
& $compiler /nologo /target:exe /optimize+ "/out:$out\$hostName" @refs $core (Join-Path $projectRoot 'desktop\NativeHost.cs')
if ($LASTEXITCODE) { throw 'Native host compilation failed.' }
Write-Output "Built $out\$desktopName and $hostName"
& rtk proxy node (Join-Path $projectRoot 'prepare.mjs')
if ($LASTEXITCODE) { throw 'Extension identity preparation failed.' }
