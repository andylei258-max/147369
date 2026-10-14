[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProgramPath,
    [Parameter(Mandatory=$true)][string]$LocalAddress,
    [ValidateRange(1024,65535)][int]$Port = 27190,
    [switch]$Remove
)
$ErrorActionPreference = 'Stop'
$RuleName = 'TabletExtend-LAN'
if ($Remove) {
    Get-NetFirewallRule -Name $RuleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    return
}
$Executable = (Resolve-Path $ProgramPath).Path
$ParsedAddress = [System.Net.IPAddress]::Parse($LocalAddress)
if ($ParsedAddress.AddressFamily -ne 'InterNetwork' -or $LocalAddress -eq '0.0.0.0') { throw 'Choose a specific LAN IPv4 address' }
Get-NetFirewallRule -Name $RuleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
New-NetFirewallRule -Name $RuleName -DisplayName 'Tablet Extend trusted LAN' -Direction Inbound -Action Allow -Profile Private -Protocol TCP -LocalPort $Port -LocalAddress $LocalAddress -RemoteAddress LocalSubnet -Program $Executable | Out-Null
Write-Host 'Rule added for this program, Private profile and local subnet only.'
