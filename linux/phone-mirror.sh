#!/usr/bin/env bash
# ============================================================
#  手机投屏助手 · Linux 版（命令行）
#  Phone Mirror Assistant (Linux CLI)
#
#  支持 USB / 无线两种连接，一键投屏（含音频与反向控制）
#
#  用法:
#    ./phone-mirror.sh list                      列出已连接设备
#    ./phone-mirror.sh usb                       通过 USB 投屏
#    ./phone-mirror.sh pair <IP:端口> <配对码>    无线配对（手机开发者选项里显示）
#    ./phone-mirror.sh connect <IP:端口>         连接已配对的设备并投屏
#    ./phone-mirror.sh disconnect                断开无线连接
#    ./phone-mirror.sh help                      显示本帮助
#
#  环境变量（可选）:
#    BORDERLESS=1        无边框窗口
#    WINDOW_WIDTH=1080   限制窗口分辨率（如 1080 / 720）
#    TITLE="我的手机"     自定义窗口标题
#
#  依赖: adb + scrcpy（首次使用请先运行: sudo bash install.sh）
# ============================================================
set -u

APP_NAME="手机投屏助手"
SCRCPY_BIN="${SCRCPY_BIN:-scrcpy}"
ADB_BIN="${ADB_BIN:-adb}"

info() { printf '\033[1;36m[%s]\033[0m %s\n' "$APP_NAME" "$*"; }
ok()   { printf '\033[1;32m✔ \033[0m%s\n' "$*"; }
warn() { printf '\033[1;33m⚠ \033[0m%s\n' "$*"; }
err()  { printf '\033[1;31m✘ \033[0m%s\n' "$*" >&2; }
die()  { err "$*"; exit 1; }

check_deps() {
  command -v "$ADB_BIN" >/dev/null 2>&1 || die "未找到 adb，请先运行: sudo bash install.sh"
  command -v "$SCRCPY_BIN" >/dev/null 2>&1 || die "未找到 scrcpy，请先运行: sudo bash install.sh"
}

# 构造 scrcpy 公共参数并启动投屏
start_scrcpy() {
  local serial="$1"
  local args=()

  # 音频: scrcpy v2.0+ 默认开启，无需额外参数
  # 反向控制: 默认开启（鼠标/键盘/触屏均可操作手机）
  [ "${BORDERLESS:-0}" = "1" ] && args+=(--window-borderless)
  [ -n "${WINDOW_WIDTH:-}" ] && args+=(--max-size "$WINDOW_WIDTH")
  [ -n "${TITLE:-}" ] && args+=(--window-title "$TITLE")

  if [ -n "$serial" ]; then
    info "启动投屏窗口（设备: $serial）…"
    exec "$SCRCPY_BIN" -s "$serial" "${args[@]}"
  else
    info "启动投屏窗口…"
    exec "$SCRCPY_BIN" "${args[@]}"
  fi
}

# 从 adb devices 输出中按类型提取设备（usb / 全部）
list_devices() {
  local type="$1"   # usb | all
  "$ADB_BIN" devices -l 2>/dev/null | awk -v t="$type" '
    NR == 1 { next }
    NF == 0 { next }
    $2 == "device" || $2 == "unauthorized" || $2 == "offline" {
      serial = $1
      rest = $0
      sub(/^[^ ]+ +/, "", rest)
      if (t == "all" || rest ~ /usb:/) print serial, $2, rest
    }'
}

cmd_help() {
  sed -n '2,22p' "$0" | sed 's/^# \{0,1\}//'
}

cmd_list() {
  check_deps
  info "正在扫描设备…"
  local lines
  lines=$(list_devices all)
  if [ -z "$lines" ]; then
    warn "未检测到设备。"
    echo "  USB 连接: 用数据线连接手机，并在手机上允许 USB 调试"
    echo "  无线连接: 先配对(./phone-mirror.sh pair)，再连接(./phone-mirror.sh connect)"
    return 0
  fi
  echo "已发现设备:"
  printf '  %s\n' "$lines"
  local n_unauth
  n_unauth=$(printf '%s\n' "$lines" | grep -c 'unauthorized' || true)
  if [ "$n_unauth" -gt 0 ]; then
    warn "存在未授权设备，请在手机弹出的窗口中勾选“允许 USB 调试”并确认。"
  fi
}

cmd_usb() {
  check_deps
  info "正在检测 USB 设备…"
  local lines
  lines=$(list_devices usb)
  if [ -z "$lines" ]; then
    err "未检测到 USB 设备。请检查:"
    echo "  1. 使用原装数据线连接手机与电脑"
    echo "  2. 台式机请插在主机后置 USB 端口（供电更稳）"
    echo "  3. 手机开发者模式中已开启 “USB 调试”"
    exit 1
  fi

  # 处理未授权设备
  local unauth
  unauth=$(printf '%s\n' "$lines" | awk '$2 == "unauthorized" {print $1}')
  if [ -n "$unauth" ]; then
    warn "检测到未授权设备: $unauth"
    echo "请在手机屏幕上点击 “允许 USB 调试”，等待授权…"
    for i in $(seq 1 30); do
      sleep 1
      if "$ADB_BIN" devices 2>/dev/null | grep -q "^${unauth}[[:space:]]*device$"; then
        break
      fi
      [ "$i" = "30" ] && die "等待授权超时，请重新插拔数据线后重试。"
    done
    ok "设备已授权"
  fi

  # 选出已授权设备
  local serial
  serial=$(printf '%s\n' "$lines" | awk '$2 == "device" {print $1; exit}')
  if [ -z "$serial" ]; then
    die "没有可用设备。"
  fi
  ok "已连接设备: $serial"
  start_scrcpy "$serial"
}

cmd_pair() {
  [ $# -ge 2 ] || die "用法: ./phone-mirror.sh pair <IP:端口> <配对码>"
  check_deps
  local addr="$1" code="$2"
  info "正在无线配对 $addr …"
  # 配对可能因 adb server 状态失败，先保证 server 运行
  "$ADB_BIN" start-server >/dev/null 2>&1
  if "$ADB_BIN" pair "$addr" "$code"; then
    ok "配对成功！接下来连接: ./phone-mirror.sh connect <IP:端口>"
  else
    err "配对失败。请确认:"
    echo "  1. IP 与端口与手机“无线调试”界面一致"
    echo "  2. 配对码正确（6 位数字）"
    echo "  3. 手机与电脑在同一局域网"
    exit 1
  fi
}

cmd_connect() {
  [ $# -ge 1 ] || die "用法: ./phone-mirror.sh connect <IP:端口>"
  check_deps
  local addr="$1"
  info "正在连接 $addr …"
  "$ADB_BIN" start-server >/dev/null 2>&1
  local out
  out=$("$ADB_BIN" connect "$addr" 2>&1)
  echo "$out"
  if ! echo "$out" | grep -q "connected"; then
    err "连接失败。若提示 unauthorized，请先在手机“无线调试”中配对；若连接被拒绝，请确认端口号。"
    exit 1
  fi

  # 等待设备进入可用状态
  local serial
  serial=$(echo "$addr" | cut -d: -f1-2)
  for i in $(seq 1 15); do
    sleep 1
    if "$ADB_BIN" devices 2>/dev/null | grep -q "^${serial}[[:space:]]*device$"; then
      break
    fi
  done
  ok "连接成功: $serial"
  start_scrcpy "$serial"
}

cmd_disconnect() {
  check_deps
  info "正在断开全部无线连接…"
  "$ADB_BIN" disconnect >/dev/null 2>&1
  ok "已断开"
}

# 主入口
case "${1:-}" in
  help|-h|--help) cmd_help ;;
  list)           cmd_list ;;
  usb)            cmd_usb ;;
  pair)           shift; cmd_pair "$@" ;;
  connect)        shift; cmd_connect "$@" ;;
  disconnect)     cmd_disconnect ;;
  *)              cmd_help; exit 1 ;;
esac
