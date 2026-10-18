#!/usr/bin/env bash
# ============================================================
# 手机投屏助手 Linux GUI · deb 打包脚本
# 用法: bash build_deb.sh [amd64|arm64|all]   默认 all
# 依赖: dpkg-deb（Debian/Ubuntu 自带）
# ============================================================
set -e
cd "$(dirname "$0")"

VERSION="2.2.1"
PKG="phone-mirror-assistant"
ICON="icons/app.png"

if [ ! -f "$ICON" ]; then
  echo "缺少图标 $ICON，先运行: bash make_icon.sh 或提供 icons/app.png"
  exit 1
fi

build_arch() {
  local ARCH="$1"
  local OUT="dist/${PKG}_${VERSION}-1_${ARCH}.deb"
  local ROOT="pkgroot"

  rm -rf "$ROOT"
  mkdir -p "$ROOT/DEBIAN"
  mkdir -p "$ROOT/usr/bin"
  mkdir -p "$ROOT/usr/share/$PKG"
  mkdir -p "$ROOT/usr/share/applications"
  mkdir -p "$ROOT/usr/share/icons/hicolor/256x256/apps"

  # control
  cat > "$ROOT/DEBIAN/control" <<EOF
Package: $PKG
Version: $VERSION-1
Section: utils
Priority: optional
Architecture: $ARCH
Depends: python3, python3-pyside6, adb, scrcpy
Maintainer: cfq123451 <cfq123451@users.noreply.github.com>
Description: Phone Mirror Assistant - wireless / USB one-click phone mirroring (Linux GUI)
 Powered by scrcpy and adb. Auto-detect devices, wireless pairing dialog,
 USB connection wizard, system tray, reverse control and audio.
Homepage: https://github.com/cfq123451/phone-mirror-assistant
EOF

  # 启动脚本
  cat > "$ROOT/usr/bin/$PKG" <<'EOF'
#!/bin/sh
# 手机投屏助手 启动器
exec python3 /usr/share/phone-mirror-assistant/phone-mirror-gui.py "$@"
EOF
  chmod 755 "$ROOT/usr/bin/$PKG"

  # 主程序与示例配置
  cp phone-mirror-gui.py "$ROOT/usr/share/$PKG/"
  cp config.example.ini "$ROOT/usr/share/$PKG/" 2>/dev/null || true
  cp README.md "$ROOT/usr/share/$PKG/" 2>/dev/null || true

  # 桌面入口 + 图标
  cp phone-mirror-assistant.desktop "$ROOT/usr/share/applications/"
  cp "$ICON" "$ROOT/usr/share/icons/hicolor/256x256/apps/$PKG.png"

  # 打包
  mkdir -p dist
  dpkg-deb --build --root-owner-group "$ROOT" "$OUT"
  echo "✅ 生成: $OUT"
  rm -rf "$ROOT"
}

case "${1:-all}" in
  amd64) build_arch amd64 ;;
  arm64) build_arch arm64 ;;
  all) build_arch amd64; build_arch arm64 ;;
  *) echo "用法: $0 [amd64|arm64|all]"; exit 1 ;;
esac
