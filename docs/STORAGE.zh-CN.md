# 保存状态及分支恢复

2026-10-14 最新保存状态：

- 云端真实工作仓库：`/workspace/147369`。
- 目标远端：`https://github.com/andylei258-max/147369.git`。
- 开发分支：`dev/tablet-extend-mvp`，源码、文档、Gradle wrapper 和构建入口已经提交并推送到 GitHub。
- 原 main 保持 `79675ceed6b9973f112808e129f490e4149e4ce4`，未合并、未修改。
- 之前普通 Git push 和 GitHub 工具创建分支均返回 403；用户安装 ChatGPT Codex Connector 并授予仓库访问后已解决。
- **远端已有此次开发分支和代码：** [打开开发分支](https://github.com/andylei258-max/147369/tree/dev/tablet-extend-mvp)。本地 Codex 可直接 clone/fetch 后切换该分支。

## 交付备份

云端 Library 工作目录会生成：

- `tablet-extend-mvp.bundle`：原 main＋开发分支完整 Git 历史；通过 `git bundle verify`。
- `tablet-extend-mvp.zip`：所有已提交源码/文档，以及内置 `tablet-extend-mvp.bundle`，不含 SDK、驱动下载、配对码或本机配置。
- `tablet-extend-mvp.sha256.txt`：交付文件校验值和最新 commit。

GitHub 开发分支现已是正常接手入口；这里的云端本地备份用于离线恢复。Windows 本地 Codex 拿到 bundle/ZIP 后也可以完整恢复，所有生成物可以重建。

## Windows 本地 Codex 从 bundle 离线恢复（可选）

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

后续修改仍提交并推送开发分支。检查对应 GitHub Actions 结果；GitHub Actions 编译成功仍不代表平板实测成功。

## 已解决的授权问题

排查时确认：GitHub 的 Authorized GitHub Apps 页面已有账号授权，但 Installed GitHub Apps 页面尚未安装连接器。ChatGPT 插件内改为“全部允许”也未单独解决 GitHub 403。用户随后安装 ChatGPT Codex Connector 并授予仓库读写访问，实际 Git push 成功。没有索要密码或 PAT，也没有改变 main。

这个问题已经解决；本项目源码与已有云端验证结果无需重做。
