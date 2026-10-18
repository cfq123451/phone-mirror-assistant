# 手机投屏助手 · Linux GUI 版

基于 scrcpy / adb 的 Linux 图形界面手机投屏工具，操作逻辑与 Windows 版一致。

## 安装

```bash
# amd64 (x86_64)
sudo apt install -y ./phone-mirror-assistant_2.2.1-1_amd64.deb

# arm64 (树莓派 / ARM 盒子 / 银河麒麟等)
sudo apt install -y ./phone-mirror-assistant_2.2.1-1_arm64.deb
```

依赖（自动安装）：`python3-pyside6 adb scrcpy`（需 Debian 12+ / Ubuntu 22.04+ / 树莓派 OS）。

安装后从应用菜单点击「手机投屏助手」启动，或终端运行 `phone-mirror-assistant`。

## 使用

- **设备列表**：自动识别 USB / 无线设备（连接方式徽章区分），同一台设备同时在线自动去重；每行「投屏」按钮一键投屏
- **无线配对**：点「无线配对」→ 填手机上显示的 IP / 端口 / 6 位配对码 → 后台自动 `adb pair`
- **USB 连接**：点「USB 连接」→ 未插线给出三条排查提示；已连接弹出「连接到 xxx 成功，是否确认投屏？」
- **开始 / 停止投屏**：主界面一键控制；启动时检测到设备自动投屏
- **窗口设置**：窗口宽度（300~1440）、置顶、无边框
- **托盘**：关闭窗口驻留系统托盘，右键可快速操作
- **配置**：`~/.config/phone-mirror-assistant/config.ini`（首次运行自动生成）
- **中文输入**：首次在手机输入法里选 `English(US)`，之后电脑打字中文正常

## 打包

```bash
bash build_deb.sh all        # 生成 amd64 + arm64 两个 deb 到 dist/
bash build_deb.sh amd64      # 只打 amd64
bash build_deb.sh arm64      # 只打 arm64
```

## 开发运行

```bash
pip install PySide6          # 或 apt install python3-pyside6
python3 phone-mirror-gui.py
```

需要系统已安装 `adb` 与 `scrcpy`。
