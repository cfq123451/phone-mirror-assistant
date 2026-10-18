#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
手机投屏助手 · Linux GUI 版
Phone Mirror Assistant (Linux GUI)

基于 scrcpy / adb 的 Linux 图形界面手机投屏工具，操作逻辑与 Windows 版一致：
  设备列表自动识别（USB/无线）→ 一键投屏；无线配对二级弹窗；USB 连接三分支提示；
  开始/停止、窗口宽度、置顶、无边框、系统托盘、config.ini 配置。

依赖（安装包已自动处理）：
  python3-pyside6 / adb / scrcpy
"""

import os
import re
import shutil
import subprocess
import sys
import threading
import time
import configparser
from pathlib import Path

from PySide6.QtCore import Qt, QThread, Signal, QTimer, QSize
from PySide6.QtGui import QAction, QColor, QFont, QIcon, QPainter, QPixmap
from PySide6.QtWidgets import (
    QApplication, QMainWindow, QWidget, QVBoxLayout, QHBoxLayout, QLabel,
    QPushButton, QLineEdit, QListWidget, QListWidgetItem, QComboBox,
    QMessageBox, QDialog, QFormLayout, QCheckBox, QFrame, QSystemTrayIcon,
    QMenu, QSizePolicy, QScrollArea
)

APP_NAME = "手机投屏助手"
APP_EN = "Phone Mirror Assistant"
VERSION = "2.2.1"
SUBTITLE = "无线 / USB 一键投屏"

BASE_DIR = Path(__file__).resolve().parent
CONFIG_DIR = Path.home() / ".config" / "phone-mirror-assistant"
CONFIG_PATH = CONFIG_DIR / "config.ini"

ADB = shutil.which("adb") or "adb"
SCRCPY = shutil.which("scrcpy") or "scrcpy"

DARK_QSS = """
QMainWindow { background: #1e222a; }
QWidget { background: #1e222a; color: #e8eaf0; font-family: "Noto Sans CJK SC","WenQuanYi Micro Hei","Microsoft YaHei"; font-size: 13px; }
QListWidget { background: #161a21; border: 1px solid #2a2f3a; border-radius: 10px; padding: 6px; }
QListWidget::item { background: transparent; border-radius: 8px; }
QListWidget::item:selected { background: #253041; border: 1px solid #3d7eff; border-radius: 8px; }
QPushButton { background: #2b3240; border: 1px solid #3a4252; border-radius: 10px; color: #e8eaf0; padding: 7px 18px; font-size: 13px; }
QPushButton:hover { background: #364050; }
QPushButton:pressed { background: #222a36; }
QPushButton:disabled { background: #262c37; color: #6b7484; border-color: #2f3642; }
QPushButton#primary { background: #2f6bff; border: none; color: white; font-weight: bold; }
QPushButton#primary:hover { background: #4281ff; }
QPushButton#danger { background: #d94b4b; border: none; color: white; font-weight: bold; }
QPushButton#danger:hover { background: #e55c5c; }
QPushButton#ghost { background: #232936; border: 1px solid #313947; border-radius: 8px; padding: 5px 10px; font-size: 12px; }
QPushButton#ghost:hover { background: #2c3444; }
QComboBox { background: #232936; border: 1px solid #313947; border-radius: 8px; padding: 4px 10px; }
QComboBox::drop-down { border: none; width: 22px; }
QLineEdit { background: #161a21; border: 1px solid #313947; border-radius: 8px; padding: 6px 10px; selection-background-color: #2f6bff; }
QLabel#title { font-size: 19px; font-weight: bold; color: #ffffff; }
QLabel#subtitle { font-size: 12px; color: #8b94a5; }
QLabel#section { font-size: 13px; color: #b9c0cd; padding: 2px 0; }
QFrame#card { background: #161a21; border: 1px solid #2a2f3a; border-radius: 10px; }
QScrollArea { background: transparent; border: none; }
QScrollArea > QWidget > QWidget { background: transparent; }
QCheckBox { spacing: 6px; }
QCheckBox::indicator { width: 16px; height: 16px; border-radius: 4px; border: 1px solid #3a4252; background: #232936; }
QCheckBox::indicator:checked { background: #2f6bff; border-color: #2f6bff; }
QMenu { background: #1e222a; border: 1px solid #2a2f3a; border-radius: 8px; padding: 4px; }
QMenu::item { padding: 6px 18px; border-radius: 6px; }
QMenu::item:selected { background: #2f6bff; }
QToolTip { background: #232936; color: #e8eaf0; border: 1px solid #3a4252; }
QMessageBox { background: #1e222a; }
"""


def run_cmd(args, timeout=15):
    """运行外部命令，返回 (ok, stdout)"""
    try:
        p = subprocess.run(args, capture_output=True, text=True,
                           timeout=timeout, errors="replace")
        return p.returncode == 0, (p.stdout or "") + (p.stderr or "")
    except subprocess.TimeoutExpired:
        return False, "timeout"
    except Exception as e:
        return False, str(e)


def adb_devices():
    """解析 adb devices -l 输出 -> list[dict]"""
    ok, out = run_cmd([ADB, "devices", "-l"], timeout=10)
    devices = []
    if not ok:
        return devices
    for line in out.splitlines()[1:]:
        line = line.strip()
        if not line or line.startswith("*") or line.startswith("daemon"):
            continue
        parts = line.split()
        if len(parts) < 2:
            continue
        serial, state = parts[0], parts[1]
        if state != "device":
            continue
        dev = {"serial": serial, "state": state, "model": "", "transport": "wifi"}
        joined = " ".join(parts[2:])
        m = re.search(r"model:(\S+)", joined)
        if m:
            dev["model"] = m.group(1).replace("_", " ")
        if "usb:" in joined or "usb" in joined:
            dev["transport"] = "usb"
        devices.append(dev)
    return devices


class DeviceScanner(QThread):
    """后台轮询 adb 设备列表"""
    devices_updated = Signal(list)
    stopped = Signal()

    def __init__(self, interval=3.0):
        super().__init__()
        self.interval = interval
        self._run = True

    def stop(self):
        self._run = False
        self.stopped.emit()

    def run(self):
        while self._run:
            try:
                devices = adb_devices()
                self.devices_updated.emit(devices)
            except Exception:
                pass
            for _ in range(int(self.interval * 10)):
                if not self._run:
                    break
                time.sleep(0.1)


class PairWorker(QThread):
    """配对后台线程"""
    result = Signal(bool, str)

    def __init__(self, target, code):
        super().__init__()
        self.target = target
        self.code = code

    def run(self):
        ok, out = run_cmd([ADB, "pair", self.target, self.code], timeout=30)
        self.result.emit(ok, out.strip())


class PairDialog(QDialog):
    """无线配对二级弹窗：IP:端口 + 6位配对码"""

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setWindowTitle("无线配对")
        self.setFixedWidth(420)
        self.setModal(True)

        lay = QVBoxLayout(self)
        lay.setContentsMargins(20, 18, 20, 18)
        lay.setSpacing(10)

        tip = QLabel(
            "请在手机上打开「开发者选项 → 无线调试 → 使用配对码配对设备」，\n"
            "然后把手机上显示的 IP、端口和 6 位配对码填到下面：")
        tip.setWordWrap(True)
        tip.setStyleSheet("color:#8b94a5;")
        lay.addWidget(tip)

        form = QFormLayout()
        form.setSpacing(8)
        self.ip_edit = QLineEdit()
        self.ip_edit.setPlaceholderText("例如 192.168.1.5")
        self.port_edit = QLineEdit()
        self.port_edit.setPlaceholderText("例如 39429")
        self.code_edit = QLineEdit()
        self.code_edit.setPlaceholderText("6 位数字配对码")
        self.code_edit.setMaxLength(6)
        form.addRow("IP 地址", self.ip_edit)
        form.addRow("端口", self.port_edit)
        form.addRow("配对码", self.code_edit)
        lay.addLayout(form)

        self.status_label = QLabel("")
        self.status_label.setWordWrap(True)
        self.status_label.setStyleSheet("color:#e8b64c;")
        lay.addWidget(self.status_label)

        btns = QHBoxLayout()
        btns.addStretch(1)
        cancel_btn = QPushButton("取消")
        cancel_btn.clicked.connect(self.reject)
        self.ok_btn = QPushButton("确认配对")
        self.ok_btn.setObjectName("primary")
        self.ok_btn.clicked.connect(self.do_pair)
        btns.addWidget(cancel_btn)
        btns.addWidget(self.ok_btn)
        lay.addLayout(btns)

    def do_pair(self):
        ip = self.ip_edit.text().strip()
        port = self.port_edit.text().strip()
        code = self.code_edit.text().strip()
        if not ip or not port or not code:
            self.status_label.setText("请完整填写 IP、端口和配对码")
            return
        if not code.isdigit() or len(code) != 6:
            self.status_label.setText("配对码应为 6 位数字")
            return
        self.ok_btn.setEnabled(False)
        self.status_label.setText("正在配对…（请保持手机配对弹窗打开）")
        worker = PairWorker(f"{ip}:{port}", code)
        worker.result.connect(self.on_pair_done)
        worker.finished.connect(worker.deleteLater)
        worker.start()

    def on_pair_done(self, ok, out):
        self.ok_btn.setEnabled(True)
        if ok:
            QMessageBox.information(self, "配对成功", "配对成功！请回到主界面点击「开始投屏」。")
            self.accept()
        else:
            self.status_label.setText(f"配对失败：{out[:120]}")


class DeviceItemWidget(QWidget):
    """设备列表单行：名称 / 地址 / 徽章 / 状态 / 投屏按钮"""
    mirror_clicked = Signal(str)

    def __init__(self, dev, mirroring, parent=None):
        super().__init__(parent)
        self.serial = dev["serial"]
        lay = QHBoxLayout(self)
        lay.setContentsMargins(10, 6, 10, 6)
        lay.setSpacing(8)

        model = dev["model"] or self.serial[:18]
        info_col = QVBoxLayout()
        info_col.setSpacing(2)
        name_lbl = QLabel(model)
        name_lbl.setStyleSheet("font-weight:bold; color:#ffffff; font-size:13px;")
        addr_lbl = QLabel(dev["serial"])
        addr_lbl.setStyleSheet("color:#7d8798; font-size:11px;")
        info_col.addWidget(name_lbl)
        info_col.addWidget(addr_lbl)
        lay.addLayout(info_col, 1)

        badge = QLabel("无线" if dev["transport"] == "wifi" else "USB")
        badge.setAlignment(Qt.AlignCenter)
        if dev["transport"] == "usb":
            badge.setStyleSheet("background:#1d4b8f; color:#a8c8ff; border-radius:8px; padding:2px 8px; font-size:11px;")
        else:
            badge.setStyleSheet("background:#3d5a3a; color:#a8e0a8; border-radius:8px; padding:2px 8px; font-size:11px;")
        lay.addWidget(badge)

        if mirroring:
            state = QLabel("投屏中")
            state.setStyleSheet("background:#d94b4b; color:#ffe0e0; border-radius:8px; padding:2px 8px; font-size:11px;")
            lay.addWidget(state)
        else:
            btn = QPushButton("投屏")
            btn.setObjectName("ghost")
            btn.setCursor(Qt.PointingHandCursor)
            btn.clicked.connect(lambda: self.mirror_clicked.emit(self.serial))
            lay.addWidget(btn)


class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle(f"{APP_NAME} · {APP_EN}")
        self.setMinimumWidth(360)
        self.resize(400, 620)
        self._devices = []
        self._mirroring = {}          # serial -> Popen
        self._cfg = self.load_config()
        self._scanner = DeviceScanner()
        self._scanner.devices_updated.connect(self.on_devices)
        self._scanner.start()
        self._build_ui()
        self._build_tray()
        self._apply_config()
        QTimer.singleShot(1500, self._maybe_auto_start)

    # ---------- 配置 ----------
    @staticmethod
    def load_config():
        cfg = configparser.ConfigParser()
        defaults = {"mode": "auto", "conn_addr": "", "window_width": "300",
                    "always_on_top": "0", "borderless": "0", "auto_start": "1"}
        try:
            CONFIG_DIR.mkdir(parents=True, exist_ok=True)
        except Exception:
            pass
        if CONFIG_PATH.exists():
            try:
                cfg.read(CONFIG_PATH, encoding="utf-8")
            except Exception:
                pass
        sec = cfg["phone-mirror"] if cfg.has_section("phone-mirror") else {}
        merged = dict(defaults)
        for k in defaults:
            if k in sec:
                merged[k] = sec[k]
        return merged

    def save_config(self):
        cfg = configparser.ConfigParser()
        cfg["phone-mirror"] = {
            "mode": self._cfg["mode"],
            "conn_addr": self._cfg["conn_addr"],
            "window_width": self._cfg["window_width"],
            "always_on_top": self._cfg["always_on_top"],
            "borderless": self._cfg["borderless"],
            "auto_start": self._cfg["auto_start"],
        }
        try:
            with open(CONFIG_PATH, "w", encoding="utf-8") as f:
                cfg.write(f)
        except Exception:
            pass

    # ---------- UI ----------
    def _build_ui(self):
        central = QWidget()
        root = QVBoxLayout(central)
        root.setContentsMargins(14, 12, 14, 12)
        root.setSpacing(8)

        # 标题区
        title_row = QHBoxLayout()
        title_box = QVBoxLayout()
        title_box.setSpacing(0)
        t = QLabel(APP_NAME)
        t.setObjectName("title")
        st = QLabel(f"{SUBTITLE} · v{VERSION}")
        st.setObjectName("subtitle")
        title_box.addWidget(t)
        title_box.addWidget(st)
        title_row.addLayout(title_box)
        title_row.addStretch(1)
        root.addLayout(title_row)

        # 设备列表
        sec = QLabel("设备列表")
        sec.setObjectName("section")
        root.addWidget(sec)
        self.device_list = QListWidget()
        self.device_list.setSelectionMode(QListWidget.NoSelection)
        self.device_list.setSpacing(4)
        root.addWidget(self.device_list, 1)

        # 操作按钮行
        btn_row = QHBoxLayout()
        btn_row.setSpacing(6)
        self.refresh_btn = QPushButton("刷新")
        self.refresh_btn.clicked.connect(self.refresh_now)
        self.pair_btn = QPushButton("无线配对")
        self.pair_btn.clicked.connect(self.open_pair)
        self.usb_btn = QPushButton("USB 连接")
        self.usb_btn.clicked.connect(self.do_usb)
        for b in (self.refresh_btn, self.pair_btn, self.usb_btn):
            btn_row.addWidget(b)
        root.addLayout(btn_row)

        btn_row2 = QHBoxLayout()
        btn_row2.setSpacing(6)
        self.start_btn = QPushButton("开始投屏")
        self.start_btn.setObjectName("primary")
        self.start_btn.clicked.connect(self.start_mirror_all)
        self.stop_btn = QPushButton("停止投屏")
        self.stop_btn.setObjectName("danger")
        self.stop_btn.setEnabled(False)
        self.stop_btn.clicked.connect(self.stop_all)
        for b in (self.start_btn, self.stop_btn):
            btn_row2.addWidget(b)
        root.addLayout(btn_row2)

        # 设置区
        opt_row = QHBoxLayout()
        opt_row.setSpacing(6)
        opt_row.addWidget(QLabel("窗口宽度"))
        self.width_combo = QComboBox()
        for w in ("300", "480", "720", "1080", "1440"):
            self.width_combo.addItem(w)
        self.width_combo.setCurrentText(self._cfg["window_width"])
        self.width_combo.currentTextChanged.connect(self.on_width_changed)
        opt_row.addWidget(self.width_combo)
        self.top_check = QCheckBox("窗口置顶")
        self.top_check.setChecked(self._cfg["always_on_top"] == "1")
        self.top_check.toggled.connect(self.on_top_toggled)
        self.blank_check = QCheckBox("无边框")
        self.blank_check.setChecked(self._cfg["borderless"] == "1")
        self.blank_check.toggled.connect(self.on_blank_toggled)
        opt_row.addWidget(self.top_check)
        opt_row.addWidget(self.blank_check)
        opt_row.addStretch(1)
        root.addLayout(opt_row)

        btn_row3 = QHBoxLayout()
        btn_row3.setSpacing(6)
        kb_btn = QPushButton("配置手机键盘布局")
        kb_btn.clicked.connect(self.open_keyboard_help)
        exit_btn = QPushButton("退出")
        exit_btn.clicked.connect(self.quit_app)
        btn_row3.addWidget(kb_btn)
        btn_row3.addWidget(exit_btn)
        root.addLayout(btn_row3)

        # 底部状态
        self.status_lbl = QLabel("正在扫描设备…")
        self.status_lbl.setObjectName("subtitle")
        root.addWidget(self.status_lbl)

        self.setCentralWidget(central)

    def _build_tray(self):
        self.tray = QSystemTrayIcon(QIcon(self._tray_icon()), self)
        self.tray.setToolTip(APP_NAME)
        menu = QMenu()
        show = menu.addAction("显示主界面")
        show.triggered.connect(self.show_normal)
        m1 = menu.addAction("立即投屏")
        m1.triggered.connect(self.start_mirror_all)
        m2 = menu.addAction("断开投屏")
        m2.triggered.connect(self.stop_all)
        menu.addSeparator()
        p1 = menu.addAction("无线配对")
        p1.triggered.connect(self.open_pair)
        q = menu.addAction("退出")
        q.triggered.connect(self.quit_app)
        self.tray.setContextMenu(menu)
        self.tray.activated.connect(self.on_tray_activated)
        self.tray.show()

    @staticmethod
    def _tray_icon():
        """生成托盘/窗口图标（蓝色圆角方形 + 手机图案）"""
        cache = Path("/tmp/phone-mirror-icon.png")
        if cache.exists():
            return str(cache)
        pm = QPixmap(64, 64)
        pm.fill(Qt.transparent)
        p = QPainter(pm)
        p.setRenderHint(QPainter.Antialiasing)
        p.setBrush(QColor("#2f6bff"))
        p.setPen(Qt.NoPen)
        p.drawRoundedRect(2, 2, 60, 60, 14, 14)
        p.setPen(QColor("#ffffff"))
        p.setBrush(Qt.NoBrush)
        # 手机轮廓
        p.drawRoundedRect(22, 12, 20, 40, 4, 4)
        p.drawLine(28, 16, 36, 16)  # 听筒
        p.drawLine(30, 46, 34, 46)  # 按键
        # 屏幕斜线（投屏示意）
        p.setPen(QColor("#a8c8ff"))
        p.drawLine(24, 20, 40, 20)
        p.drawLine(24, 26, 36, 26)
        p.drawLine(24, 32, 32, 32)
        p.end()
        pm.save(str(cache))
        return str(cache)

    # ---------- 设备刷新 ----------
    def on_devices(self, devices):
        # 去重：同一 serial 的 usb/wifi 只显示一台（优先 usb）
        seen = {}
        for d in devices:
            key = d["serial"]
            if ":" in d["serial"]:  # wifi: ip:port 去掉端口匹配 usb 序列号
                key = d["serial"].split(":")[0]
            if key not in seen or (d["transport"] == "usb" and seen[key]["transport"] != "usb"):
                seen[key] = d
        self._devices = list(seen.values())
        self._render_devices()
        if self._devices:
            self.status_lbl.setText(f"发现 {len(self._devices)} 台设备")
        else:
            self.status_lbl.setText("未检测到设备 · USB 插线并允许调试，或使用无线配对")

    def _render_devices(self):
        self.device_list.clear()
        for d in self._devices:
            item = QListWidgetItem()
            w = DeviceItemWidget(d, d["serial"] in self._mirroring)
            w.mirror_clicked.connect(self.start_mirror_one)
            item.setSizeHint(w.sizeHint())
            self.device_list.addItem(item)
            self.device_list.setItemWidget(item, w)
        has_mirror = bool(self._mirroring)
        self.stop_btn.setEnabled(has_mirror)
        if has_mirror:
            self.start_btn.setEnabled(False)
        else:
            self.start_btn.setEnabled(bool(self._devices))

    def refresh_now(self):
        self.status_lbl.setText("正在刷新…")
        self.on_devices(adb_devices())

    # ---------- 无线配对 ----------
    def open_pair(self):
        PairDialog(self).exec()

    # ---------- USB 连接 ----------
    def do_usb(self):
        usb = [d for d in adb_devices() if d["transport"] == "usb"]
        if not usb:
            QMessageBox.warning(self, "USB 未连接", 
                "请先完成以下三步：\n\n"
                "1. 请使用原装数据线连接手机与电脑\n"
                "2. 请使用台式主机主板 USB 端口\n"
                "3. 请在开发者模式下开启「USB 调试」")
            return
        dev = usb[0]
        # 检查授权状态
        ok, out = run_cmd([ADB, "-s", dev["serial"], "shell", "getprop", "ro.product.model"], timeout=10)
        model = out.strip() if ok else dev["serial"][:18]
        if not ok or "unauthorized" in out.lower():
            QMessageBox.warning(self, "需要授权",
                "手机端正在弹出 USB 调试授权请求，请点击「允许」后重试。")
            return
        resp = QMessageBox.question(self, "连接成功",
            f"已连接到“{model}”。\n是否确认投屏？",
            QMessageBox.Yes | QMessageBox.No)
        if resp == QMessageBox.Yes:
            self.start_mirror_one(dev["serial"])

    # ---------- 投屏 ----------
    def _scrcpy_args(self, serial):
        args = [SCRCPY, "-s", serial,
                "--audio",
                "--keyboard=uhid",
                "--stay-awake",
                "--turn-screen-off"]
        width = self.width_combo.currentText()
        if width and width != "0":
            args += ["--max-size", width]
        if self.blank_check.isChecked():
            args += ["--window-borderless"]
        title = f"{APP_NAME}"
        args += ["--window-title", title]
        return args

    def start_mirror_one(self, serial):
        if serial in self._mirroring:
            return
        args = self._scrcpy_args(serial)
        try:
            p = subprocess.Popen(args, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            self._mirroring[serial] = p
            self._cfg["conn_addr"] = serial
            self.save_config()
            self._render_devices()
            self.status_lbl.setText(f"正在投屏：{serial}")
            self.tray.showMessage(APP_NAME, f"已开始投屏 {serial}", QSystemTrayIcon.Information, 2000)
        except FileNotFoundError:
            QMessageBox.critical(self, "缺少 scrcpy",
                "未找到 scrcpy，请先安装依赖：\nsudo apt install adb scrcpy")

    def start_mirror_all(self):
        if not self._devices:
            QMessageBox.information(self, "提示", "暂未检测到设备，请先连接手机。")
            return
        self.start_mirror_one(self._devices[0]["serial"])

    def stop_all(self):
        for serial, p in list(self._mirroring.items()):
            try:
                p.terminate()
            except Exception:
                pass
        self._mirroring.clear()
        self._render_devices()
        self.status_lbl.setText("已停止投屏")

    def on_tray_activated(self, reason):
        if reason == QSystemTrayIcon.DoubleClick:
            self.show_normal()

    def show_normal(self):
        self.showNormal()
        self.raise_()
        self.activateWindow()

    # ---------- 设置 ----------
    def on_width_changed(self, text):
        self._cfg["window_width"] = text
        self.save_config()

    def on_top_toggled(self, on):
        self._cfg["always_on_top"] = "1" if on else "0"
        self.setWindowFlag(Qt.WindowStaysOnTopHint, on)
        self.show()
        self.save_config()

    def on_blank_toggled(self, on):
        self._cfg["borderless"] = "1" if on else "0"
        self.save_config()

    def _apply_config(self):
        self.setWindowFlag(Qt.WindowStaysOnTopHint, self._cfg["always_on_top"] == "1")

    def open_keyboard_help(self):
        ok, out = run_cmd([ADB, "shell", "ime", "list", "-s"], timeout=10)
        imes = [l.strip() for l in out.splitlines() if l.strip()] if ok else []
        hint = ("在手机上：设置 → 系统 → 语言与输入法 → 当前输入法\n"
                "首次使用请选择「English(US)」或系统英文键盘，\n"
                "之后在电脑窗口打字，中文即可正常输入。\n")
        if imes:
            hint += "\n手机当前输入法：\n" + "\n".join(imes[:6])
        QMessageBox.information(self, "配置手机键盘布局", hint)

    # ---------- 退出 ----------
    def _maybe_auto_start(self):
        if self._cfg["auto_start"] == "1" and self._devices:
            self.start_mirror_all()

    def closeEvent(self, event):
        event.ignore()
        self.hide()
        self.tray.showMessage(APP_NAME, "已驻留系统托盘，右键托盘可操作", QSystemTrayIcon.Information, 2000)

    def quit_app(self):
        self.stop_all()
        self._scanner.stop()
        self._scanner.wait(2000)
        QApplication.instance().quit()


def main():
    app = QApplication(sys.argv)
    app.setApplicationName(APP_NAME)
    app.setQuitOnLastWindowClosed(False)
    app.setStyle("Fusion")
    app.setStyleSheet(DARK_QSS)
    w = MainWindow()
    w.show()
    sys.exit(app.exec())


if __name__ == "__main__":
    main()
