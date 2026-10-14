using System.Security.Cryptography;

namespace TabletExtend.Windows;

internal sealed class SettingsForm : Form
{
    public AppSettings Result { get; private set; }
    public SettingsForm(AppSettings current)
    {
        Result = current;
        Text = "平板扩展屏设置"; Width = 650; Height = 500; StartPosition = FormStartPosition.CenterScreen;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16), AutoSize = true };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); Controls.Add(layout);
        var addresses = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        addresses.Items.AddRange(AppSettings.Addresses());
        addresses.SelectedItem = current.BindAddress;
        if (addresses.SelectedIndex < 0 && addresses.Items.Count > 0) addresses.SelectedIndex = 0;
        var port = Box(current.Port.ToString()); var fps = Box(current.Fps.ToString()); var quality = Box(current.Quality.ToString());
        var cross = Box(current.CrossingHotkey); var force = Box(current.LockHotkey); var token = Box(current.Token);
        token.ReadOnly = true;
        void Row(string title, Control control)
        {
            int row = layout.RowCount++; layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = title, AutoSize = true, Margin = new Padding(0, 10, 6, 6) }, 0, row);
            layout.Controls.Add(control, 1, row);
        }
        Row("局域网 IP", addresses); Row("端口", port); Row("帧率", fps); Row("JPEG 质量", quality);
        Row("跨屏快捷键", cross); Row("强制锁定快捷键", force); Row("配对码（填到平板）", token);
        var tokenButtons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var copy = new Button { Text = "复制配对码", AutoSize = true }; copy.Click += (_, _) => Clipboard.SetText(token.Text);
        var regenerate = new Button { Text = "重新生成", AutoSize = true }; regenerate.Click += (_, _) => token.Text = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        tokenButtons.Controls.AddRange([copy, regenerate]); Row("", tokenButtons);
        Row("提示", new Label { AutoSize = true, MaximumSize = new Size(420, 0), Text = "默认 Ctrl+Win 在松开时切换。可能出现开始菜单或被游戏拦截；可改 Ctrl+Alt+F9 / Ctrl+Alt+F10。传输未加密，仅用于可信局域网。保存后连接会断开，跨屏关闭。" });
        var save = new Button { Text = "保存并应用", AutoSize = true };
        save.Click += (_, _) =>
        {
            try
            {
                Result = new AppSettings { BindAddress = addresses.Text, Port = int.Parse(port.Text), Fps = int.Parse(fps.Text), Quality = int.Parse(quality.Text), Token = token.Text, CrossingHotkey = cross.Text, LockHotkey = force.Text };
                Result.Validate(); DialogResult = DialogResult.OK; Close();
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
            { MessageBox.Show(this, ex.Message, "设置未保存"); }
        };
        Row("", save); AcceptButton = save;
    }
    private static TextBox Box(string value) => new() { Text = value, Dock = DockStyle.Fill, Margin = new Padding(3, 6, 3, 6) };
}
