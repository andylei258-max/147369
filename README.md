# 平板扩展屏 · Tablet Extend

把 Windows 的第二块逻辑显示器传输到安卓平板，让教学内容和代码窗口各占一块屏幕。

**当前状态：MVP 源码、Windows 发布构建、原生 Android 调试 APK 与云端检查已完成；尚未在真实 Windows 和平板上验收。** 必须先安装外部 VDD 驱动并启用“扩展这些显示器”。本项目不创建网页假屏幕，也不采集主屏作为镜像替代。

开发分支：`dev/tablet-extend-mvp`。不合并到 `main`。

## 能力

- 复用 [Virtual Display Driver 25.7.23](https://github.com/VirtualDrivers/Virtual-Display-Driver/releases/tag/25.7.23) 提供真正的 Windows 逻辑扩展显示器。驱动独立下载、安装，未经本项目修改。
- Windows 托盘选择虚拟扩展屏，默认放主屏右侧；可改左、上、下，也可打开 Windows 显示设置自由排列。
- `Ctrl+Win` 在松开首个修饰键时切换跨屏。关闭时回主屏并用 `ClipCursor` 限制；不停止传输或改变窗口布局。
- `Ctrl+Win+K` 强制锁定；普通切换不能解锁；再次解锁仍保持跨屏关闭。
- 原生安卓 Canvas 接收 JPEG 桌面帧、显示红绿鼠标图标，锁定时增加锁形标记。
- 画面成功绘制后才确认连接。断联、画面确认超时或主机退出会释放限制并回主屏；重连默认关闭。主机守护进程对主程序退出/卡顿提供额外恢复。
- 快捷键可修改；无法注册强制锁定组合时尝试 `Ctrl+Alt+F10`；跨屏可改为 `Ctrl+Alt+F9`。

`Ctrl+Win` 无普通按键，不能用 `RegisterHotKey` 单独可靠表达，因此使用键盘钩子；可能触发开始菜单或被游戏/系统软件拦截。备用组合也可能被占用，注册失败会明确提示，托盘操作仍可用。详见交接文档。

## 最短接手路径

让 Windows 本地 Codex 阅读 **[中文交接文档](docs/HANDOFF.zh-CN.md)**，其中包含依赖、构建、驱动安装、启动方法和可直接复制的接手指令。

- [必须完成的电脑＋平板测试清单](docs/DEVICE-TESTS.zh-CN.md)
- [开源方案、许可证与维护调查](docs/RESEARCH.zh-CN.md)
- [云端验证记录](docs/VALIDATION.zh-CN.md)
- [实现结构、协议与状态规则](docs/ARCHITECTURE.zh-CN.md)
- [第三方声明](THIRD_PARTY_NOTICES.md)

## 构建

Windows 10/11 x64、.NET 8 SDK；安卓构建需要 JDK 17、Android SDK API 35、Build Tools 34.0.0。Gradle 8.9 wrapper 已包含并固定下载校验哈希。

```powershell
git clone https://github.com/andylei258-max/147369.git
cd 147369
git switch dev/tablet-extend-mvp
./scripts/build-windows.ps1
./scripts/build-android.ps1
```

Windows：`artifacts/windows-win-x64/TabletExtend.Windows.exe`，保留整个发布目录。APK：`artifacts/android/tablet-extend-debug.apk`。脚本在 Windows 的实际执行尚待本地验证；在云端已验证相应 dotnet / Gradle 构建命令。

驱动下载：`./scripts/get-vdd.ps1`，只下载和 SHA256 校验，不自动安装。安装、驱动签名和管理员权限处理参见交接文档。

## 边界

第一版单平板、可信局域网 TCP、JPEG 默认 10 fps / 质量 85，软件采集，适合先验证教学窗口。无 USB、音频、触控回传、H.264/HEVC 硬件编码或 TLS 加密。不能承诺游戏/反作弊环境下的鼠标约束或低延迟表现。断开后虚拟显示器仍保留，窗口也保留；用户可在 Windows 设置禁用它。

TCP 明确关闭时在主机读写检测到断开后恢复；网络黑洞默认约 1.5 秒检测，加上 250 ms 策略检查周期。安卓本地无响应读超时 2.5 秒。实际延迟需要设备测试，不承诺物理断网瞬间可检测。

云端检查验证了状态逻辑、网络协议、构建产物及 JVM JPEG 互通，**没有验证驱动能加载、Windows 虚拟屏能被成功采集、快捷键能在你的游戏中生效，或 APK 能在你的平板上运行。**
