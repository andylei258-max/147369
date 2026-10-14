using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace TabletExtend.Windows;

internal sealed class AppSettings
{
    public string BindAddress { get; set; } = "";
    public int Port { get; set; } = 27190;
    public int Fps { get; set; } = 10;
    public int Quality { get; set; } = 85;
    public string Token { get; set; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public string CrossingHotkey { get; set; } = "Ctrl+Win";
    public string LockHotkey { get; set; } = "Ctrl+Win+K";
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TabletExtend");
    public static string FilePath => Path.Combine(Folder, "settings.json");
    public static AppSettings Load()
    {
        if (!File.Exists(FilePath)) return new();
        return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
    }
    public void Validate()
    {
        if (!IPAddress.TryParse(BindAddress, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || ip.Equals(IPAddress.Any))
            throw new ArgumentException("请选择本机的局域网 IPv4 地址。");
        if (Port is < 1024 or > 65535 || Fps is < 1 or > 30 || Quality is < 30 or > 100)
            throw new ArgumentException("端口 1024–65535，帧率 1–30，JPEG 质量 30–100。");
        if (Token.Length != 64 || !Token.All(Uri.IsHexDigit)) throw new ArgumentException("配对码需要 64 个十六进制字符。可点击重新生成。");
        var cross = KeyChord.Parse(CrossingHotkey); var force = KeyChord.Parse(LockHotkey);
        if (cross == force || force.Key == 0) throw new ArgumentException("快捷键必须不同，强制锁定需要普通按键。");
    }
    public void Save()
    {
        Directory.CreateDirectory(Folder);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }
    public static string[] Addresses() => System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
        .SelectMany(n => n.GetIPProperties().UnicastAddresses)
        .Select(a => a.Address).Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
        .Select(a => a.ToString()).Distinct().ToArray();
}
