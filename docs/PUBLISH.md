# 发布指南 · Publish to GitHub

把本仓库发布到 GitHub 开源的全流程。发布前请把 README 中的 `cfq123451` 替换为你的 GitHub 用户名。

## 1. 创建 GitHub 仓库

GitHub 网页 → New repository：
- Repository name：`phone-mirror-assistant`
- Description：`USB-first phone screen mirroring tool for Windows, powered by scrcpy（基于 scrcpy 的 USB 优先手机投屏工具）`
- **Public**（开源）
- 不要勾选 "Add a README"（本仓库已自带，避免冲突）

## 2. 本地关联并推送

```bash
git remote add origin https://github.com/cfq123451/phone-mirror-assistant.git
git branch -M main
git push -u origin main
```

## 3. 打版本标签

```bash
git tag -a v2.0.0 -m "v2.0.0: USB-first + main UI + new icon"
git push origin v2.0.0
```

## 4. 创建 GitHub Release

GitHub 仓库页 → Releases → Draft a new release：
- **Tag**：`v2.0.0`
- **Title**：`v2.0.0 · USB 优先投屏 · 全新主界面与图标`
- **Body**：粘贴 [RELEASE_NOTES.md](RELEASE_NOTES.md) 的 v2.0.0 内容
- **Assets**：上传 `手机投屏助手-v2.0.0.zip`（含 exe + scrcpy 运行时，用户下载即用）

> CI：`.github/workflows/build.yml` 会在打标签时自动构建 `dist\` 产物，可作为发布资产备选。

## 5. 发布后检查

- 确认仓库 Public 可见
- README 徽章、License、Release 资产均正常
- 在 Releases 页确认 zip 可下载、解压后可运行

## 常用命令速查

```bash
# 提交修改
git add .
git commit -m "xxx"

# 推送
git push

# 更新版本号：改 CHANGELOG / RELEASE_NOTES 后
git add -A && git commit -m "release: vX.Y.Z"
git tag -a vX.Y.Z -m "vX.Y.Z"
git push && git push origin vX.Y.Z
```
