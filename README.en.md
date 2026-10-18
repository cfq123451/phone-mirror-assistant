# Phone Mirror Assistant

**🌐 Language:** [**中文**](README.md) · [**English**](README.en.md)

[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20(ARM64)-blue)]()
[![Framework](https://img.shields.io/badge/.NET-4.0%2B-purple)]()
[![scrcpy](https://img.shields.io/badge/scrcpy-4.1-orange)](https://github.com/Genymobile/scrcpy)

A USB-first phone screen mirroring tool for Windows, powered by [scrcpy](https://github.com/Genymobile/scrcpy). Plug in a USB cable and it auto-detects and mirrors your phone; wireless debugging works as a fallback. It ships with a dark-themed main window and a system tray icon — **no app needs to be installed on your phone**.

> This project is initiated by **@cfq123451**, with **Doubao AI assistant** co-developing the design, code and documentation.

## ✨ Features

- **USB-first auto-connect** — Plug in the cable, allow USB debugging once, and the phone is detected and mirrored automatically. Wireless debugging is available as a fallback (connection mode: `Auto / USB only / Wireless only`)
- **Main window UI** — A dark rounded-corner main window appears on launch: dynamic device list (auto-detects USB/wireless, connection-type badges, one-click mirroring per row), wireless pairing / USB connection wizards, one-click start/stop
- **System tray** — Closing the window keeps it in the tray with a right-click quick menu; single-instance protection
- **Reverse control** — Control your phone with mouse & keyboard; Chinese input works (`--keyboard=uhid`, supports phone IME)
- **No phone app required** — Just enable "USB debugging" / "Wireless debugging" on the phone
- **Portable** — Green single-folder layout, double-click to run, no installation
- **Linux CLI (ARM64)** — A Linux command-line version `phone-mirror.sh` with USB/wireless mirroring, audio and reverse control; ideal for Raspberry Pi / ARM boxes

## 📸 Screenshots

| Main Window | Wireless Pair |
| --- | --- |
| ![main](docs/screenshots/main-window.png) | ![pair](docs/screenshots/pair-dialog.png) |

## 🚀 Quick Start

1. Download the latest `手机投屏助手-vX.Y.Z.zip` from [Releases](https://github.com/cfq123451/phone-mirror-assistant/releases) and unzip it (scrcpy runtime is bundled)
2. On your phone: Settings → About phone → tap "Build number" 7 times to enable Developer Mode → turn on "USB debugging" or "Wireless debugging"
3. **USB mirroring:** Connect the phone to the PC with a data cable (use a rear USB port on desktops), tap "Allow USB debugging" on the phone, then click "USB 连接" in the main window to auto-detect and mirror
4. **Wireless mirroring:** On the phone open "Wireless debugging" → "Pair device with pairing code", click "无线配对" in the main window, enter the IP/port/6-digit code shown on the phone, confirm — pairing and mirroring happen automatically
5. Double-click `手机投屏.exe` — the device is detected and mirroring starts automatically

> Note: When the PC is connected to the phone's hotspot, most phones refuse "Wireless debugging" — USB is the most reliable option in that case.

## 🐧 Linux Version (ARM64 / x86_64 CLI)

The Windows version is a GUI; the Linux version is a **command-line tool** (same scrcpy engine, feature-aligned: USB/wireless mirroring, audio, mouse & keyboard reverse control).

**Install** (Debian/Ubuntu/Raspberry Pi OS, one line):

```bash
curl -L -o phone-mirror.tar.gz https://github.com/cfq123451/phone-mirror-assistant/releases/latest/download/phone-mirror-assistant-linux-arm64.tar.gz
tar xzf phone-mirror.tar.gz
cd phone-mirror-linux
sudo bash install.sh          # auto-installs adb + scrcpy dependencies
```

**Usage:**

```bash
./phone-mirror.sh list                      # list devices
./phone-mirror.sh usb                       # USB mirroring (plug in cable, allow USB debugging on phone)
./phone-mirror.sh pair 192.168.1.5:39429 123456   # wireless pairing (IP:port + 6-digit code shown in phone "Wireless debugging")
./phone-mirror.sh connect 192.168.1.5:39429       # connect to a paired device and mirror
```

Optional environment variables: `BORDERLESS=1` (borderless window), `WINDOW_WIDTH=1080` (resolution limit), `TITLE=MyPhone` (window title).

> Note: scrcpy opens the mirroring window on the desktop — freely resizable/movable, mouse & keyboard control the phone directly; audio and video are output together.

## 🔨 Build from source

**Requirements:** Windows 10/11 · .NET Framework 4.0+ (built-in) · PowerShell 5+

```powershell
# Optional: regenerate the icon (assets/手机投屏.ico)
.\scripts\make-icon.ps1

# Compile and assemble a runnable dist\ (auto-downloads scrcpy if missing)
.\scripts\build.ps1
```

The resulting `dist\` folder is directly runnable. GitHub Actions automatically builds and uploads `dist` artifacts on every push/tag.

## 📖 Usage

- **Start/stop mirroring:** One-click control from the main window; auto-mirrors when a device is detected at startup (`auto_start`)
- **Window options:** width 300–1200, always-on-top, borderless
- **Device list:** auto-detects USB & wireless devices (connection-type badges), deduplicates the same phone shown via both USB and wireless; each row has a "mirror" button; multiple devices can mirror simultaneously
- **Wireless pairing:** click "无线配对" → enter IP/port/6-digit code from the phone dialog → `adb pair` runs automatically in the background, then mirrors
- **USB connection:** click "USB 连接" → auto-detects the USB device and asks for confirmation; shows three troubleshooting hints when no cable is detected
- **Tray:** closing the window keeps it in the tray; right-click the tray icon → show main window / start / stop / configure keyboard layout / exit
- **Chinese typing not working:** click "配置手机键盘布局" → on the phone select `English(US)` the first time, then Chinese input works

## ⚙️ Configuration (`config.ini`)

See [config.example.ini](config.example.ini): `mode`(auto/usb/wifi), `conn_addr`, `window_width`, `always_on_top`, `borderless`, `auto_start`. Written back automatically on exit.

## 🛠 Troubleshooting

| Issue | Fix |
| --- | --- |
| Device not detected | Try a cable that supports data transfer; set USB mode to "File Transfer/MTP"; if Device Manager shows a yellow exclamation mark, install the `Android ADB Interface` driver (vendor driver or "Driver Booster") |
| Wireless debugging fails | If the PC is on the phone's hotspot, most phones refuse wireless debugging → use USB instead |
| Antivirus warning | The program is unsigned — click "Allow/Trust" on first run |
| Mirroring exits mid-session | Make sure you tapped "Always allow" on the phone dialog, then click "Stop" and "Start" again |

## 📄 License

[Apache-2.0](LICENSE). Third-party components and licenses: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## 🙏 Credits

- **Initiator & developer:** [@cfq123451](https://github.com/cfq123451)
- **Co-development / docs / design:** Doubao (Doubao) AI assistant
- **Core mirroring engine:** [scrcpy](https://github.com/Genymobile/scrcpy) (Apache-2.0)
