namespace TabletExtend.Windows;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--guard" && int.TryParse(args[1], out int parent))
        { CursorGuardian.Run(parent, args[2]).GetAwaiter().GetResult(); return; }
        if (args.Length == 2 && args[0] == "--diagnostics")
        {
            ApplicationConfiguration.Initialize();
            var bounds = Native.PrimaryBounds();
            var report = new
            {
                recordedAt = DateTimeOffset.UtcNow,
                os = Environment.OSVersion.VersionString,
                processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                devModeSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.DevMode>(),
                primary = new { bounds.Left, bounds.Top, bounds.Right, bounds.Bottom },
                displays = Screen.AllScreens.Select(s => new { s.DeviceName, s.Primary, s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height, adapter = Native.AdapterDescription(s.DeviceName) })
            };
            File.WriteAllText(args[1], System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            return;
        }
        using var single = new Mutex(true, "Local\\TabletExtend.Host", out bool created);
        if (!created) { MessageBox.Show("平板扩展屏已经运行，请查看系统托盘。"); return; }
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try { Application.Run(new TrayApplication()); }
        catch (Exception ex)
        {
            try { var cursor = new WindowsCursor(); cursor.Release(); cursor.MoveToMain(); } catch { }
            MessageBox.Show(ex.Message, "平板扩展屏未能运行");
        }
        finally { single.ReleaseMutex(); }
    }
}
