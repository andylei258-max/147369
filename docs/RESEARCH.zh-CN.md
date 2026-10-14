# 开源方案调查（2026-10-14）

调查通过 GitHub 仓库搜索、官方 README/LICENSE、GitHub REST 仓库/最近提交/Release 元数据完成。原始摘要保存在 [research-snapshot.json](research-snapshot.json)。`pushed_at` 可能包含其他分支/标签活动，因此维护判断另列默认分支最近提交；不以星数代替可用性判断。

| 项目 | 核查的许可证 | 维护证据 | 虚拟显示 / 安卓 / 传输 | 决策 |
| --- | --- | --- | --- | --- |
| [VirtualDrivers/Virtual-Display-Driver](https://github.com/VirtualDrivers/Virtual-Display-Driver) | 根 LICENSE MIT | 默认分支最近提交 2026-06-22；最新非预发布 Release `25.7.23`（2025-07-23）；未归档 | Windows UMDF/IddCx 虚拟显示；无安卓端、无网络串流 | **选用外部原始驱动**。上游标为 Signed 的发布包；签名在目标 Windows 上仍需验证。 |
| [MolotovCherry/virtual-display-rs](https://github.com/MolotovCherry/virtual-display-rs) | MIT | 默认分支最近提交 2024-12-11；Release `v0.3.1`（2023-10-22）；未归档 | Rust Windows 驱动、配置应用；无安卓/传输；README 要求安装证书 | 可研究的驱动替代，但对不懂编程的用户优先采用有现成签名发行和安装工具的 VDD。导入证书不等于满足所有现代 Windows 驱动签名策略。 |
| [pavlobu/deskreen](https://github.com/pavlobu/deskreen) | **当前 AGPL-3.0**；不能套用旧版本 MIT 印象 | 最近提交和 Release `v3.2.16` 均为 2026-07-08 | Electron/WebRTC，浏览器接收；本身不应被当作已验证的 Windows IDD 创建器 | 不复用。用户要求原生接收和真实扩展屏，且还需鼠标/连接状态联动。 |
| [LizardByte/Sunshine](https://github.com/LizardByte/Sunshine) ＋ [Moonlight Android](https://github.com/moonlight-stream/moonlight-android) | 两者 GPL-3.0；再分发需按各自许可处理 | Sunshine 最近提交 2026-10-14，Release `v2026.914.233613`；Moonlight 默认分支最近提交 2026-09-12，Release `v12.2` | 成熟硬件编码主机＋原生安卓解码，GameStream 协议；Windows 虚拟显示仍需另配并选正确输出 | 后续性能升级优先考虑。当前没有核实到可直接复用的“本项目鼠标锁定状态图标＋绘制确认断联”接口，不假设 stock 客户端支持这些功能。需要真实整合/客户端修改与许可处理。 |
| [rustdesk/rustdesk](https://github.com/rustdesk/rustdesk) | AGPL-3.0 | 最近提交 2026-10-14，Release `1.5.0`（2026-09-30） | 成熟远程桌面＋安卓；有相关虚拟显示工作，但不是核查后可直接满足本需求的即插即用组件 | 不把远程桌面/镜像等同扩展屏，不复用未核实的自动驱动/状态能力。 |
| [HELFZz/OpenDisplay](https://github.com/HELFZz/OpenDisplay) | 根许可证 API 返回空；README 明确写 **TBD** | 调查时可读源仓库、README 标记 v0.1-beta；元数据见快照 | README 描述 IddCx、NVENC/HEVC、MediaCodec；USB/ADB、NVIDIA、测试签名 | 不复制代码：授权未明确、USB 优先且要求测试签名。README 的“tested”属于作者声明，未在本环境复现。 |

## 选用基础和实际复用范围

最大的系统集成风险是创建真正的 Windows 显示器，因此直接复用 VDD，而不从零写驱动。主机采用 .NET/Windows Forms/Win32 官方 API，安卓接收采用平台 Socket、BitmapFactory 和 Canvas，避免为了第一版鼠标状态图标维护庞大的串流客户端分支。

JPEG/TCP 是本项目实现的简单、受限帧长、单帧确认协议；它不是 Sunshine 或 Moonlight 的协议。它牺牲帧率/带宽效率换取 CPU 编码、跨 GPU 适用的实现和可在云端做的真实跨语言网络检查。云端尚不能证明 GDI 对目标 VDD/GPU 组合的采集效果，必须作为首轮设备测试门槛；如失败，应实现 DXGI Desktop Duplication 采集并实测，不能改采主屏掩盖失败。

没有修改或内嵌上游驱动，不破坏其签名。没有声称 VDD 本身有鼠标锁定、托盘联动或安卓图标接口；这些都由本仓库应用实现。驱动是否动态创建/移除、Windows 编号是否稳定也不作假设：启动时让用户明确选择非主屏，再验证独立、不重叠的桌面范围。

## 实际下载检查

从 [官方 25.7.23 Release](https://github.com/VirtualDrivers/Virtual-Display-Driver/releases/tag/25.7.23) 下载并检查过：

| ZIP | SHA256 |
| --- | --- |
| `VirtualDisplayDriver-x86.Driver.Only.zip` | `e24210692b442b39af763536330ce78b423f19342b7a7792c26de3944e418b3a` |
| `VDD.Control.25.7.23.zip` | `a701f2272e9fcf382849b24f913c6dd07597b3b1116525f2e90182f019609154` |

注意 `x86` 是上游 ZIP 名称，内部 INF 明确为 `NTamd64`，本项目针对 **Windows x64**。

driver-only ZIP 内确认有 `MttVDD.dll`、`MttVDD.inf`、`mttvdd.cat` 和 `vdd_settings.xml`。INF 声明 `Root\MttVDD`、Display 类、UMDF、`IddCx0102`，设备友好名 `Virtual Display Driver`。XML 默认一个虚拟显示器，含 1366×768 和 1920×1080 模式。VDC ZIP 内确认有 `VDD Control.exe` 和 `SignedDrivers/x86/VDD/`。

存在 CAT 文件、包名有 Signed，以及 SHA256 匹配，都不等于已在 Windows 通过驱动签名策略。主机源码 HEAD 的新特性也不能自动归属于 2025 年的固定发行包。安装和 Secure Boot/内存完整性兼容性均待本地验证。

## 官方 API 依据

- [ClipCursor](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-clipcursor)：系统鼠标资源，用完或失去适用条件应释放；不能把它当成游戏原始输入沙箱。
- [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)：修饰键＋虚拟按键，Windows 键组合可能保留给系统；必须核查注册返回值。
- [LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc)：钩子须及时返回，超时可能被移除。回调只做状态识别并 PostMessage，不做网络、编码或鼠标切换。
- [ChangeDisplaySettingsEx](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw)：只提交所选扩展屏的 `DM_POSITION`，检查返回值，不改主屏身份。
