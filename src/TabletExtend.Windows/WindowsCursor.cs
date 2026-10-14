using TabletExtend.Core;

namespace TabletExtend.Windows;

internal sealed class WindowsCursor : ICursorBoundary
{
    public void Release() => Native.Check(Native.ClipCursor(IntPtr.Zero));
    public void MoveToMain()
    {
        var rect = Native.PrimaryBounds();
        Native.Check(Native.GetCursorPos(out var point));
        // Preserve position when already on main, otherwise choose a safe center.
        if (point.X < rect.Left || point.X >= rect.Right || point.Y < rect.Top || point.Y >= rect.Bottom)
            Native.Check(Native.SetCursorPos(rect.Left + (rect.Right - rect.Left) / 2, rect.Top + (rect.Bottom - rect.Top) / 2));
    }
    public void RestrictToMain()
    {
        var rect = Native.PrimaryBounds(); Native.Check(Native.ClipCursor(ref rect));
    }
}
