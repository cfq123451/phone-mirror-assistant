#!/usr/bin/env bash
# ============================================================
#  手机投屏助手 · Linux 一键安装脚本
#  自动安装依赖: adb + scrcpy
#  支持: Debian / Ubuntu / Raspberry Pi OS (arm64, amd64)
#
#  用法: sudo bash install.sh
# ============================================================
set -e

info() { printf '\033[1;36m[安装]\033[0m %s\n' "$*"; }
ok()   { printf '\033[1;32m✔ \033[0m%s\n' "$*"; }
err()  { printf '\033[1;31m✘ \033[0m%s\n' "$*" >&2; }

command -v apt-get >/dev/null 2>&1 || {
  err "当前仅支持 Debian/Ubuntu 系系统（apt）"
  err "其他系统请手动安装 adb 与 scrcpy 后运行 ./phone-mirror.sh"
  exit 1
}

ARCH=$(uname -m)
info "系统架构: $ARCH"

info "更新软件源…"
apt-get update -qq

info "安装 adb + scrcpy…"
if apt-get install -y adb scrcpy; then
  ok "依赖安装完成"
else
  err "apt 安装失败。请手动检查软件源，或参考 README 使用官方二进制安装。"
  exit 1
fi

ok "安装完成！用法:"
echo "  ./phone-mirror.sh list      列出设备"
echo "  ./phone-mirror.sh usb       通过 USB 投屏"
echo "  ./phone-mirror.sh pair      无线配对"
echo "  ./phone-mirror.sh connect   无线连接投屏"
echo "  ./phone-mirror.sh help      查看全部用法"
