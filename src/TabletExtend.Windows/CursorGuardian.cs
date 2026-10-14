using System.Diagnostics;
using System.IO.Pipes;

namespace TabletExtend.Windows;

// Separate user-session process, no elevation/service. It releases ClipCursor
// if the tray process dies or stops servicing its UI heartbeat for two seconds.
internal sealed class CursorGuardian : IDisposable
{
    private readonly NamedPipeServerStream pipe;
    private readonly Process child;
    private bool disposed;
    public CursorGuardian()
    {
        string name = "TabletExtend-" + Guid.NewGuid().ToString("N");
        pipe = new(name, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        string exe = Environment.ProcessPath ?? throw new InvalidOperationException("无法定位守护程序");
        var start = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true };
        if (Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(CursorGuardian).Assembly.Location);
        start.ArgumentList.Add("--guard"); start.ArgumentList.Add(Environment.ProcessId.ToString()); start.ArgumentList.Add(name);
        child = Process.Start(start) ?? throw new InvalidOperationException("无法启动鼠标守护程序");
        try { pipe.WaitForConnectionAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult(); }
        catch { pipe.Dispose(); child.Dispose(); throw; }
    }
    public void Pulse()
    {
        if (!pipe.IsConnected || child.HasExited) throw new InvalidOperationException("鼠标守护程序已退出，请重新启动主机。");
        pipe.WriteByte(1);
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        pipe.Dispose(); child.Dispose();
    }
    public static async Task Run(int parentId, string name)
    {
        var cursor = new WindowsCursor();
        try
        {
            using var parent = Process.GetProcessById(parentId);
            using var client = new NamedPipeClientStream(".", name, PipeDirection.In, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await client.ConnectAsync(5000);
            byte[] buffer = new byte[1];
            while (!parent.HasExited)
            {
                using var timeout = new CancellationTokenSource(2000);
                if (await client.ReadAsync(buffer, timeout.Token) == 0) break;
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ArgumentException or TimeoutException) { }
        finally
        {
            try { cursor.Release(); cursor.MoveToMain(); } catch { /* desktop may be unavailable */ }
        }
    }
}
