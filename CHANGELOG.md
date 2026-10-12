# Changelog

本项目遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

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
