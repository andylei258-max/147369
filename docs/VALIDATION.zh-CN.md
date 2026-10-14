# 云端验证记录

日期：2026-10-14。执行环境：Linux x86_64 云端容器，工作目录 `/workspace/147369`；**没有 Windows 桌面、VDD 驱动加载环境、安卓模拟器或物理平板**。

工具：.NET SDK **8.0.426**；Temurin JDK **17.0.20.1+1**；Gradle **8.9**；Android Gradle Plugin **8.7.3**；Android Platform **35**、Build Tools **34.0.0/35.0.0**。下载经环境 HTTP 代理并保留 TLS 校验，Gradle JVM 配置了环境代理和 CA；这些云端专用路径/信任库没有写入项目配置。

## 已验证

| 检查 | 结果 | 实际证明范围 |
| --- | --- | --- |
| GitHub 方案调查 | 读取官方仓库、许可证、默认分支最近提交、Release；摘要已保存 | 有来源的选型证据，不是上游项目运行验收 |
| VDD 固定 ZIP 和 VDC 下载检查 | 两个 ZIP SHA256 已记录；INF、CAT、DLL、配置、控制程序路径已检查 | 文件结构、架构声明、发布包一致性；**没有 Windows 签名策略验证** |
| `dotnet build TabletExtend.sln -c Release` | **成功，0 warnings / 0 errors** | 主机、核心、检查源码编译通过，包括 Windows 目标互操作声明 |
| `dotnet publish ... -r win-x64 --self-contained true` | **成功**，输出 `artifacts/windows-win-x64/`，EXE 为 PE32+ x64 GUI | Windows 可执行发布目录生成；**未运行主机或原生 Win32 API** |
| `dotnet run --project tests/TabletExtend.Checks -c Release` | **36 checks passed** | 模拟鼠标/时钟状态机＋真实 loopback TCP 服务、错误码/认证/确认/断联；图像为合成数据 |
| `bash scripts/check-java-wire.sh`（JDK17 PATH） | **7 checks passed** | 安卓使用的同一个 WirePacket Java 类处理 big-endian 包、截断/负/超长/未知类型；非设备执行 |
| `python scripts/check-interop.py` | **两帧互通成功** | 实际 C# TCP 服务→安卓 Java 协议解析→JVM ImageIO 解码真实合成 JPEG→序号 ACK；状态第一帧未连接、第二帧健康且跨屏关闭 |
| `gradle assembleDebug lintDebug --rerun-tasks` | **BUILD SUCCESSFUL；lint “No issues found”** | 原生安卓 APK 编译、打包、静态检查；非 Android 运行证明 |
| `android/gradlew --version` | **Gradle 8.9**，wrapper 实际下载完成并通过固定 SHA256 | 版本固定的构建入口及下载校验正常，不代表 Windows BAT 入口已执行 |
| `apksigner verify .../app-debug.apk` | 退出码 0 | debug APK 包签名结构可验证；非生产签名或设备安装证明 |
| `git diff --check`、文档相对链接检查 | 通过 | 补丁空白规范及文档路径一致性 |

Android 原产物：`android/app/build/outputs/apk/debug/app-debug.apk`。Windows 原产物：`artifacts/windows-win-x64/`。这些生成文件被 `.gitignore` 排除，源码和构建入口在 Git 分支保存；本地 Codex 重新构建即可。GitHub Actions 配置会生成可下载的构建 artifact，但需查看实际 workflow 结果，不能把配置存在当作已成功运行。

36 项检查包括：首次默认关闭、未连接不能开启、首帧确认限制、开关回主屏、强制锁优先和解锁保持关闭、断联释放且保留锁标记、重连关闭、租约边界、正确确认续租、鼠标 API 故障释放/恢复、包长度/big-endian、错配对码拒绝、仅 TCP 连接不启用限制、第二接收者拒绝但不扰乱第一会话、错误序号拒绝、确认停止、EOF 恢复、多个来源并发状态切换。FakeCursor 不验证 Windows 的实际 ClipCursor 行为。

## 尚未验证

- 安卓 PowerShell 构建、驱动下载、防火墙、应急恢复脚本在 Windows 执行。Linux 云端没有 `pwsh`；Windows 构建脚本已在 GitHub Windows runner 执行成功，不能冒充用户电脑实机执行。
- 性能、真实 Wi-Fi 丢包/延迟、屏幕 DPI/多GPU、原生 GDI 捕获是否黑屏、硬件游标的具体叠加效果。
- Ctrl+Win 钩子/系统开始菜单冲突、RegisterHotKey 默认和备用组合注册情况、应用间 ClipCursor 竞争。
- NamedPipe 用户限制与守护进程在 Windows 的进程创建、超时、卡死恢复、UAC/锁屏/睡眠行为。
- APK 的原生 Canvas、BitmapFactory 解码、旋转、应用生命周期和长时间内存/温度表现。

## 必须电脑＋平板实测

完整清单见 [DEVICE-TESTS.zh-CN.md](DEVICE-TESTS.zh-CN.md)。重点是驱动签名/加载→真实第二逻辑屏→真实窗口拖入→平板独立内容→鼠标/强制锁→断联/重连→主机卡死/退出恢复。以上关键项目当前全部“未测试”，不可宣布设备 MVP 验收成功。

## 云端遇到并处理的问题

- 首次 GitHub Actions [运行 38619877164](https://github.com/andylei258-max/147369/actions/runs/38619877164)：Windows 构建、36 项核心检查、自包含发布和 artifact 上传成功；Android 的 Java 检查与 C#→Java 互通成功，但 SDK 准备阶段报 `sdkmanager: command not found`。流水线补充 `android-actions/setup-android@v3`，显式安装 SDK 工具并设置 PATH；修正后的运行结果另行记录。
- 最初没有仓库；用户提供目标链接后克隆原仓库，切独立分支，迁入源码。主分支未改。
- 最初 Git push 和 GitHub 工具创建分支均返回 403 / `Resource not accessible by integration`。用户完成 GitHub App 安装与仓库访问授权后，开发分支已成功推送，main 未改；GitHub Actions 已触发。账户授权、ChatGPT 操作许可与 GitHub App 仓库安装授权是不同层次。完整 Git 历史备份仍保留，恢复方法见 STORAGE.zh-CN.md。
- 云端只有 Java 运行环境、没有 javac，下载并使用 JDK17。
- Gradle 默认未使用环境代理，首次解析 AGP 失败；修正 **云端运行参数**和 CA 信任后官方仓库依赖成功下载，不关闭证书验证，不替换来历不明的仓库。
- 第一轮 Android lint 有备份规则、绘制分配、图标、静态文字提示；补充备份排除规则、复用 RectF、图标和字符串资源，最后 lint 无问题。
- 代码审查修复连续帧自动收起设置区的问题；现在首帧才收起，后续长按展开保持可操作。这是源码审查修复，尚未设备复现验证。

本地实测后在该分支追加设备记录，保留本文件的云端证明边界，不把未测试项目批量改成通过。
