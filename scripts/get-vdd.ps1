[CmdletBinding()]
param([string]$Destination = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/vdd'))
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Destination | Out-Null
$Packages = @(
    @{ Name='VirtualDisplayDriver-x86.Driver.Only.zip'; Hash='e24210692b442b39af763536330ce78b423f19342b7a7792c26de3944e418b3a' },
    @{ Name='VDD.Control.25.7.23.zip'; Hash='a701f2272e9fcf382849b24f913c6dd07597b3b1116525f2e90182f019609154' }
)
foreach ($Package in $Packages) {
    $Archive = Join-Path $Destination $Package.Name
    Invoke-WebRequest -Uri "https://github.com/VirtualDrivers/Virtual-Display-Driver/releases/download/25.7.23/$($Package.Name)" -OutFile $Archive
    if ((Get-FileHash $Archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Package.Hash) { throw "Hash mismatch: $($Package.Name)" }
    Expand-Archive -Path $Archive -DestinationPath (Join-Path $Destination ([IO.Path]::GetFileNameWithoutExtension($Package.Name))) -Force
}
Write-Host 'Downloaded and hash checked. Nothing has been installed.'
Write-Host 'Local Codex must verify driver/catalog signatures on Windows and inspect VDC before installing as Administrator.'
