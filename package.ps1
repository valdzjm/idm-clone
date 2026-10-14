$ErrorActionPreference = 'Stop'
$packageRoot = Join-Path $PSScriptRoot 'release\LocalDownloadManager'
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $packageRoot 'bin') -Force | Out-Null
foreach ($name in @('LocalDownload.exe','LocalDownloadHost.exe','host.json')) {
    $source = Join-Path $PSScriptRoot "bin\$name"
    if ($name -ne 'host.json') {
        foreach ($suffix in @('.v02.exe','.v03.exe','.v04.exe')) {
            $versioned = Join-Path $PSScriptRoot ("bin\" + $name.Replace('.exe',$suffix))
            if (Test-Path -LiteralPath $versioned) { $source = $versioned }
        }
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $packageRoot "bin\$name") -Force
}
foreach ($name in @('extension','desktop')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $packageRoot -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $packageRoot 'tests') -Force | Out-Null
foreach ($name in @('verify.py','media.mjs','browser.mjs','preview.py','youtube-resolve.py','youtube-download.py','social-resolve.py','social-download.py','page-engine.py','PageHarness.cs','page-popup.mjs','facebook-story.mjs','ui-behavior.py','UiHarness.cs')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "tests\$name") -Destination (Join-Path $packageRoot 'tests') -Force
}
foreach ($name in @('README.md','INSPECTION.md','SOCIAL-SETUP.md','UPDATE-0.4.md','register-host.ps1','unregister-host.ps1','build.ps1','prepare.mjs','extension-id.txt','setup-video-tools.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $packageRoot -Force
}
$archive = Join-Path $PSScriptRoot 'release\LocalDownloadManager.zip'
Compress-Archive -LiteralPath $packageRoot -DestinationPath $archive -Force
Write-Output "Packaged $archive"
