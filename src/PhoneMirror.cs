using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace PhoneMirror
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (new Mutex(true, "PhoneMirrorAppSingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("手机投屏助手已在运行，请查看任务栏或系统托盘。", "手机投屏助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }

    public class Dev
    {
        public string Serial;
        public string Model;
    }

    class MainForm : Form
    {
        string _baseDir;
        string _scrcpyDir;
        string _scrcpyExe;
        string _adbExe;
        string _configPath;
        string _iconPath;

        string _connAddr = "";
        int _windowWidth = 300;
        bool _alwaysOnTop = false;
        bool _borderless = false;
        string _mode = "auto";
        bool _autoStart = true;

        Process _mirrorProc;
        volatile bool _refreshing;
        bool _reallyExit;

        string _currentSerial;
        string _currentModel;

        NotifyIcon _tray;
        System.Windows.Forms.Timer _timer;

        Panel _header;
        Panel _status;
        Panel _dot;
        Label _lblStatus;
        Label _lblSub;
        Button _btnRefresh;
        ComboBox _cmbMode;
        TextBox _txtWireless;
        Button _btnStart;
        Button _btnStop;
        ComboBox _cmbWidth;
        CheckBox _chkTop;
        CheckBox _chkBorder;
        Button _btnKeyboard;
        Button _btnQuit;

        static readonly Color C_BG = Color.FromArgb(255, 18, 20, 28);
        static readonly Color C_CARD = Color.FromArgb(255, 26, 30, 43);
        static readonly Color C_BORDER = Color.FromArgb(255, 43, 49, 69);
        static readonly Color C_TEXT = Color.FromArgb(255, 229, 231, 235);
        static readonly Color C_SUB = Color.FromArgb(255, 156, 163, 175);
        static readonly Color C_ACCENT = Color.FromArgb(255, 59, 130, 246);
        static readonly Color C_GREEN = Color.FromArgb(255, 34, 197, 94);
        static readonly Color C_GRAY = Color.FromArgb(255, 100, 116, 139);

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        public MainForm()
        {
            _baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _scrcpyDir = Path.Combine(_baseDir, "scrcpy");
            _scrcpyExe = Path.Combine(_scrcpyDir, "scrcpy.exe");
            _adbExe = Path.Combine(_scrcpyDir, "adb.exe");
            _configPath = Path.Combine(_baseDir, "config.ini");
            _iconPath = Path.Combine(_baseDir, "手机投屏.ico");

            LoadConfig();
            BuildUi();
            BuildTray();

            Shown += OnShown;
            FormClosing += OnFormClosing;

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 5000;
            _timer.Tick += delegate { RefreshStatus(); };
            _timer.Start();
        }

        // ---------- config ----------
        void LoadConfig()
        {
            try
            {
                if (!File.Exists(_configPath)) return;
                foreach (string raw in File.ReadAllLines(_configPath, Encoding.UTF8))
                {
                    string t = raw.Trim();
                    if (t.Length == 0 || t[0] == '[' || t[0] == ';' || t[0] == '#') continue;
                    int eq = t.IndexOf('=');
                    if (eq < 1) continue;
                    string k = t.Substring(0, eq).Trim();
                    string v = t.Substring(eq + 1).Trim();
                    if (k == "conn_addr") _connAddr = v;
                    else if (k == "window_width") { int w; if (int.TryParse(v, out w) && w > 0) _windowWidth = w; }
                    else if (k == "always_on_top") _alwaysOnTop = v == "1";
                    else if (k == "borderless") _borderless = v == "1";
                    else if (k == "mode") { if (v == "usb" || v == "wifi" || v == "auto") _mode = v; }
                    else if (k == "auto_start") _autoStart = v == "1";
                }
            }
            catch { }
        }

        void SaveConfig()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[settings]");
                sb.AppendLine("conn_addr=" + _txtWireless.Text.Trim());
                sb.AppendLine("window_width=" + _cmbWidth.Text);
                sb.AppendLine("always_on_top=" + (_chkTop.Checked ? "1" : "0"));
                sb.AppendLine("borderless=" + (_chkBorder.Checked ? "1" : "0"));
                sb.AppendLine("mode=" + _mode);
                sb.AppendLine("auto_start=" + (_autoStart ? "1" : "0"));
                File.WriteAllText(_configPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        // ---------- UI ----------
        void BuildUi()
        {
            Text = "手机投屏助手";
            FormBorderStyle = FormBorderStyle.None;
            BackColor = C_BG;
            ClientSize = new Size(470, 500);
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;

            Font titleFont = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold);
            Font bodyFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            Font smallFont = new Font("Microsoft YaHei UI", 8F, FontStyle.Regular);

            // header
            _header = new Panel();
            _header.Bounds = new Rectangle(0, 0, 470, 84);
            _header.Paint += HeaderPaint;
            _header.MouseDown += HeaderMouseDown;
            Controls.Add(_header);

            PictureBox iconBox = new PictureBox();
            iconBox.Bounds = new Rectangle(22, 22, 40, 40);
            iconBox.SizeMode = PictureBoxSizeMode.Zoom;
            try
            {
                string previewPath = Path.Combine(_baseDir, "手机投屏图标预览.png");
                iconBox.Image = File.Exists(previewPath) ? Image.FromFile(previewPath) : SystemIcons.Application.ToBitmap();
            }
            catch { iconBox.Image = SystemIcons.Application.ToBitmap(); }
            _header.Controls.Add(iconBox);

            Label title = new Label();
            title.Text = "手机投屏助手";
            title.Font = titleFont;
            title.ForeColor = C_TEXT;
            title.AutoSize = false;
            title.Bounds = new Rectangle(74, 20, 260, 32);
            title.TextAlign = ContentAlignment.MiddleLeft;
            _header.Controls.Add(title);

            Label sub = new Label();
            sub.Text = "USB / 无线 一键投屏  ·  豆包 AI 协作开发";
            sub.Font = smallFont;
            sub.ForeColor = C_SUB;
            sub.AutoSize = false;
            sub.Bounds = new Rectangle(76, 52, 260, 20);
            sub.TextAlign = ContentAlignment.MiddleLeft;
            _header.Controls.Add(sub);

            Button btnMin = MakeCaptionButton("—", new Point(398, 14));
            btnMin.Click += delegate { ToTray(); };
            _header.Controls.Add(btnMin);

            Button btnClose = MakeCaptionButton("✕", new Point(434, 14));
            btnClose.Click += delegate { ToTray(); };
            _header.Controls.Add(btnClose);

            // status card
            _status = new Panel();
            _status.Bounds = new Rectangle(20, 100, 430, 96);
            _status.BackColor = C_BG;
            _status.Paint += StatusPaint;
            Controls.Add(_status);

            _dot = new Panel();
            _dot.Bounds = new Rectangle(24, 34, 12, 12);
            _dot.BackColor = C_GRAY;
            SetRoundRegion(_dot, 6);
            _status.Controls.Add(_dot);

            _lblStatus = new Label();
            _lblStatus.Text = "正在检测设备…";
            _lblStatus.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            _lblStatus.ForeColor = C_TEXT;
            _lblStatus.AutoSize = false;
            _lblStatus.Bounds = new Rectangle(48, 22, 330, 30);
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            _status.Controls.Add(_lblStatus);

            _lblSub = new Label();
            _lblSub.Text = "插上数据线，或配置无线地址后点击「刷新」";
            _lblSub.Font = smallFont;
            _lblSub.ForeColor = C_SUB;
            _lblSub.AutoSize = false;
            _lblSub.Bounds = new Rectangle(48, 52, 340, 22);
            _lblSub.TextAlign = ContentAlignment.MiddleLeft;
            _status.Controls.Add(_lblSub);

            _btnRefresh = MakeFlatButton("刷新", new Rectangle(352, 30, 64, 30), C_CARD, C_TEXT);
            _btnRefresh.Click += delegate { RefreshStatus(); };
            _status.Controls.Add(_btnRefresh);

            // mode row
            Label lblMode = new Label();
            lblMode.Text = "连接方式";
            lblMode.Font = bodyFont;
            lblMode.ForeColor = C_SUB;
            lblMode.Bounds = new Rectangle(20, 220, 80, 30);
            lblMode.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(lblMode);

            _cmbMode = new ComboBox();
            _cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbMode.FlatStyle = FlatStyle.Flat;
            _cmbMode.BackColor = C_CARD;
            _cmbMode.ForeColor = C_TEXT;
            _cmbMode.Font = bodyFont;
            _cmbMode.Bounds = new Rectangle(104, 216, 210, 30);
            _cmbMode.Items.Add("自动（USB 优先）");
            _cmbMode.Items.Add("仅 USB");
            _cmbMode.Items.Add("仅无线");
            if (_mode == "usb") _cmbMode.SelectedIndex = 1;
            else if (_mode == "wifi") _cmbMode.SelectedIndex = 2;
            else _cmbMode.SelectedIndex = 0;
            _cmbMode.SelectedIndexChanged += delegate
            {
                int i = _cmbMode.SelectedIndex;
                _mode = i == 1 ? "usb" : (i == 2 ? "wifi" : "auto");
            };
            Controls.Add(_cmbMode);

            // wireless row
            Label lblWifi = new Label();
            lblWifi.Text = "无线地址";
            lblWifi.Font = bodyFont;
            lblWifi.ForeColor = C_SUB;
            lblWifi.Bounds = new Rectangle(20, 256, 80, 30);
            lblWifi.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(lblWifi);

            _txtWireless = new TextBox();
            _txtWireless.Text = _connAddr;
            _txtWireless.Font = bodyFont;
            _txtWireless.BackColor = C_CARD;
            _txtWireless.ForeColor = C_TEXT;
            _txtWireless.BorderStyle = BorderStyle.FixedSingle;
            _txtWireless.Bounds = new Rectangle(104, 252, 300, 30);
            Controls.Add(_txtWireless);

            // start / stop
            _btnStart = MakeFlatButton("▶  开始投屏", new Rectangle(20, 296, 205, 48), C_ACCENT, Color.White);
            _btnStart.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            _btnStart.Click += delegate { StartMirror(); };
            Controls.Add(_btnStart);

            _btnStop = MakeFlatButton("■  停止投屏", new Rectangle(245, 296, 205, 48), C_CARD, C_TEXT);
            _btnStop.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            _btnStop.Enabled = false;
            _btnStop.Click += delegate { StopMirror(); };
            Controls.Add(_btnStop);

            // options row
            Label lblWidth = new Label();
            lblWidth.Text = "窗口宽度";
            lblWidth.Font = bodyFont;
            lblWidth.ForeColor = C_SUB;
            lblWidth.Bounds = new Rectangle(20, 362, 80, 30);
            lblWidth.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(lblWidth);

            _cmbWidth = new ComboBox();
            _cmbWidth.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbWidth.FlatStyle = FlatStyle.Flat;
            _cmbWidth.BackColor = C_CARD;
            _cmbWidth.ForeColor = C_TEXT;
            _cmbWidth.Font = bodyFont;
            _cmbWidth.Bounds = new Rectangle(104, 358, 90, 30);
            _cmbWidth.Items.Add("300");
            _cmbWidth.Items.Add("400");
            _cmbWidth.Items.Add("600");
            _cmbWidth.Items.Add("800");
            _cmbWidth.Items.Add("1200");
            string wText = _windowWidth.ToString();
            int wi = _cmbWidth.Items.IndexOf(wText);
            _cmbWidth.SelectedIndex = wi >= 0 ? wi : 0;
            Controls.Add(_cmbWidth);

            _chkTop = new CheckBox();
            _chkTop.Text = "窗口置顶";
            _chkTop.Checked = _alwaysOnTop;
            _chkTop.Font = bodyFont;
            _chkTop.ForeColor = C_TEXT;
            _chkTop.Bounds = new Rectangle(210, 360, 100, 26);
            Controls.Add(_chkTop);

            _chkBorder = new CheckBox();
            _chkBorder.Text = "无边框";
            _chkBorder.Checked = _borderless;
            _chkBorder.Font = bodyFont;
            _chkBorder.ForeColor = C_TEXT;
            _chkBorder.Bounds = new Rectangle(320, 360, 90, 26);
            Controls.Add(_chkBorder);

            // footer
            _btnKeyboard = MakeFlatButton("⌨  配置手机键盘布局（解决打字无字）", new Rectangle(20, 404, 300, 38), C_CARD, C_SUB);
            _btnKeyboard.Click += delegate { OpenKeyboardSettings(); };
            Controls.Add(_btnKeyboard);

            _btnQuit = MakeFlatButton("退出", new Rectangle(340, 404, 110, 38), Color.FromArgb(255, 70, 30, 40), C_TEXT);
            _btnQuit.Click += delegate { _reallyExit = true; Close(); };
            Controls.Add(_btnQuit);

            Label tip = new Label();
            tip.Text = "关闭窗口后驻留系统托盘 · 右键托盘图标查看更多操作";
            tip.Font = smallFont;
            tip.ForeColor = C_SUB;
            tip.AutoSize = false;
            tip.Bounds = new Rectangle(20, 458, 430, 20);
            tip.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(tip);

            SetRoundRegion(this, 18);
            _dot.BringToFront();
        }

        Button MakeCaptionButton(string text, Point loc)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Color.FromArgb(255, 42, 48, 70);
            b.ForeColor = C_SUB;
            b.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            b.Size = new Size(30, 30);
            b.Location = loc;
            b.MouseEnter += delegate { b.BackColor = Color.FromArgb(255, 58, 66, 94); };
            b.MouseLeave += delegate { b.BackColor = Color.FromArgb(255, 42, 48, 70); };
            return b;
        }

        Button MakeFlatButton(string text, Rectangle r, Color back, Color fore)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = back;
            b.ForeColor = fore;
            b.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            b.Bounds = r;
            b.MouseEnter += delegate
            {
                Color c = b.BackColor;
                b.BackColor = Color.FromArgb(255, Math.Min(255, c.R + 18), Math.Min(255, c.G + 18), Math.Min(255, c.B + 18));
            };
            b.MouseLeave += delegate { b.BackColor = back; };
            return b;
        }

        void SetRoundRegion(Control c, int rad)
        {
            using (GraphicsPath p = MakeRounded(new Rectangle(0, 0, c.Width, c.Height), rad))
            {
                c.Region = new Region(p);
            }
        }

        GraphicsPath MakeRounded(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        void HeaderPaint(object s, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, 0, _header.Width, _header.Height),
                Color.FromArgb(255, 43, 50, 90), Color.FromArgb(255, 23, 26, 40), 90F))
            {
                e.Graphics.FillRectangle(b, 0, 0, _header.Width, _header.Height);
            }
            using (Pen pen = new Pen(C_BORDER))
            {
                e.Graphics.DrawLine(pen, 0, _header.Height - 1, _header.Width, _header.Height - 1);
            }
        }

        void StatusPaint(object s, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            GraphicsPath p = MakeRounded(new Rectangle(0, 0, _status.Width - 1, _status.Height - 1), 12);
            using (SolidBrush b = new SolidBrush(C_CARD)) e.Graphics.FillPath(b, p);
            using (Pen pen = new Pen(C_BORDER)) e.Graphics.DrawPath(pen, p);
        }

        void HeaderMouseDown(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
            }
        }

        Icon GetAppIcon()
        {
            try
            {
                if (File.Exists(_iconPath)) return new Icon(_iconPath);
            }
            catch { }
            return SystemIcons.Application;
        }

        // ---------- tray ----------
        void BuildTray()
        {
            _tray = new NotifyIcon();
            _tray.Icon = GetAppIcon();
            _tray.Text = "手机投屏助手";
            _tray.Visible = true;
            _tray.DoubleClick += delegate { ShowMain(); };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = C_CARD;
            menu.ForeColor = C_TEXT;
            menu.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());

            ToolStripMenuItem mShow = new ToolStripMenuItem("显示主界面");
            mShow.Click += delegate { ShowMain(); };
            menu.Items.Add(mShow);

            ToolStripMenuItem mStart = new ToolStripMenuItem("开始投屏");
            mStart.Click += delegate { StartMirror(); };
            menu.Items.Add(mStart);

            ToolStripMenuItem mStop = new ToolStripMenuItem("停止投屏");
            mStop.Click += delegate { StopMirror(); };
            menu.Items.Add(mStop);

            ToolStripMenuItem mKey = new ToolStripMenuItem("配置手机键盘布局");
            mKey.Click += delegate { OpenKeyboardSettings(); };
            menu.Items.Add(mKey);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem mExit = new ToolStripMenuItem("退出");
            mExit.Click += delegate { _reallyExit = true; Close(); };
            menu.Items.Add(mExit);

            _tray.ContextMenuStrip = menu;
        }

        class DarkColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Color.FromArgb(255, 26, 30, 43); } }
            public override Color ImageMarginGradientBegin { get { return Color.FromArgb(255, 26, 30, 43); } }
            public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(255, 26, 30, 43); } }
            public override Color ImageMarginGradientEnd { get { return Color.FromArgb(255, 26, 30, 43); } }
            public override Color MenuItemSelected { get { return Color.FromArgb(255, 59, 130, 246); } }
            public override Color MenuItemBorder { get { return Color.FromArgb(255, 59, 130, 246); } }
            public override Color MenuBorder { get { return Color.FromArgb(255, 43, 49, 69); } }
            public override Color SeparatorDark { get { return Color.FromArgb(255, 43, 49, 69); } }
            public override Color SeparatorLight { get { return Color.FromArgb(255, 43, 49, 69); } }
        }

        void ShowMain()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        void ToTray()
        {
            Hide();
        }

        // ---------- adb ----------
        void ParseDevices(List<Dev> usb, List<Dev> wifi)
        {
            try
            {
                Process p = new Process();
                p.StartInfo.FileName = _adbExe;
                p.StartInfo.Arguments = "devices -l";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardOutput = true;
                if (!p.Start()) return;
                string all = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(5000)) { try { p.Kill(); } catch { } return; }
                string[] lines = all.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("List") || t.StartsWith("*")) continue;
                    string[] parts = t.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2 || parts[1] != "device") continue;
                    string serial = parts[0];
                    string model = "";
                    foreach (string part in parts)
                    {
                        if (part.StartsWith("model:")) { model = part.Substring(6); break; }
                    }
                    if (model.Length == 0) model = serial;
                    Dev d = new Dev();
                    d.Serial = serial;
                    d.Model = model;
                    if (serial.IndexOf(':') >= 0) wifi.Add(d); else usb.Add(d);
                }
            }
            catch { }
        }

        Dev FindWifiTarget(List<Dev> wifi)
        {
            string addr = _txtWireless.Text.Trim();
            if (addr.Length > 0)
            {
                foreach (Dev d in wifi) if (d.Serial == addr) return d;
            }
            if (wifi.Count > 0) return wifi[0];
            return null;
        }

        bool PickTarget()
        {
            List<Dev> usb = new List<Dev>();
            List<Dev> wifi = new List<Dev>();
            ParseDevices(usb, wifi);
            Dev t = null;
            if (_mode == "usb")
            {
                if (usb.Count > 0) t = usb[0];
            }
            else if (_mode == "wifi")
            {
                t = FindWifiTarget(wifi);
            }
            else
            {
                if (usb.Count > 0) t = usb[0];
                else t = FindWifiTarget(wifi);
            }
            if (t == null)
            {
                _currentSerial = null;
                _currentModel = null;
                return false;
            }
            _currentSerial = t.Serial;
            _currentModel = t.Model;
            return true;
        }

        void RefreshStatus()
        {
            if (_refreshing) return;
            _refreshing = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<Dev> usb = new List<Dev>();
                List<Dev> wifi = new List<Dev>();
                ParseDevices(usb, wifi);
                string serial = _currentSerial;
                string model = _currentModel;
                bool running = _mirrorProc != null && !_mirrorProc.HasExited;
                try
                {
                    BeginInvoke((Action)delegate { ApplyStatus(usb, wifi, running, serial, model); });
                }
                catch { }
                _refreshing = false;
            });
        }

        void ApplyStatus(List<Dev> usb, List<Dev> wifi, bool running, string serial, string model)
        {
            Dev shown = null;
            bool showWifi = false;
            if (usb.Count > 0) { shown = usb[0]; showWifi = false; }
            else if (wifi.Count > 0) { shown = FindWifiTarget(wifi); if (shown == null && wifi.Count > 0) { shown = wifi[0]; } showWifi = true; }

            if (shown != null)
            {
                _lblStatus.Text = "设备在线 · " + shown.Model;
                _dot.BackColor = C_GREEN;
                string modeText = showWifi ? "无线" : "USB";
                string addr = showWifi ? shown.Serial : shown.Serial;
                _lblSub.Text = modeText + " · " + addr + (running ? "  ·  投屏运行中" : "  ·  点击「开始投屏」");
                if (running)
                {
                    _btnStart.Enabled = false;
                    _btnStop.Enabled = true;
                }
                else
                {
                    _btnStart.Enabled = true;
                    _btnStop.Enabled = false;
                }
            }
            else
            {
                _lblStatus.Text = "未检测到设备";
                _dot.BackColor = C_GRAY;
                _lblSub.Text = "USB：插上数据线并在手机上授权  /  无线：配对后填地址";
                _btnStart.Enabled = true;
                _btnStop.Enabled = false;
            }
        }

        // ---------- mirror ----------
        void StartMirror()
        {
            if (_mirrorProc != null && !_mirrorProc.HasExited)
            {
                MessageBox.Show("投屏已在运行中，如需重新连接请先停止。", "手机投屏助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!File.Exists(_scrcpyExe))
            {
                MessageBox.Show("未找到 scrcpy\\scrcpy.exe，请确认软件目录完整。", "手机投屏助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!PickTarget())
            {
                MessageBox.Show("未检测到可用的设备。\n\nUSB：插上数据线，并在手机上点「允许 USB 调试」；\n无线：先完成无线调试配对，再填入 IP:端口。",
                    "手机投屏助手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                StringBuilder a = new StringBuilder();
                a.Append("-s ").Append(_currentSerial);
                if (_chkBorder.Checked) a.Append(" --window-borderless");
                a.Append(" --window-width=").Append(_cmbWidth.Text);
                if (_chkTop.Checked) a.Append(" --always-on-top");
                a.Append(" --stay-awake --keyboard=uhid");

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = _scrcpyExe;
                psi.Arguments = a.ToString();
                psi.WorkingDirectory = _scrcpyDir;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                _mirrorProc = Process.Start(psi);
                UpdateMirrorButtons(true);
                _lblStatus.Text = "设备在线 · " + _currentModel;
                _lblSub.Text = (_currentSerial.IndexOf(':') >= 0 ? "无线" : "USB") + " · " + _currentSerial + "  ·  投屏运行中";
                _dot.BackColor = C_GREEN;
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动投屏失败：\n" + ex.Message, "手机投屏助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void StopMirror()
        {
            if (_mirrorProc != null && !_mirrorProc.HasExited)
            {
                try { _mirrorProc.Kill(); } catch { }
            }
            _mirrorProc = null;
            UpdateMirrorButtons(false);
            RefreshStatus();
        }

        void UpdateMirrorButtons(bool running)
        {
            _btnStart.Enabled = !running;
            _btnStop.Enabled = running;
        }

        void OpenKeyboardSettings()
        {
            if (!PickTarget())
            {
                MessageBox.Show("未检测到设备，请先连接手机（USB 或无线）。", "手机投屏助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                Process p = new Process();
                p.StartInfo.FileName = _adbExe;
                p.StartInfo.Arguments = "-s " + _currentSerial + " shell am start -a android.settings.HARD_KEYBOARD_SETTINGS";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开手机键盘设置失败：\n" + ex.Message, "手机投屏助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------- lifecycle ----------
        void OnShown(object s, EventArgs e)
        {
            RefreshStatus();
            if (_autoStart)
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    Thread.Sleep(1200);
                    try
                    {
                        BeginInvoke((Action)delegate
                        {
                            if (_mirrorProc == null || _mirrorProc.HasExited) StartMirror();
                        });
                    }
                    catch { }
                });
            }
        }

        void OnFormClosing(object s, FormClosingEventArgs e)
        {
            if (!_reallyExit)
            {
                e.Cancel = true;
                ToTray();
                return;
            }
            try
            {
                if (_mirrorProc != null && !_mirrorProc.HasExited) _mirrorProc.Kill();
            }
            catch { }
            SaveConfig();
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
        }
    }
}
