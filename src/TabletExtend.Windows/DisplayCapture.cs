using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TabletExtend.Windows;

internal sealed class DisplayCapture
{
    private string? selected;
    public string? Selected => Volatile.Read(ref selected);
    public void Select(string? device) => Volatile.Write(ref selected, device);

    public static Screen RequireExtended(string? device)
    {
        var screens = Screen.AllScreens;
        var target = screens.SingleOrDefault(s => s.DeviceName == device);
        if (target is null || target.Primary) throw new InvalidOperationException("请先选择已启用的虚拟扩展屏；禁止采集主屏。");
        if (screens.Any(s => s.DeviceName != target.DeviceName && s.Bounds.IntersectsWith(target.Bounds)))
            throw new InvalidOperationException("显示器范围重叠。请在 Windows 显示设置选择“扩展这些显示器”并分开排列。");
        return target;
    }

    public static void Arrange(string device, string direction)
    {
        var target = RequireExtended(device);
        var main = Screen.PrimaryScreen ?? throw new InvalidOperationException("没有主屏");
        var mode = new Native.DevMode { Size = (ushort)Marshal.SizeOf<Native.DevMode>() };
        Native.Check(Native.EnumDisplaySettings(device, -1, ref mode));
        (mode.X, mode.Y) = direction switch
        {
            "left" => (main.Bounds.Left - target.Bounds.Width, main.Bounds.Top),
            "above" => (main.Bounds.Left, main.Bounds.Top - target.Bounds.Height),
            "below" => (main.Bounds.Left, main.Bounds.Bottom),
            _ => (main.Bounds.Right, main.Bounds.Top)
        };
        var proposed = new Rectangle(mode.X, mode.Y, target.Bounds.Width, target.Bounds.Height);
        if (Screen.AllScreens.Any(s => s.DeviceName != device && proposed.IntersectsWith(s.Bounds)))
            throw new InvalidOperationException("该位置与现有显示器重叠，请使用 Windows 显示设置手动排列。");
        mode.Fields = 0x20; // DM_POSITION; no resolution/refresh or primary change.
        int result = Native.ChangeDisplaySettingsEx(device, ref mode, IntPtr.Zero, 1, IntPtr.Zero);
        if (result != 0) throw new InvalidOperationException($"Windows 未应用排列，返回 {result}。请在显示设置操作。");
    }

    public byte[] Capture(int quality)
    {
        var bounds = RequireExtended(Selected).Bounds;
        if (bounds.Width > 4096 || bounds.Height > 4096) throw new InvalidOperationException("MVP 请使用不超过 4096×4096 的 SDR 屏幕。");
        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
            DrawCursor(graphics, bounds);
        }
        using var output = new MemoryStream();
        var jpeg = ImageCodecInfo.GetImageEncoders().Single(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
        bitmap.Save(output, jpeg, parameters); return output.ToArray();
    }

    private static void DrawCursor(Graphics graphics, Rectangle bounds)
    {
        var cursor = new Native.CursorInfo { Size = Marshal.SizeOf<Native.CursorInfo>() };
        if (!Native.GetCursorInfo(ref cursor) || cursor.Flags != 1 || !bounds.Contains(cursor.Position.X, cursor.Position.Y)) return;
        IntPtr copy = Native.CopyIcon(cursor.Handle);
        if (copy == IntPtr.Zero) return;
        try
        {
            if (!Native.GetIconInfo(copy, out var icon)) return;
            try
            {
                var dc = graphics.GetHdc();
                try { Native.DrawIconEx(dc, cursor.Position.X - bounds.Left - (int)icon.HotspotX, cursor.Position.Y - bounds.Top - (int)icon.HotspotY, copy, 0, 0, 0, IntPtr.Zero, 3); }
                finally { graphics.ReleaseHdc(dc); }
            }
            finally { if (icon.Mask != IntPtr.Zero) Native.DeleteObject(icon.Mask); if (icon.Color != IntPtr.Zero) Native.DeleteObject(icon.Color); }
        }
        finally { Native.DestroyIcon(copy); }
    }
}
