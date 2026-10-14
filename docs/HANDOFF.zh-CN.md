# Windows 本地 Codex 交接

更新时间：2026-10-14。目标仓库：`https://github.com/andylei258-max/147369`；独立分支：`dev/tablet-extend-mvp`。不要合并主分支。本文供本地 Codex 操作；需要用户碰设备时，一次只让用户做一步，用通俗中文说明。

**交接时远端写入被 403 拒绝；该开发分支当前只在云端本地。** 如果 GitHub 还没有此分支，先按 [STORAGE.zh-CN.md](STORAGE.zh-CN.md) 从交付的 Git bundle 恢复，再执行下面步骤。不要以为仅 clone 远端当前 main 就能取得项目。

## 当前进度

已实现真实扩展桌面的 Windows 采集主机、原生安卓接收端、鼠标开关/强制锁状态机、默认快捷键和可修改快捷键、托盘、原生状态图标、绘制确认、断联/超时释放、独立守护进程、排列位置菜单、配对与有限帧长。

云端已经交叉编译 Windows 主机并生成 win-x64 自包含目录，生成安卓调试 APK。状态、真实 loopback TCP 和跨语言 JPEG 检查已运行。**设备可用性未验收**：Windows 驱动加载、GDI 虚拟屏采集、主机运行、系统快捷键、游标 API、守护进程及真实 APK 安装/显示均未在用户设备验证。

先读 [验证记录](VALIDATION.zh-CN.md) 和 [设备测试清单](DEVICE-TESTS.zh-CN.md)。不要把 36 个模拟/网络检查写成 36 个设备测试通过。

## 依赖

| 部分 | 依赖 |
| --- | --- |
| Windows 主机 | Windows 10 22H2 或 Windows 11 x64 优先；.NET **8 SDK**（global.json 接受 8.0.400 同 feature 及后续 feature）；Git |
| 安卓构建 | JDK **17**、Android SDK Platform **35**、Build Tools **34.0.0**（AGP 默认需要）、platform-tools；Gradle **8.9 wrapper** / AGP **8.7.3** 已固定 |
| 安卓设备 | Android **8.0/API26** 以上，能够在 LAN 与电脑连通；网络只请求 INTERNET 权限，无 root |
| 虚拟显示 | 上游 VDD **25.7.23** x64 原始发行包及 VDC；微软 Visual C++ x64 Redistributable 按上游要求安装 |
| 签名核查 | PowerShell `Get-AuthenticodeSignature`；Windows SDK `signtool` 可选，核查 CAT 和其关联 DLL；不需为应用本身安装 WDK |

主机不依赖 NVIDIA、NDK、Node、Sunshine、Moonlight 或浏览器。首次目标设置建议一个 **1366×768/60 Hz 的 SDR 虚拟屏**，成功后再用 1920×1080；显示器刷新率和传输 fps 是不同概念。

本项目不自动修改 Secure Boot、测试签名或内存完整性。驱动如果被 Windows 拒绝，保留准确错误码、核查签名与系统兼容性，再决定方案；不能自动降低系统安全设置来制造成功。ARM64 不在当前可验证目标范围内。

## 取得代码和构建

在普通用户 PowerShell 工作目录执行，已有仓库先确认无用户未提交改动，不能覆盖它们：

```powershell
git clone https://github.com/andylei258-max/147369.git
cd 147369
git fetch origin
git switch dev/tablet-extend-mvp
dotnet --info
java -version
```

已有仓库使用 fetch 和 switch，不需要重复 clone。修改继续提交到同一独立分支；提交前核对 `git branch --show-current`。

Windows 构建与状态检查：

```powershell
./scripts/build-windows.ps1
```

输出：`artifacts/windows-win-x64/TabletExtend.Windows.exe`。必须保留 **整个目录**，包括 .NET、Windows Forms、本项目 DLL 和运行时文件，不能只复制 EXE。自包含不代表单文件。应用没有商用代码签名，SmartScreen 的实际表现待本机确认。

也可以用等价命令：

```powershell
dotnet build TabletExtend.sln -c Release
dotnet run --project tests/TabletExtend.Checks -c Release --no-build
dotnet publish src/TabletExtend.Windows -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o artifacts/windows-win-x64
```

Android：在 Android Studio SDK Manager 装好 API35、Build Tools34.0.0、platform-tools；选择 JDK17。设置该终端的 `JAVA_HOME`、`ANDROID_HOME` 为实际路径，或在 `android/local.properties` 配置本机 `sdk.dir`（文件不提交）。

```powershell
./scripts/build-android.ps1
```

输出：`artifacts/android/tablet-extend-debug.apk`；原位置 `android/app/build/outputs/apk/debug/app-debug.apk`。脚本执行 `assembleDebug lintDebug`。这是 **调试密钥签名**，适合实测，不是生产发布证书。云端和本机各自生成的 debug key 可能不同，已有同包名安装时可能报签名不一致；先核对，不要无提示删除用户应用数据。本应用没有用户教学文件，只保存连接参数。

云端 JVM 跨语言检查可在装有 JDK17/Python3/.NET8 的机器执行：

```powershell
dotnet build tests/TabletExtend.Checks -c Release
python scripts/check-interop.py
```

`scripts/check-java-wire.sh` 在 Bash 环境运行。这些验证不依赖真实显示设备。

## 驱动下载、核查和安装

普通用户先运行：

```powershell
./scripts/get-vdd.ps1
```

脚本只下载 **25.7.23** 的两个官方 ZIP，严格检查调查时记录的 SHA256，解压到 `artifacts/vdd`。没有执行安装。驱动文件不能修改，尤其 INF/CAT/DLL 不能为了绕过签名而编辑。

核查解压后的 `VDD Control.exe`、`mttvdd.cat`、`MttVDD.dll`。控制程序签名与驱动目录签名分别核查；GUI 签名有效不代表驱动可加载。若装有 Windows SDK，可对 `mttvdd.cat` 和该 catalog 内 `MttVDD.dll` 使用 `signtool verify /kp /c` 检查内核驱动策略；记录实际结果。官方源码当前 README 和 2025 年包不完全相同，不能把 README 新功能当作固定包已验证能力。

按 [VDD 官方安装说明](https://github.com/VirtualDrivers/Virtual-Display-Driver#-installation)，管理员启动解压包中的 **`VDD Control.exe`**，选择显示驱动的 Install 操作。用户不在电脑旁时，不宣称完成这一步。本地 Codex 可以主动处理下载和核查，若 UAC 需要用户操作，就只请用户确认这一个 UAC 提示。

成功门槛：设备管理器 Display Adapters 中 Virtual Display Driver 状态正常，Windows 显示设置出现独立第二屏，且不是镜像。若需要错误诊断，读取 `Get-PnpDevice -Class Display`、Windows 设备状态、驱动安装日志 `C:\Windows\INF\setupapi.dev.log` 和相关事件；日志中可能含本机信息，不把完整日志/连接配对码提交到公开 GitHub。

driver-only ZIP 的 INF 已核查是 `Root\MttVDD`、`NTamd64`。仅执行 `pnputil /add-driver /install` 不能被当作一定已经创建 root 设备：优先使用上游 VDC 创建并安装。不要在未验证时发明 VDD 私有接口。

安装可能需要管理员权限或重启。上游工具也支持卸载；如要用 pnputil 卸载，先确定这个驱动的精确 `oemNN.inf` 名称，不批量删除其他显卡/驱动。主机退出并释放鼠标后再卸载。

## Windows 首次启动

1. 在系统显示设置选择“**扩展这些显示器**”。主屏保持物理显示器；不把虚拟屏设为主屏。先选 SDR/1366×768。用“标识”确认虚拟屏编号。
2. 普通用户运行 `artifacts/windows-win-x64/TabletExtend.Windows.exe`。主机不要作为服务或计划任务后台无桌面运行，也不默认提权。
3. 首次出现连接设置，选择与平板同一 LAN 的电脑 IPv4，端口默认 `27190`，复制配对码。并非选 VPN/虚拟网卡地址。配置保存在 `%LOCALAPPDATA%\TabletExtend\settings.json`，不提交这个文件。
4. 托盘“选择虚拟扩展屏”选刚确认的那一块。菜单显示 Windows 设备名、尺寸和适配器名；自动名称识别是提示，不是设备验收。程序拒绝主屏及重叠范围。
5. 首次选择会把虚拟屏放主屏右侧。可用位置菜单改左/上/下；任意精细排列用 Windows 显示设置。主机每次重新启动要求重新选择目标，避免 DISPLAY 编号变化采错屏；重新选择默认放右侧。

如果脚本被 PowerShell 执行策略挡住，诊断当前策略，在合适情况下仅为该次进程执行使用 `powershell -ExecutionPolicy Bypass -File ...`；不要修改全局执行策略来省事。

## 局域网和 APK 安装

主机监听具体 IP 的 TCP，未用 HTTP/URLACL，不需要 `netsh http add urlacl`。

如果 Windows 防火墙阻挡，确认家庭可信网络为 Private，管理员运行针对实际路径/IP 的规则脚本。例如将 IP 改为电脑当前 LAN 地址：

```powershell
./scripts/configure-firewall.ps1 -ProgramPath ./artifacts/windows-win-x64/TabletExtend.Windows.exe -LocalAddress 192.168.1.10
```

规则只允许 Private 配置、指定 EXE/本地 IPv4/端口、LocalSubnet 来源；不关整个防火墙，不开放公网端口。换目录、IP 或端口需重新配置。撤销用同脚本 `-Remove`（此脚本仍要求参数；可使用原参数并添加 `-Remove`）。

把 APK 传到平板打开安装，按需允许该文件来源安装应用；或者已有授权的 ADB 设备可以 `adb install -r artifacts/android/tablet-extend-debug.apk`。USB/ADB 此处仅为安装方法，**不是已实现的 USB 显示传输**。需要用户在平板确认时，一次只教一个操作。

平板打开“平板扩展屏”，填电脑 IP、端口、完整配对码，连接。成功收到首帧后设置区自动收起，长按画面可以重新展开。平板不必开启 USB 调试来使用 LAN 显示。

平板进入后台时主动断开，主机释放；回前台时若用户尚未点“断开”，自动重连且跨屏关闭。失败重试间隔约 1 秒。没有扫码/自动发现，MVP 手工配对。

## 本地 Codex 的实施顺序

先构建并记录结果 → 签名核查 → 上游驱动安装 → Windows 真正第二桌面 → 主机只采这块桌面 → APK 安装 → LAN 图像 → 鼠标/快捷键 → 状态和故障恢复 → 游戏/长时间测试。

**最先验证真实扩展显示**：在 Windows 里把一个记事本窗口拖到虚拟屏，主屏开另一个不同窗口；平板必须看到被拖走的记事本，主屏继续显示另一窗口。若 GDI 得到黑屏/错屏，先调查 VDD/GPU/采集路径，必要时实现 DXGI Desktop Duplication；不改成主屏镜像或网页内容演示。

只在前一门槛通过后开展后续。Windows 原生 API 的只读诊断入口：

```powershell
$Exe = (Resolve-Path ./artifacts/windows-win-x64/TabletExtend.Windows.exe).Path
$Out = Join-Path (Get-Location) 'artifacts/native-diagnostics.json'
Start-Process -FilePath $Exe -ArgumentList @('--diagnostics', $Out) -Wait
```

路径若含空格，用正确的 PowerShell `ArgumentList` 引号；可在不含空格的本机开发目录执行。报告包含屏幕范围/适配器名/DEVMODE 布局尺寸（预期 220），不改变鼠标或安装驱动。本机原生诊断尚未在云端执行。

## 已知问题和限制

- 单平板、SDR 软件 JPEG、默认目标 10 fps，实际 fps 会随采集、网络、解码和确认变低。4K 带宽/CPU 和长时内存需测；尺寸上限每轴 4096，JPEG 上限 16 MiB。不用于高帧率游戏串流。
- 无 H.264/HEVC、音频、触控输入、USB、TLS、自动发现、应用开机自启或自动驱动安装。
- 断联不删除虚拟显示器，窗口保留，可能暂时在不可见桌面。需要时 Windows 设置禁用该屏或把窗口移回。断联时按用户要求释放 ClipCursor，强制锁标志可以保留，但没有健康连接时不持续限鼠标。
- Ctrl+Win 只有修饰键，采用低级键盘钩子，并保留 Windows 原始按键事件避免卡键。可能弹开始菜单；按 Ctrl+Win+K 时普通切换被取消，不应额外切换跨屏。系统/游戏冲突时换 Ctrl+Alt+F9/F10，先验证注册再实测。备用键被占用也会明确报错。
- ClipCursor 约束系统指针，不是 Raw Input/反作弊隔离。程序每 250 ms 重施主屏限制，但其他游戏/工具可能覆盖；不能承诺所有游戏都可靠。UAC/锁屏/安全桌面受 Windows 桌面权限限制。
- 正常网络断开在 TCP 检测后恢复；无声掉线约 1.5 秒租约＋250 ms Tick，安卓读超时 2.5 秒后消除过期状态。若主机 UI 不服务心跳，独立守护 2 秒后释放。实际计时和异常桌面情况待测。
- Windows 启动、守护子进程、Win32 结构、原生快捷键、DPI、GDI 虚拟屏采集均只有源码编译证明，必须本机实测。APK 没有在物理平板/模拟器运行过。
- Android Bitmap 每帧分配，网络无多帧积压，但长时间 GC、画质、旋转和系统省电行为尚待测试；应用仅保存连接信息，备份已排除。

## 紧急恢复与清理

同一普通用户桌面可运行 `./scripts/release-mouse.ps1`，释放 ClipCursor 并把指针放到主屏中心。先退出/结束主机，避免主机健康会话把限制再施回来。任务管理器可结束主机，独立守护应随后恢复；如果安全桌面/系统故障导致恢复失败，记录情况，不宣称保证。

退出托盘会停止串流并释放，不自动禁用驱动。卸载 VDD 使用上游工具。撤销防火墙规则，删除本项目发布目录，按用户意愿清理 `%LOCALAPPDATA%\TabletExtend`。不要移除无关设备或程序。

## 可以直接复制给 Windows 本地 Codex 的接手指令

```text
请接手“安卓平板作为 Windows 真正扩展屏”项目。
仓库：https://github.com/andylei258-max/147369
开发分支：dev/tablet-extend-mvp。不得合并 main。

云端 Git push 和 GitHub 创建分支被 403 拒绝，开发提交已存在于交付的 tablet-extend-mvp.bundle/备份ZIP；如果远端没有开发分支，请先用该 bundle 恢复分支和完整 Git 历史，再用本机授权 push。不得假装远端已有代码，也不要用空的 main 开始重写。

先读取 README.md、docs/HANDOFF.zh-CN.md、docs/VALIDATION.zh-CN.md 和 docs/DEVICE-TESTS.zh-CN.md。
云端已完成 .NET Windows 交叉编译/自包含发布、原生 Android APK 构建、核心状态与 TCP 检查和 C#→Java JPEG 互通；未在真实 Windows/平板验证驱动、主机、快捷键或 APK。

请主动处理普通依赖、构建和源码问题；保留已有未提交修改，在上述独立分支继续提交。
先构建，再下载固定版本 VDD 25.7.23 并检查 ZIP 哈希和 Windows 驱动签名；用上游 VDD Control 安装，不改 INF/CAT/DLL，不自动关闭 Secure Boot/内存完整性或开启测试签名。
确认 Windows 真正出现第二个扩展桌面，物理屏仍为主屏，然后启动托盘主机、选择虚拟屏、安装原生 APK并连接 LAN。
首先把一个真实 Windows 窗口拖到虚拟屏并确认平板显示它，同时主屏显示不同内容；采集失败不能换成镜像/网页演示。
逐项实测鼠标跨屏、Ctrl+Win、Ctrl+Win+K、不可用时的替代键、红绿/锁图标、断联/超时/重连默认关闭、主进程结束与卡死恢复、DPI/排列变化。必要时修复源码并重测。
对需要我操作平板、UAC或重启的环节，一次只教我一步，用通俗中文；不要让我一次执行一大串命令。
每项结果明确标“已验证/失败/未测试”，记录设备和版本，更新测试文档，不把构建成功当作实机成功。提交和推送到开发分支，不自行合并。
```
