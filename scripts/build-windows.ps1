[CmdletBinding()]
param([string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $ProjectRoot
try {
    dotnet build TabletExtend.sln -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Windows build failed' }
    dotnet run --project tests/TabletExtend.Checks -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Core checks failed' }
    dotnet publish src/TabletExtend.Windows -c Release -r $Runtime --self-contained true -p:PublishSingleFile=false -o "artifacts/windows-$Runtime"
    if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed' }
    Write-Host "Run artifacts/windows-$Runtime/TabletExtend.Windows.exe as a normal user."
} finally { Pop-Location }
