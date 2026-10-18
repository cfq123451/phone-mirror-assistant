# Changelog

本项目遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [2.2.0] - 2026-10-18

### Added
- **Linux 命令行版**（`linux/phone-mirror.sh`）：支持 ARM64 / x86_64，功能对齐 Windows 版核心
  - `usb`：USB 投屏（含未授权等待、常见问题提示）
  - `pair` / `connect`：无线配对与连接投屏
  - `list` / `disconnect` / `help`
  - 音频、鼠标键盘反向控制默认开启；可选无边框窗口、分辨率限制、自定义标题
- `linux/install.sh` 一键安装依赖（apt 安装 adb + scrcpy，适配树莓派 OS / Debian / Ubuntu）
- GitHub Actions 新增 `build-linux.yml`：使用原生 ARM64 runner（ubuntu-24.04-arm）进行语法检查、冒烟测试并自动打包 `phone-mirror-assistant-linux-arm64.tar.gz` 发布到 Release

## [2.1.0] - 2026-10-17

### Added
- 动态设备列表：自动识别多台 USB/无线设备，支持滚动；连接方式徽章（USB/无线）；行内「投屏」按钮一键投屏
- 多设备并发投屏：不同设备互不影响，可同时投屏
- 无线配对二级界面：填入 IP/端口/6 位配对码后后台自动 `adb pair` 配对
- USB 连接引导：插线检测 + 授权提示 + 未插线三条排查提示
- 托盘图标与主界面图标统一（自绘蓝底手机图标）

### Fixed
- 同一台设备 USB/无线同时在线时重复显示 → 自动去重
- 设备行「投屏中」按钮四角杂色；全部按钮四角圆角杂色根治
- 无线投屏时状态卡错误显示 USB 信息
- 未插 USB 却误弹「连接成功」

### Changed
- 界面整体视觉重做：深色圆角、统一按钮圆角/边框/禁用态、去除最大化按钮

## [2.0.0] - 2026-10-12

### Added
- USB 连接模式：自动识别 USB 设备并投屏；连接方式三档（自动/USB/无线）
- 全新深色主界面：设备状态卡片、连接方式、窗口参数、一键开始/停止
- 全新应用图标（蓝紫渐变 + 手机投屏 + 在线指示）
- 单实例保护

### Changed
- 界面全面深色主题化；托盘菜单完善

## [1.1.0] - 2026-10-11

### Fixed
- 反向键盘中文输入不出字（scrcpy `--keyboard=uhid`）
- 托盘新增「配置手机键盘布局」

## [1.0.0] - 2026-10-11

### Added
- 托盘版无线投屏（scrcpy 4.1）：无边框小窗、置顶、防锁屏
- `config.ini` 配置（conn_addr / window_width / always_on_top / borderless）
