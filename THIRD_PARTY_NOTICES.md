# 第三方组件与分发边界

本仓库新增主机、核心策略、协议及安卓接收代码采用根目录 MIT LICENSE。

| 组件 | 用途 | 许可证/处理 |
| --- | --- | --- |
| VirtualDrivers/Virtual-Display-Driver 25.7.23 | 创建 Windows 真正的逻辑扩展显示器 | 上游根许可证 MIT；独立下载上游原始签名驱动，不把驱动二进制或代码纳入本仓库。驱动及 VDC 自带依赖须保持其原有声明。 |
| .NET 8 / Windows Forms | Windows 主机、网络与鼠标 Win32 互操作 | 上游 .NET MIT，并包含第三方声明。自包含 publish 输出中的 LICENSE/ThirdPartyNotices 应随整个目录保留。 |
| Android 平台 API | 原生 Canvas、JPEG 解码、Socket | 使用设备平台 API；没有复制 AOSP 实现。SDK 仅为构建工具，遵守 Google Android SDK 条款，不重新分发 SDK。 |
| Gradle 8.9 wrapper | 可重现安卓构建入口 | Apache-2.0；生成的脚本保留版权头；wrapper JAR 来自对应官方 Gradle 分发。参见 [Gradle license](https://github.com/gradle/gradle/blob/v8.9.0/LICENSE)。 |
| Android Gradle Plugin 8.7.3 | 安卓构建 | Apache-2.0，构建时从官方 Maven 仓库下载。 |

未复制 Deskreen、Sunshine、Moonlight、RustDesk 或 OpenDisplay 代码。它们的许可证调查只用于选型，不把本仓库变成这些项目的衍生分发。OpenDisplay README 明确写着许可证 TBD，因此未复用其代码。

VDD 下载哈希只证明与调查时发布包一致，不代表 Windows 已验证签名。VDC ZIP 内还包含 devcon 和其他驱动/依赖，不把“根 MIT”当成整个 ZIP 每个文件都为 MIT 的证明；使用上游工具安装，不重新打包或修改它。
