# Emergency recovery only. Run in the same normal-user interactive desktop.
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class TabletExtendRecovery {
    [DllImport("user32.dll")] public static extern bool ClipCursor(IntPtr rect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
}
'@
Add-Type -AssemblyName System.Windows.Forms
if (-not [TabletExtendRecovery]::ClipCursor([IntPtr]::Zero)) { throw 'ClipCursor release failed' }
$Bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
if (-not [TabletExtendRecovery]::SetCursorPos($Bounds.Left + [int]($Bounds.Width/2), $Bounds.Top + [int]($Bounds.Height/2))) { throw 'Cursor move failed' }
Write-Host 'Mouse restriction released; pointer moved to primary screen.'
