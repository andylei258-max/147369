using System.Diagnostics;
using System.Net;
using TabletExtend.Core;

namespace TabletExtend.Windows;

internal sealed class TrayApplication : ApplicationContext
{
    private AppSettings settings;
    private readonly MousePolicy policy = new(new WindowsCursor());
    private readonly DisplayCapture capture = new();
    private readonly NotifyIcon tray;
    private readonly Hotkeys hotkeys;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 250 };
    private readonly CursorGuardian guardian;
    private readonly Form dispatcher = new();
    private FrameServer? server;
    private string diagnostic = "请选择虚拟扩展屏";
    private bool exiting, guardianFailed, changing;
    private readonly ToolStripMenuItem crossing = new("允许鼠标跨屏");
    private readonly ToolStripMenuItem locking = new("强制锁在主屏");
    private readonly ToolStripMenuItem displayMenu = new("选择虚拟扩展屏");

    public TrayApplication()
    {
        _ = dispatcher.Handle;
        settings = AppSettings.Load();
        // Startup must release any leftover clipping before a healthy session exists.
        policy.Disconnect();
        guardian = new CursorGuardian();
        hotkeys = new Hotkeys(() => policy.ToggleCrossing(), () => policy.ToggleForcedLock());
        var menu = new ContextMenuStrip();
        crossing.Click += (_, _) => policy.ToggleCrossing(); locking.Click += (_, _) => policy.ToggleForcedLock();
        menu.Items.AddRange([crossing, locking, new ToolStripSeparator(), displayMenu]);
        displayMenu.DropDownOpening += (_, _) => PopulateDisplays();
        var arrangement = new ToolStripMenuItem("扩展屏位置");
        foreach (var (label, direction) in new[] { ("右侧（默认）", "right"), ("左侧", "left"), ("上方", "above"), ("下方", "below") })
        {
            var item = new ToolStripMenuItem(label);
            item.Click += async (_, _) => await Arrange(direction); arrangement.DropDownItems.Add(item);
        }
        menu.Items.Add(arrangement);
        menu.Items.Add("Windows 显示设置", null, (_, _) => Process.Start(new ProcessStartInfo("ms-settings:display") { UseShellExecute = true }));
        menu.Items.Add("连接和快捷键设置…", null, async (_, _) => await EditSettings());
        menu.Items.Add("查看连接状态", null, (_, _) => MessageBox.Show($"IP：{settings.BindAddress}:{settings.Port}\n扩展屏：{capture.Selected ?? "未选择"}\n{diagnostic}\n实际跨屏快捷键：{hotkeys.Crossing}\n实际锁定快捷键：{hotkeys.Locking}\n{policy.Status.Error}", "平板扩展屏"));
        menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("退出并释放鼠标", null, async (_, _) => await Quit());
        tray = new NotifyIcon { Text = "平板扩展屏：跨屏关闭", Icon = SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += async (_, _) => await EditSettings();
        timer.Tick += (_, _) => Tick(); timer.Start();
        ConfigureHotkeys();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplaysChanged;
        Microsoft.Win32.SystemEvents.PowerModeChanged += PowerChanged;
        Application.ApplicationExit += OnExit;
        dispatcher.BeginInvoke(async () =>
        {
            if (string.IsNullOrEmpty(settings.BindAddress)) await EditSettings();
            tray.ShowBalloonTip(7000, "下一步：选择虚拟扩展屏", "托盘菜单 → 选择虚拟扩展屏。若没有第二块屏，请先由本地 Codex 安装 VDD 并设置扩展。", ToolTipIcon.Info);
        });
    }

    private void ConfigureHotkeys()
    {
        try
        {
            hotkeys.Configure(settings.CrossingHotkey, settings.LockHotkey);
            if (hotkeys.Warning is not null) MessageBox.Show(hotkeys.Warning, "快捷键已调整");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message + "\n托盘菜单仍可操作，请修改快捷键。", "快捷键不可用");
        }
    }

    private void Tick()
    {
        policy.Tick();
        if (!guardianFailed)
        {
            try { guardian.Pulse(); }
            catch (Exception ex)
            {
                guardianFailed = true;
                // Do not allow ClipCursor again after the safety helper is gone.
                _ = StopServer(); capture.Select(null); policy.Disconnect();
                diagnostic = ex.Message;
            }
        }
        var status = policy.Status;
        crossing.Checked = status.Allowed; crossing.Enabled = status.Connected && !status.Locked && status.Error is null;
        locking.Checked = status.Locked;
        tray.Text = status.Locked ? "平板扩展屏：强制锁定" : status.Allowed ? "平板扩展屏：允许跨屏" : "平板扩展屏：禁止跨屏";
    }

    private void PopulateDisplays()
    {
        displayMenu.DropDownItems.Clear();
        foreach (var screen in Screen.AllScreens.Where(s => !s.Primary))
        {
            string device = screen.DeviceName;
            string adapter = Native.AdapterDescription(device);
            var item = new ToolStripMenuItem($"{device} · {screen.Bounds.Width}×{screen.Bounds.Height} · {adapter}") { Checked = capture.Selected == device };
            item.Click += async (_, _) => await Change(async () =>
            {
                if (!adapter.Contains("Virtual Display", StringComparison.OrdinalIgnoreCase))
                {
                    if (MessageBox.Show("该屏幕适配器名称无法直接确认是 VDD。请用 Windows 的“标识”按钮核对它确实是虚拟屏。\n确认选择并移到主屏右侧？", "核对扩展屏", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
                }
                await StopServer(); capture.Select(null);
                try
                {
                    DisplayCapture.RequireExtended(device);
                    // Deliberate selection each startup avoids unstable DISPLAYn
                    // identifiers silently capturing another physical monitor.
                    DisplayCapture.Arrange(device, "right"); capture.Select(device); StartServer();
                }
                catch (Exception ex) { ShowError(ex); }
            });
            displayMenu.DropDownItems.Add(item);
        }
        if (displayMenu.DropDownItems.Count == 0) displayMenu.DropDownItems.Add(new ToolStripMenuItem("没有扩展屏，请先安装 VDD") { Enabled = false });
    }

    private void StartServer()
    {
        if (guardianFailed) throw new InvalidOperationException("守护程序失效，请重启主机。");
        settings.Validate(); DisplayCapture.RequireExtended(capture.Selected);
        server = new FrameServer(IPAddress.Parse(settings.BindAddress), settings.Port, settings.Token, policy, () => capture.Capture(settings.Quality), settings.Fps);
        server.Diagnostic += message => diagnostic = message;
        try { server.Start(); diagnostic = "等待平板连接；跨屏默认关闭"; }
        catch { _ = server.DisposeAsync(); server = null; throw; }
    }
    private async Task StopServer()
    {
        var old = server; server = null;
        policy.Disconnect();
        if (old is not null) await old.DisposeAsync();
    }
    private async Task Arrange(string direction)
    {
        await Change(async () =>
        {
            if (capture.Selected is not { } device) return;
            await StopServer();
            try { DisplayCapture.Arrange(device, direction); StartServer(); }
            catch (Exception ex) { capture.Select(null); ShowError(ex); }
        });
    }
    private async Task EditSettings()
    {
        await Change(async () =>
        {
            using var form = new SettingsForm(settings);
            if (form.ShowDialog() != DialogResult.OK) return;
            await StopServer(); settings = form.Result;
            try
            {
                settings.Save(); ConfigureHotkeys();
                if (capture.Selected is not null) StartServer();
            }
            catch (Exception ex) { ShowError(ex); }
        });
    }
    private async Task Change(Func<Task> action)
    {
        if (changing || exiting) return;
        changing = true;
        try { await action(); }
        catch (Exception ex) { ShowError(ex); }
        finally { changing = false; }
    }
    private void DisplaysChanged(object? sender, EventArgs args)
    {
        // Release immediately, then revalidate on UI thread. A main-screen
        // change must not leave an old ClipCursor rectangle active.
        policy.Disconnect();
        if (exiting) return;
        dispatcher.BeginInvoke(async () =>
        {
            try { if (capture.Selected is not null) DisplayCapture.RequireExtended(capture.Selected); }
            catch (Exception ex) { await StopServer(); capture.Select(null); diagnostic = ex.Message; }
        });
    }
    private void PowerChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs args)
    {
        if (args.Mode != Microsoft.Win32.PowerModes.Suspend || exiting) return;
        policy.Disconnect();
        dispatcher.BeginInvoke(async () => { await StopServer(); capture.Select(null); diagnostic = "休眠后请重新选择虚拟扩展屏"; });
    }
    private void ShowError(Exception ex) { diagnostic = ex.Message; MessageBox.Show(ex.Message, "操作未完成"); }
    private async Task Quit()
    {
        if (exiting) return; exiting = true; timer.Stop();
        await StopServer(); Cleanup(); ExitThread();
    }
    private void OnExit(object? sender, EventArgs args) { policy.Disconnect(); }
    private void Cleanup()
    {
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplaysChanged;
        Microsoft.Win32.SystemEvents.PowerModeChanged -= PowerChanged;
        Application.ApplicationExit -= OnExit;
        policy.Disconnect(); guardian.Dispose(); hotkeys.Dispose(); tray.Visible = false; tray.Dispose(); dispatcher.Dispose(); timer.Dispose();
    }
}
