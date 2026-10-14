[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Push-Location (Join-Path $ProjectRoot 'android')
try {
    .\gradlew.bat assembleDebug lintDebug --console=plain
    if ($LASTEXITCODE -ne 0) { throw 'Android build/lint failed' }
    $Destination = Join-Path $ProjectRoot 'artifacts/android'
    New-Item -ItemType Directory -Force $Destination | Out-Null
    Copy-Item 'app/build/outputs/apk/debug/app-debug.apk' (Join-Path $Destination 'tablet-extend-debug.apk') -Force
    Write-Host 'Debug APK created. This is not a production signing certificate.'
} finally { Pop-Location }
