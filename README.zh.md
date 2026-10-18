# Phone Mirror Assistant · 手机投屏助手

**🌐 语言 / Language:** [**English**](README.md) · [**中文**](README.zh.md)

[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux%20(amd64%2Farm64)-blue)]()
[![GUI](https://img.shields.io/badge/GUI-WinForms%20%7C%20PySide6-purple)]()
[![scrcpy](https://img.shields.io/badge/scrcpy-4.1-orange)](https://github.com/Genymobile/scrcpy)

**Windows / macOS / Linux 一键手机投屏**，基于 [scrcpy](https://github.com/Genymobile/scrcpy)。插上数据线或无线配对后自动识别手机并投屏，画面、声音、鼠标键盘反向控制一次到位。**手机无需安装任何应用。**

> 本项目由 **@cfq123451** 发起，**豆包（Doubao）AI 助手参与设计、开发与文档编写**。

## ✨ 特性

- **USB / 无线自动识别** —— 动态设备列表，连接方式徽章区分；同一台手机 USB/无线同时在线自动去重
- **无线配对向导** —— 填入手机会话中显示的 IP / 端口 / 6 位配对码，后台自动执行 `adb pair`
- **USB 连接向导** —— 自动检测 USB 设备并确认投屏；未插线时给出三条排查提示
- **一键投屏** —— 主界面 / 托盘一键开始、停止；每台设备行内独立「投屏」按钮，多设备可同时投屏
- **声音 + 反向控制** —— 音画同步输出；鼠标键盘直接反控手机，中文输入可用（`--keyboard=uhid`）
- **系统托盘** —— 关闭窗口驻留托盘，右键快捷操作
- **Windows / macOS / Linux 体验一致** —— 三平台均为图形界面，像普通软件一样安装即用
- **手机零安装** —— 只需在手机上开启「USB 调试」/「无线调试」

> 注意：macOS 版用于投屏**安卓**手机。苹果手机（iPhone）投 Mac 用系统自带功能即可：AirPlay 屏幕镜像（无线）或 QuickTime Player（有线），无需安装任何软件。

## 📸 截图

| 主界面 | 无线配对 |
| --- | --- |
| ![main](docs/screenshots/main-window.png) | ![pair](docs/screenshots/pair-dialog.png) |

## 📦 下载（v2.2.2）

全部安装包都在**同一个** [Release](https://github.com/cfq123451/phone-mirror-assistant/releases) 页面，按平台选择：

| 平台 | 安装包 | 安装方式 |
| --- | --- | --- |
| Windows 10/11（64 位） | `phone-mirror-assistant-windows-64-v2.2.2.exe` | 双击运行 Setup 安装，自动创建桌面与开始菜单快捷方式 |
| macOS（Apple Silicon / M 系列） | `phone-mirror-assistant-macos-arm64-v2.2.2.dmg` | 打开 dmg，把应用拖入「应用程序」 |
| macOS（Intel） | `phone-mirror-assistant-macos-x86_64-v2.2.2.dmg` | 打开 dmg，把应用拖入「应用程序」 |
| Debian / Ubuntu / 树莓派 OS（x86_64） | `phone-mirror-assistant_2.2.2-1_amd64.deb` | `sudo apt install -y ./phone-mirror-assistant_2.2.2-1_amd64.deb` |
| Debian / Ubuntu / 树莓派 OS（ARM64） | `phone-mirror-assistant_2.2.2-1_arm64.deb` | `sudo apt install -y ./phone-mirror-assistant_2.2.2-1_arm64.deb` |

deb 包会自动安装依赖（`python3-pyside6`、`adb`、`scrcpy`）。macOS 需先用 `brew install scrcpy android-platform-tools` 装一次引擎，之后像普通软件一样打开即可，不需要碰命令行。

## 🚀 快速开始

1. 从 [Releases](https://github.com/cfq123451/phone-mirror-assistant/releases) 下载并安装对应平台的安装包（Windows 安装包内置 scrcpy 运行时；Linux 作为依赖自动安装）
2. 手机：设置 → 关于手机 → 连点「版本号」7 次开启开发者模式 → 打开「USB 调试」或「无线调试」
3. **USB 投屏**：数据线连接电脑（台式机用机箱后置 USB 口），手机上点「允许 USB 调试」，主界面点「USB 连接」自动识别并投屏
4. **无线投屏**：手机打开「无线调试」→「使用配对码配对设备」，主界面点「无线配对」，填入手机上显示的 IP / 端口 / 6 位配对码，确认后自动配对并投屏

> 注意：电脑连手机热点时多数机型会拒绝「无线调试」，此时 USB 是最稳的方案。

## 📖 使用说明

- **开始 / 停止投屏**：主界面一键控制；启动时检测到设备自动投屏（`auto_start`）
- **窗口参数**：宽度 300~1440、置顶、无边框
- **设备列表**：自动识别 USB 与无线设备（连接方式徽章区分），同一台设备 USB/无线同时在线自动去重；每行右侧「投屏」按钮一键投屏，多设备可同时投屏互不影响
- **无线配对**：主界面「无线配对」→ 填写手机弹窗中的 IP / 端口 / 6 位配对码 → 后台自动 `adb pair`，成功/失败弹窗提示
- **USB 连接**：主界面「USB 连接」→ 自动检测 USB 设备并弹出「已连接到 xxx，是否确认投屏？」；未插线时给出三条排查提示
- **托盘**：关闭窗口 = 驻留托盘；右键托盘 → 显示主界面 / 开始 / 停止 / 无线配对 / 退出
- **反向打字不出字**：点「配置手机键盘布局」→ 手机端首次选择 `English(US)`，之后中文输入正常

## 🔨 从源码构建

### Windows

**环境要求：** Windows 10/11 · .NET Framework 4.0+（系统自带）· PowerShell 5+

```powershell
# 可选：重新生成图标（assets/手机投屏.ico）
.\scripts\make-icon.ps1

# 编译并组装出可运行的 dist\（若缺 scrcpy 会自动下载）
.\scripts\build.ps1
```

产物 `dist\` 目录可直接运行。安装包（`phone-mirror-assistant-windows-64-*.exe`）由 Inno Setup 按 `phone-mirror-assistant.iss` 构建。

### Linux / macOS（GUI，PySide6）

源码位于 [`linux-gui/`](linux-gui/) —— 完整图形界面（设备列表、配对弹窗、USB 向导、托盘；Linux 配置存于 `~/.config/phone-mirror-assistant/config.ini`，macOS 存于 `~/Library/Application Support/phone-mirror-assistant/config.ini`）。

```bash
# 本地打两个架构的 deb（Linux）
cd linux-gui
bash build_deb.sh all        # 输出 dist/phone-mirror-assistant_2.2.2-1_{amd64,arm64}.deb
```

GitHub Actions（`build-linux-gui.yml`、`build-macos.yml`）会在每次打标签时自动构建并上传 Linux 与 macOS 安装包。

## ⚙️ 配置

Windows：可执行文件旁的 `config.ini`；Linux：`~/.config/phone-mirror-assistant/config.ini`；macOS：`~/Library/Application Support/phone-mirror-assistant/config.ini`。

见 [config.example.ini](config.example.ini)：`mode`(auto/usb/wifi)、`conn_addr`、`window_width`、`always_on_top`、`borderless`、`auto_start`。

## 🛠 常见问题

| 问题 | 处理 |
| --- | --- |
| 识别不到设备 | 换一根能传数据的线；USB 模式选「传输文件/MTP」；设备管理器有黄色感叹号用「驱动总裁」/厂商驱动装 `Android ADB Interface` |
| 无线调试连不上 | 电脑连手机热点时多数手机拒绝无线调试 → 改用 USB |
| 杀毒软件提示 | 程序未签名，首次运行点「允许/信任」即可 |
| 投屏中途退出 | 确认手机端弹窗已点「始终允许」，再点「停止投屏」后重新「开始投屏」 |
| Linux 装完没图标 | 确认应用菜单里有「手机投屏助手」入口；或在终端运行 `phone-mirror-assistant` 查看报错 |

## 📄 开源协议

[Apache-2.0](LICENSE)。第三方组件与许可见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

## 🙏 致谢

- **发起 & 开发者**：[@cfq123451](https://github.com/cfq123451)
- **协作开发 / 文档 / 设计**：豆包（Doubao）AI 助手
- **核心投屏引擎**：[scrcpy](https://github.com/Genymobile/scrcpy) (Apache-2.0)
