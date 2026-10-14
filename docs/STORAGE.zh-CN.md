# 保存状态及分支恢复

2026-10-14 云端交接时：

- 云端真实工作仓库：`/workspace/147369`。
- 目标远端：`https://github.com/andylei258-max/147369.git`。
- 本地开发分支：`dev/tablet-extend-mvp`，源码、文档、Gradle wrapper 和构建入口已经提交。
- 原 main 保持 `79675ceed6b9973f112808e129f490e4149e4ce4`，未合并、未修改。
- 普通 Git push 返回 403；已连接 GitHub 工具创建同一分支也返回 403 / `Resource not accessible by integration`。
- **远端没有此次开发分支或代码；不能把本地提交当成已推送。** 当前集成写入权限需修复，或由本地已授权 Git 推送。

## 交付备份

云端 Library 工作目录会生成：

- `tablet-extend-mvp.bundle`：原 main＋开发分支完整 Git 历史；通过 `git bundle verify`。
- `tablet-extend-mvp.zip`：所有已提交源码/文档，以及内置 `tablet-extend-mvp.bundle`，不含 SDK、驱动下载、配对码或本机配置。
- `tablet-extend-mvp.sha256.txt`：交付文件校验值和最新 commit。

这里的备份是云端本地文件交付，不是已上传 GitHub 的替代说法。Windows 本地 Codex 拿到 bundle/ZIP 后即可完整恢复，所有生成物可以重建。

## Windows 本地 Codex 从 bundle 恢复

先解压 ZIP 或取得单独 bundle，使用实际下载路径替换以下示例。不要覆盖已有工作树的未提交修改。

```powershell
git clone https://github.com/andylei258-max/147369.git
cd 147369
git bundle verify 'C:\Downloads\tablet-extend-mvp.bundle'
git fetch 'C:\Downloads\tablet-extend-mvp.bundle' 'refs/heads/dev/tablet-extend-mvp:refs/heads/dev/tablet-extend-mvp'
git switch dev/tablet-extend-mvp
git log -2 --oneline
```

ZIP 内 bundle 位于根目录，源码也在根目录；建议恢复到 clone 的实际 Git 仓库，避免只把源码拷贝后丢失开发分支历史。

本机 GitHub 认证具备写入权限后：

```powershell
git push -u origin dev/tablet-extend-mvp
```

如果远端同名分支已由云端成功推送，先 fetch 比较提交，禁止盲目强推。不要合并 main。

推送成功后检查 GitHub 分支内容和 Actions 结果，并更新此文档/验证记录中的实际远端状态；GitHub Actions 编译成功仍不代表平板实测成功。

## 修复云端授权

用户可先打开 `https://github.com/settings/installations`，核对用于 ChatGPT/Codex 的 GitHub 应用是否包含该仓库。如果包含仍 403，需检查该应用授予的 Contents 写入能力或云端代码环境 GitHub 连接；不要索要用户密码、把 PAT 发到聊天/公开仓库，或假定聊天连接的账户 admin 权限可替代集成授权。

这是 GitHub 外部凭据授权拒绝，不是程序构建失败，也不是已经通过的批准请求。本项目源码与验证工作不需要重做。
