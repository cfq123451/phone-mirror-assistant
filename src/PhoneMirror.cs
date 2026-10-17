using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
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

        Dictionary<string, Process> _procs = new Dictionary<string, Process>();
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
        Button _btnStart;
        Button _btnStop;
        ComboBox _cmbWidth;
        CheckBox _chkTop;
        CheckBox _chkBorder;
        Button _btnKeyboard;
        Button _btnQuit;
        Button _btnPair;
        Button _btnUsb;
        List<DeviceRow> _rows = new List<DeviceRow>();
        Panel _devList;

        static readonly Color C_BG = Color.FromArgb(255, 22, 24, 31);
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
        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr hIcon);

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
                sb.AppendLine("conn_addr=" + _connAddr.Trim());
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
            ClientSize = new Size(480, 724);
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;

            Font titleFont = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold);
            Font bodyFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            Font smallFont = new Font("Microsoft YaHei UI", 8F, FontStyle.Regular);

            // header
            _header = new Panel();
            _header.Bounds = new Rectangle(0, 0, 480, 104);
            _header.BackColor = Color.FromArgb(255, 26, 29, 38);   // 与标题渐变近似，供按钮四角对齐
            _header.Paint += HeaderPaint;
            _header.MouseDown += HeaderMouseDown;
            Controls.Add(_header);

            // 自绘圆形图标：蓝色渐变圆底 + 白色手机轮廓
            Panel iconBox = new Panel();
            iconBox.Bounds = new Rectangle(24, 22, 44, 44);
            iconBox.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rc = new Rectangle(0, 0, 43, 43);
                using (GraphicsPath p = UiDraw.Rounded(rc, 22))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(rc,
                        Color.FromArgb(255, 76, 128, 255), Color.FromArgb(255, 45, 84, 216), 90F))
                    {
                        e.Graphics.FillPath(b, p);
                    }
                    using (Pen pen = new Pen(Color.FromArgb(255, 110, 160, 255)))
                    {
                        e.Graphics.DrawPath(pen, p);
                    }
                }
                // 手机图形
                using (Pen wp = new Pen(Color.White, 2.2F))
                {
                    wp.StartCap = LineCap.Round;
                    wp.EndCap = LineCap.Round;
                    Rectangle body = new Rectangle(15, 10, 14, 24);
                    using (GraphicsPath bp = UiDraw.Rounded(new Rectangle(body.X, body.Y, body.Width, body.Height), 3))
                    {
                        e.Graphics.DrawPath(wp, bp);
                    }
                    e.Graphics.DrawEllipse(wp, 20, 29, 4, 4);
                }
            };
            _header.Controls.Add(iconBox);

            Label title = new Label();
            title.Text = "手机投屏助手";
            title.Font = titleFont;
            title.ForeColor = C_TEXT;
            title.AutoSize = false;
            title.Bounds = new Rectangle(76, 16, 240, 34);
            title.TextAlign = ContentAlignment.MiddleLeft;
            _header.Controls.Add(title);

            Label sub = new Label();
            sub.Text = "无线 / USB 一键投屏";
            sub.Font = smallFont;
            sub.ForeColor = C_SUB;
            sub.AutoSize = false;
            sub.Bounds = new Rectangle(78, 52, 220, 20);
            sub.TextAlign = ContentAlignment.MiddleLeft;
            _header.Controls.Add(sub);

            RoundButton btnMin = MakeCaptionButton("—", new Point(396, 18));
            btnMin.Click += delegate { ToTray(); };
            _header.Controls.Add(btnMin);

            RoundButton btnClose = MakeCaptionButton("✕", new Point(434, 18));
            btnClose.Click += delegate { ToTray(); };
            _header.Controls.Add(btnClose);

            // 设备列表标题 + 刷新
            Label lblDevTitle = new Label();
            lblDevTitle.Text = "设备列表";
            lblDevTitle.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
            lblDevTitle.ForeColor = C_TEXT;
            lblDevTitle.Bounds = new Rectangle(30, 130, 100, 26);
            lblDevTitle.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(lblDevTitle);

            _btnRefresh = MakeFlatButton("⟳  刷新", new Rectangle(376, 126, 80, 34), Color.FromArgb(255, 46, 52, 66), Color.White);
            _btnRefresh.Click += delegate { RefreshStatus(); };
            Controls.Add(_btnRefresh);

            // 设备列表卡片
            _status = new Panel();
            _status.Bounds = new Rectangle(30, 162, 420, 262);
            _status.BackColor = C_BG;
            _status.Paint += StatusPaint;
            Controls.Add(_status);

            _dot = new Panel();
            _dot.Bounds = new Rectangle(16, 17, 10, 10);
            _dot.BackColor = C_GRAY;
            SetRoundRegion(_dot, 5);
            _status.Controls.Add(_dot);

            _lblStatus = new Label();
            _lblStatus.Text = "正在检测设备…";
            _lblStatus.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
            _lblStatus.ForeColor = C_TEXT;
            _lblStatus.AutoSize = false;
            _lblStatus.Bounds = new Rectangle(34, 8, 240, 26);
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            _status.Controls.Add(_lblStatus);

            _lblSub = new Label();
            _lblSub.Text = "插上数据线，或无线配对后自动发现";
            _lblSub.Font = smallFont;
            _lblSub.ForeColor = C_SUB;
            _lblSub.AutoSize = false;
            _lblSub.Bounds = new Rectangle(34, 34, 376, 18);
            _lblSub.TextAlign = ContentAlignment.MiddleLeft;
            _status.Controls.Add(_lblSub);

            // 设备列表：可滚动动态容器（支持多台设备）
            _devList = new Panel();
            _devList.Bounds = new Rectangle(10, 54, 400, 200);
            _devList.BackColor = Color.FromArgb(255, 28, 32, 43);
            _devList.AutoScroll = true;
            _devList.HorizontalScroll.Enabled = false;
            _status.Controls.Add(_devList);

            // 连接引导：无线配对 / USB 连接（紧凑小按钮）
            _btnPair = MakeFlatButton("无线配对", new Rectangle(100, 442, 130, 36), Color.FromArgb(255, 63, 128, 245), Color.White);
            _btnPair.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            _btnPair.Click += delegate { ShowPairForm(); };
            Controls.Add(_btnPair);

            _btnUsb = MakeFlatButton("USB 连接", new Rectangle(250, 442, 130, 36), Color.FromArgb(255, 52, 94, 196), Color.White);
            _btnUsb.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            _btnUsb.Click += delegate { UsbConnect(); };
            Controls.Add(_btnUsb);

            // start / stop —— 橙色开始，蓝色停止
            _btnStart = MakeFlatButton("▶  开始投屏", new Rectangle(30, 500, 200, 54), Color.FromArgb(255, 238, 130, 40), Color.White);
            _btnStart.Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold);
            _btnStart.Click += delegate { StartMirror(); };
            Controls.Add(_btnStart);

            _btnStop = MakeFlatButton("■  停止投屏", new Rectangle(250, 500, 200, 54), Color.FromArgb(255, 206, 62, 74), Color.White);
            _btnStop.Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold);
            _btnStop.Enabled = false;
            _btnStop.Click += delegate { StopMirror(); };
            Controls.Add(_btnStop);

            // options row
            Label lblWidth = new Label();
            lblWidth.Text = "窗口宽度";
            lblWidth.Font = bodyFont;
            lblWidth.ForeColor = C_SUB;
            lblWidth.Bounds = new Rectangle(30, 584, 80, 30);
            lblWidth.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(lblWidth);

            _cmbWidth = new ComboBox();
            _cmbWidth.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbWidth.FlatStyle = FlatStyle.Flat;
            _cmbWidth.BackColor = C_CARD;
            _cmbWidth.ForeColor = C_TEXT;
            _cmbWidth.Font = bodyFont;
            _cmbWidth.Bounds = new Rectangle(110, 580, 90, 32);
            _cmbWidth.Items.Add("300");
            _cmbWidth.Items.Add("400");
            _cmbWidth.Items.Add("600");
            _cmbWidth.Items.Add("800");
            _cmbWidth.Items.Add("1200");
            string wText = _windowWidth.ToString();
            int wi = _cmbWidth.Items.IndexOf(wText);
            _cmbWidth.SelectedIndex = wi >= 0 ? wi : 0;
            Controls.Add(_cmbWidth);

            _chkTop = new RoundCheckBox();
            _chkTop.Text = "窗口置顶";
            _chkTop.Checked = _alwaysOnTop;
            _chkTop.Font = bodyFont;
            _chkTop.ForeColor = C_TEXT;
            _chkTop.Bounds = new Rectangle(230, 584, 100, 26);
            Controls.Add(_chkTop);

            _chkBorder = new RoundCheckBox();
            _chkBorder.Text = "无边框";
            _chkBorder.Checked = _borderless;
            _chkBorder.Font = bodyFont;
            _chkBorder.ForeColor = C_TEXT;
            _chkBorder.Bounds = new Rectangle(345, 584, 90, 26);
            Controls.Add(_chkBorder);

            // footer：键盘配置 + 退出
            _btnKeyboard = MakeFlatButton("配置手机键盘布局", new Rectangle(30, 636, 150, 42), Color.FromArgb(255, 46, 52, 66), C_SUB);
            _btnKeyboard.Click += delegate { OpenKeyboardSettings(); };
            Controls.Add(_btnKeyboard);

            _btnQuit = MakeFlatButton("退出", new Rectangle(280, 636, 170, 42), Color.FromArgb(255, 58, 63, 78), C_TEXT);
            _btnQuit.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
            _btnQuit.Click += delegate { _reallyExit = true; Close(); };
            Controls.Add(_btnQuit);

            Label tip = new Label();
            tip.Text = "关闭窗口后驻留系统托盘 · 右键托盘图标查看更多操作";
            tip.Font = smallFont;
            tip.ForeColor = C_SUB;
            tip.AutoSize = false;
            tip.Bounds = new Rectangle(30, 696, 420, 16);
            tip.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(tip);

            SetRoundRegion(this, 18);
            _dot.BringToFront();
        }

        RoundButton MakeCaptionButton(string text, Point loc)
        {
            RoundButton b = new RoundButton();
            b.Text = text;
            b.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            b.ForeColor = C_SUB;
            b.BackColor = Color.FromArgb(255, 42, 48, 70);
            b.GradientTop = Color.FromArgb(255, 46, 53, 78);
            b.GradientBottom = Color.FromArgb(255, 38, 44, 64);
            b.HoverTop = Color.FromArgb(255, 66, 74, 104);
            b.HoverBottom = Color.FromArgb(255, 54, 62, 88);
            b.BorderColor = Color.FromArgb(255, 60, 68, 96);
            b.Radius = 15;
            b.Size = new Size(30, 30);
            b.Location = loc;
            return b;
        }

        RoundButton MakeFlatButton(string text, Rectangle r, Color back, Color fore)
        {
            RoundButton b = new RoundButton();
            b.Text = text;
            b.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            b.ForeColor = fore;
            b.BackColor = back;
            b.GradientTop = UiDraw.Lighten(back, 8);
            b.GradientBottom = back;
            b.HoverTop = UiDraw.Lighten(back, 26);
            b.HoverBottom = UiDraw.Lighten(back, 12);
            // 圆角按尺寸分级：大按钮 16px，中小按钮 10px
            b.Radius = r.Height >= 48 ? 16 : 10;
            // 统一细边框：低对比蓝灰
            b.BorderColor = Color.FromArgb(255, 66, 74, 94);
            b.Bounds = r;
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
            Rectangle rc = new Rectangle(0, 0, _header.Width, _header.Height);
            using (LinearGradientBrush b = new LinearGradientBrush(rc,
                Color.FromArgb(255, 28, 31, 42), Color.FromArgb(255, 24, 27, 36), 90F))
            {
                e.Graphics.FillRectangle(b, rc);
            }
            using (Pen pen = new Pen(Color.FromArgb(255, 47, 53, 67)))
            {
                e.Graphics.DrawLine(pen, 0, _header.Height - 1, _header.Width, _header.Height - 1);
            }
        }

        void StatusPaint(object s, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent != null ? Parent.BackColor : BackColor);
            GraphicsPath p = MakeRounded(new Rectangle(0, 0, _status.Width - 1, _status.Height - 1), 16);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 28, 32, 43)))
            {
                e.Graphics.FillPath(b, p);
            }
            using (Pen pen = new Pen(Color.FromArgb(255, 47, 53, 67)))
            {
                e.Graphics.DrawPath(pen, p);
            }
            // 顶部内高光
            using (Pen hl = new Pen(Color.FromArgb(60, 255, 255, 255)))
            {
                e.Graphics.DrawLine(hl, 14, 1, _status.Width - 14, 1);
            }
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
            try { return MakeAppIcon(); }
            catch { }
            try
            {
                if (File.Exists(_iconPath)) return new Icon(_iconPath);
            }
            catch { }
            return SystemIcons.Application;
        }

        // 自绘托盘图标：与主界面标题图标一致（蓝色渐变圆底 + 白色手机轮廓）
        Icon MakeAppIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    Rectangle rc = new Rectangle(1, 1, 30, 30);
                    using (GraphicsPath p = UiDraw.Rounded(rc, 15))
                    {
                        using (LinearGradientBrush b = new LinearGradientBrush(rc,
                            Color.FromArgb(255, 76, 128, 255), Color.FromArgb(255, 45, 84, 216), 90F))
                        {
                            g.FillPath(b, p);
                        }
                        using (Pen pen = new Pen(Color.FromArgb(255, 110, 160, 255)))
                        {
                            g.DrawPath(pen, p);
                        }
                    }
                    using (Pen wp = new Pen(Color.White, 1.8F))
                    {
                        wp.StartCap = LineCap.Round;
                        wp.EndCap = LineCap.Round;
                        using (GraphicsPath bp = UiDraw.Rounded(new Rectangle(11, 7, 10, 18), 2))
                        {
                            g.DrawPath(wp, bp);
                        }
                        g.DrawEllipse(wp, 15, 21, 3, 3);
                    }
                }
                IntPtr h = bmp.GetHicon();
                try
                {
                    using (Icon temp = Icon.FromHandle(h))
                    {
                        return (Icon)temp.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(h);
                }
            }
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
            mStop.Click += delegate { StopAll(); };
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
        static bool IsWifiSerial(string serial)
        {
            string sl = (serial ?? "").ToLowerInvariant();
            return serial.IndexOf(':') >= 0
                || sl.StartsWith("adb-")
                || sl.Contains("_adb-tls")
                || sl.Contains("_tcp");
        }

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
                    // 无线设备特征：ip:port 形式，或 adb-mdns 名称（adb-xxx..._adb-tls-connect_tcp）
                    if (IsWifiSerial(serial)) wifi.Add(d); else usb.Add(d);
                }
            }
            catch { }
        }

        Dev FindWifiTarget(List<Dev> wifi)
        {
            string addr = _connAddr.Trim();
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
                bool running = false;
                if (serial != null)
                {
                    Process pp;
                    if (_procs.TryGetValue(serial, out pp) && pp != null && !pp.HasExited) running = true;
                }
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
            // 优先显示当前选中/投屏的设备（按 serial 匹配），避免 USB/无线错位
            List<Dev> all = new List<Dev>(usb);
            all.AddRange(wifi);
            if (serial != null)
            {
                foreach (Dev d in all) if (d.Serial == serial) { shown = d; break; }
            }
            if (shown == null)
            {
                if (usb.Count > 0) shown = usb[0];
                else if (wifi.Count > 0) shown = wifi[0];
            }
            if (shown != null) showWifi = IsWifiSerial(shown.Serial);

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
            UpdateRows(usb, wifi);
        }

        // 提取设备物理标识：USB serial(df9e54d0) 与无线 mdns(adb-df9e54d0-xxx) 归为同一台
        static string DeviceBaseKey(string serial)
        {
            string s = serial ?? "";
            int idx = s.IndexOf("_adb-tls");
            if (idx > 0)
            {
                string[] seg = s.Substring(0, idx).Split('-');
                if (seg.Length >= 2) return seg[1];
            }
            if (s.Contains(":")) return s;
            return s;
        }

        void UpdateRows(List<Dev> usb, List<Dev> wifi)
        {
            if (_devList == null) return;
            _devList.SuspendLayout();
            _devList.Controls.Clear();
            _rows.Clear();
            int y = 4;
            // 同一台设备（USB 与无线同源）只显示一次，优先 USB 行
            HashSet<string> taken = new HashSet<string>();
            foreach (Dev d in usb) taken.Add(DeviceBaseKey(d.Serial));
            List<Dev> wifiDedup = new List<Dev>();
            foreach (Dev d in wifi)
            {
                string k = DeviceBaseKey(d.Serial);
                if (!taken.Contains(k)) { wifiDedup.Add(d); taken.Add(k); }
            }
            // 设备行：USB 全部在前，无线紧随其后（支持多台）
            foreach (Dev d in usb) AddDevRow(d, "usb", ref y);
            foreach (Dev d in wifiDedup) AddDevRow(d, "wifi", ref y);
            // 纯文本提示行
            DeviceRow tip = new DeviceRow();
            tip.Title = "更多设备 · 可继续无线配对发现";
            tip.Plain = true;
            tip.Cursor = Cursors.Default;
            tip.QuickBtn.Visible = false;
            tip.Bounds = new Rectangle(0, y, 380, 30);
            _devList.Controls.Add(tip);
            _rows.Add(tip);
            y += 30;
            _devList.ResumeLayout();
            // 无任何选中时，默认选中第一台设备行
            bool anySel = false;
            foreach (DeviceRow rr in _rows) if (rr.Selected) { anySel = true; break; }
            if (!anySel && _rows.Count > 0 && _rows[0].Target.Length > 0) SelectDeviceRow(0);
            _status.Invalidate();
        }

        void AddDevRow(Dev d, string kind, ref int y)
        {
            DeviceRow r = new DeviceRow();
            r.Bounds = new Rectangle(0, y, 380, 44);
            r.Title = d.Model;
            r.Sub = d.Serial;
            r.Dot = C_GREEN;
            r.Kind = kind;
            r.Target = d.Serial;
            r.Badge = kind == "usb" ? "USB" : "无线";
            r.QuickBtn.Visible = true;
            r.Click += delegate { int i = _rows.IndexOf(r); if (i >= 0) SelectDeviceRow(i); };
            r.QuickClick = delegate(DeviceRow rr) { QuickStart(rr); };
            if (d.Serial == _currentSerial) r.Selected = true;
            bool run = _procs.ContainsKey(d.Serial) && !_procs[d.Serial].HasExited;
            r.Running = run;
            r.QuickBtn.Text = run ? "投屏中" : "▶ 投屏";
            r.QuickBtn.Enabled = !run;
            _devList.Controls.Add(r);
            _rows.Add(r);
            y += 52;
        }

        void SelectDeviceRow(int idx)
        {
            if (_rows.Count == 0) return;
            if (idx < 0 || idx >= _rows.Count) idx = 0;
            for (int i = 0; i < _rows.Count; i++) _rows[i].Selected = (i == idx);
            DeviceRow r = _rows[idx];
            if (r.Kind == "usb") _mode = "usb";
            else if (r.Kind == "wifi")
            {
                _mode = "wifi";
                if (r.Target.Length > 0) _connAddr = r.Target;
            }
            else _mode = "auto";
            _status.Invalidate();
        }

        void SelectUsbRow()
        {
            SelectDeviceRow(0);
        }

        // 行内「快速投屏」：直接对指定设备投屏（互不影响，可多台同时投）
        void QuickStart(DeviceRow r)
        {
            if (r == null || r.Target.Length == 0) return;
            int idx = _rows.IndexOf(r);
            if (idx >= 0) SelectDeviceRow(idx);
            _currentSerial = r.Target;
            _currentModel = r.Title;
            StartMirror();
        }

        // ---------- mirror ----------
        void StartMirror()
        {
            Process cur;
            if (_currentSerial != null && _procs.TryGetValue(_currentSerial, out cur) && cur != null && !cur.HasExited)
            {
                MessageBox.Show("该设备已在投屏运行中，无需重复启动。", "手机投屏助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                _procs[_currentSerial] = Process.Start(psi);
                UpdateMirrorButtons();
                _lblStatus.Text = "设备在线 · " + _currentModel;
                _lblSub.Text = (IsWifiSerial(_currentSerial) ? "无线" : "USB") + " · " + _currentSerial + "  ·  投屏运行中";
                _dot.BackColor = C_GREEN;
                RefreshStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动投屏失败：\n" + ex.Message, "手机投屏助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void StopMirror()   // 停止当前选中设备的投屏
        {
            if (_currentSerial != null)
            {
                Process p;
                if (_procs.TryGetValue(_currentSerial, out p))
                {
                    if (p != null && !p.HasExited)
                    {
                        try { p.Kill(); } catch { }
                    }
                    _procs.Remove(_currentSerial);
                }
            }
            UpdateMirrorButtons();
            RefreshStatus();
        }

        void StopAll()      // 停止所有设备的投屏
        {
            List<string> keys = new List<string>(_procs.Keys);
            foreach (string k in keys)
            {
                Process p;
                if (_procs.TryGetValue(k, out p) && p != null)
                {
                    try { if (!p.HasExited) p.Kill(); } catch { }
                }
            }
            _procs.Clear();
            UpdateMirrorButtons();
            RefreshStatus();
        }

        void UpdateMirrorButtons()
        {
            bool running = false;
            if (_currentSerial != null)
            {
                Process p;
                if (_procs.TryGetValue(_currentSerial, out p) && p != null && !p.HasExited) running = true;
            }
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

        // ---------- 连接引导：打开 CMD 窗口提示小白下一步操作 ----------
        void OpenCmdGuide(string kind)
        {
            try
            {
                bool isWifi = kind == "wifi";
                string bat = Path.Combine(Path.GetTempPath(), "pm_guide_" + kind + ".bat");
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("@echo off");
                sb.AppendLine("title " + (isWifi ? "无线配对引导" : "USB 连接引导") + " - 手机投屏助手");
                sb.AppendLine("color 0B");
                sb.AppendLine("echo.");
                sb.AppendLine("echo  ============================================================");
                sb.AppendLine("echo   " + (isWifi ? "无线调试配对" : "USB 连接") + "引导 - 照着做就行");
                sb.AppendLine("echo  ============================================================");
                sb.AppendLine("echo.");
                if (isWifi)
                {
                    sb.AppendLine("echo  第1步：手机 设置 - 我的设备 - 全部参数与信息");
                    sb.AppendLine("echo          连续点【OS版本】7次，开启开发者模式");
                    sb.AppendLine("echo.");
                    sb.AppendLine("echo  第2步：设置 - 更多设置 - 开发者选项");
                    sb.AppendLine("echo          打开【USB调试】和【无线调试】");
                    sb.AppendLine("echo.");
                    sb.AppendLine("echo  第3步：手机和电脑连同一个WiFi / 局域网");
                    sb.AppendLine("echo.");
                    sb.AppendLine("echo  第4步：手机无线调试页面点【使用配对码配对设备】");
                    sb.AppendLine("echo          记下弹窗里的 IP地址:端口 和 6位配对码");
                    sb.AppendLine("echo.");
                    sb.AppendLine("echo  第5步：在本窗口输入下面命令（把值改成你手机的）：");
                    sb.AppendLine("echo.");
                    sb.AppendLine("echo       adb pair 192.168.x.x:端口号");
                    sb.AppendLine("echo       按回车后，输入 6 位配对码，再回车");
                    sb.AppendLine("echo.");
                    sb.AppendLine("echo  第6步：配对成功提示 paired 后，把无线调试主界面");
                    sb.AppendLine("echo          的 IP地址:端口 填进主界面【无线地址】");
                    sb.AppendLine("echo          再点【开始投屏】即可");
                    sb.AppendLine("echo.");
                }
                else
                {
                    sb.AppendLine("echo  第1步：手机 设置 - 我的设备 - 全部参数与信息");
                    sb.AppendLine("echo          连续点【OS版本】7次，开启开发者模式");
                    sb.AppendLine("echo.");
                    sb.AppendLine("echo  第2步：设置 - 更多设置 - 开发者选项");
                    sb.AppendLine("echo          打开【USB调试】");
                    sb.AppendLine("echo.");
                    sb.AppendLine("echo  第3步：用能传数据的数据线，把手机插到电脑USB口");
                    sb.AppendLine("echo          （优先插机箱后面的USB口）");
                    sb.AppendLine("echo.");
                    sb.AppendLine("echo  第4步：手机弹窗【允许USB调试】- 点允许");
                    sb.AppendLine("echo          并勾选【始终允许】");
                    sb.AppendLine("echo.");
                    sb.AppendLine("echo  -----------------------------------------------");
                    sb.AppendLine("echo  下面自动检测手机是否已被电脑识别：");
                    sb.AppendLine("echo.");
                }
                sb.AppendLine("cd /d \"" + _scrcpyDir + "\"");
                if (isWifi)
                {
                    sb.AppendLine("echo  已开无线调试的手机（应看到 ip:端口）：");
                    sb.AppendLine("adb mdns services");
                    sb.AppendLine("echo.");
                    sb.AppendLine("echo  提示：配对完成后，回主界面点【刷新】和【开始投屏】。");
                    sb.AppendLine("echo        如果什么都没显示，说明手机无线调试没开或");
                    sb.AppendLine("echo        和电脑不在同一网络，回到第2步检查。");
                }
                else
                {
                    sb.AppendLine("echo  检测结果（应看到类似  XXXXXXXX device 的一行）：");
                    sb.AppendLine("adb devices");
                    sb.AppendLine("echo.");
                    sb.AppendLine("echo  提示：识别成功后，回主界面点【开始投屏】。");
                    sb.AppendLine("echo        没识别到就换根数据线，或把手机USB模式");
                    sb.AppendLine("echo        改成【传输文件 / MTP】再试。");
                }
                sb.AppendLine("echo.");
                sb.AppendLine("pause");
                File.WriteAllText(bat, sb.ToString(), Encoding.GetEncoding(936));
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "cmd.exe";
                psi.Arguments = "/k \"\"" + bat + "\"\"";
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开引导窗口失败：" + ex.Message, "手机投屏助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------- 无线配对：二级界面填写 -> 后台自动执行 adb pair -> 结果提示 ----------
        string RunAdb(string arguments)
        {
            try
            {
                Process p = new Process();
                p.StartInfo.FileName = _adbExe;
                p.StartInfo.Arguments = arguments;
                p.StartInfo.WorkingDirectory = _scrcpyDir;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                // adb 输出固定为 UTF-8，强制 UTF-8 解码避免乱码
                p.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                p.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                if (!p.Start()) return "启动 adb 失败";
                string so = p.StandardOutput.ReadToEnd();
                string se = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(20000)) { try { p.Kill(); } catch { } }
                return so + se;
            }
            catch (Exception ex) { return "adb 调用失败: " + ex.Message; }
        }

        string ProbeWireless()
        {
            try
            {
                string outText = RunAdb("mdns services");
                Regex r = new Regex(@"(\d{1,3}(?:\.\d{1,3}){3}:\d+)");
                Match m = r.Match(outText);
                if (m.Success) return m.Groups[1].Value;
            }
            catch { }
            return "";
        }

        void ShowPairForm()
        {
            using (PairForm f = new PairForm())
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                string ip = f.IpAddress.Trim();
                string port = f.Port.Trim();
                string code = f.PairCode.Trim();
                if (ip.Length == 0 || port.Length == 0 || code.Length == 0)
                {
                    MessageBox.Show("请完整填写 IP地址、端口号 和 6位配对码。", "手机投屏助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                string pairAddr = ip + ":" + port;
                // 后台执行配对，避免卡界面
                ThreadPool.QueueUserWorkItem(delegate
                {
                    string result = RunAdb("pair " + pairAddr + " " + code);
                    bool ok = result.IndexOf("Successfully paired", StringComparison.OrdinalIgnoreCase) >= 0;
                    try
                    {
                        BeginInvoke((Action)delegate
                        {
                            if (ok)
                            {
                                string probe = ProbeWireless();
                                if (probe.Length > 0)
                                {
                                    _connAddr = probe;
                                    SaveConfig();
                                    RefreshStatus();
                                }
                                string msg = "配对成功！";
                                if (probe.Length > 0)
                                    msg += "\n已自动探测到无线地址并填入【无线地址】：" + probe + "\n\n点【开始投屏】即可。";
                                else
                                    msg += "\n请在手机【无线调试】主界面查看 IP地址:端口，\n填入主界面【无线地址】后点【开始投屏】。";
                                MessageBox.Show(msg, "手机投屏助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show("配对失败：\n" + result.Trim() + "\n\n请确认：\n1. 配对码约 1 分钟有效，过期请重新在手机上点配对；\n2. IP地址、端口号、配对码与手机弹窗完全一致。",
                                    "手机投屏助手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        });
                    }
                    catch { }
                });
            }
        }

        // ---------- USB 连接：后台自动检测 -> 按状态给结果提示 ----------
        void UsbConnect()
        {
            try { _lblStatus.Text = "正在检测 USB 设备…"; _lblSub.Text = "请保持数据线连接，稍候…"; } catch { }
            ThreadPool.QueueUserWorkItem(delegate
            {
                Dev okDev = null;
                string pendingModel = "";
                try
                {
                    Process p = new Process();
                    p.StartInfo.FileName = _adbExe;
                    p.StartInfo.Arguments = "devices -l";
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardOutput = true;
                    if (p.Start())
                    {
                        string all = p.StandardOutput.ReadToEnd();
                        if (p.WaitForExit(5000))
                        {
                            string[] lines = all.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string line in lines)
                            {
                                string t = line.Trim();
                                if (t.Length == 0 || t.StartsWith("List") || t.StartsWith("*")) continue;
                                string[] parts = t.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length < 2) continue;
                                if (parts[1] == "device")
                                {
                                    // 无线设备（adb-mdns 名 / ip:port）不算 USB 连接
                                    if (IsWifiSerial(parts[0])) continue;
                                    string model = "";
                                    foreach (string part in parts)
                                        if (part.StartsWith("model:")) { model = part.Substring(6); break; }
                                    if (model.Length == 0) model = parts[0];
                                    okDev = new Dev();
                                    okDev.Serial = parts[0];
                                    okDev.Model = model;
                                    break;
                                }
                                else if (parts[1] == "unauthorized" && pendingModel.Length == 0)
                                {
                                    if (!IsWifiSerial(parts[0])) pendingModel = parts[0];
                                }
                            }
                        }
                        try { p.Kill(); } catch { }
                    }
                }
                catch { }
                Dev dev = okDev;
                string pend = pendingModel;
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        if (dev != null)
                        {
                            DialogResult dr = MessageBox.Show("连接到『" + dev.Model + "』成功，是否确认投屏？",
                                "手机投屏助手", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (dr == DialogResult.Yes)
                            {
                                _mode = "usb";
                                SelectUsbRow();
                                StartMirror();
                            }
                            else
                            {
                                RefreshStatus();
                            }
                        }
                        else if (pend.Length > 0)
                        {
                            MessageBox.Show("检测到手机，但手机还没有授权 USB 调试。\n\n请在手机上弹出的【允许 USB 调试】窗口点「允许」，\n并勾选「始终允许」，然后再次点击【USB 连接】。",
                                "手机投屏助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            RefreshStatus();
                        }
                        else
                        {
                            MessageBox.Show("未检测到 USB 连接的手机，请依次检查：\n\n1、请使用原装数据线连接手机与电脑；\n2、请使用台式主机主板 USB 端口（机箱后面的 USB 口）；\n3、请在开发者模式下开启「USB 调试」。",
                                "手机投屏助手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            RefreshStatus();
                        }
                    });
                }
                catch { }
            });
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
                            if (!(_currentSerial != null && _procs.ContainsKey(_currentSerial))) StartMirror();
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
                StopAll();
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

    // ---------------- 无线配对二级输入界面 ----------------
    class PairForm : Form
    {
        public string IpAddress { get { return _txtIp.Text; } }
        public string Port { get { return _txtPort.Text; } }
        public string PairCode { get { return _txtCode.Text; } }

        TextBox _txtIp = new TextBox();
        TextBox _txtPort = new TextBox();
        TextBox _txtCode = new TextBox();

        static readonly Color C_BG2 = Color.FromArgb(255, 26, 30, 43);
        static readonly Color C_CARD2 = Color.FromArgb(255, 43, 49, 69);
        static readonly Color C_TEXT2 = Color.FromArgb(255, 229, 231, 235);
        static readonly Color C_SUB2 = Color.FromArgb(255, 156, 163, 175);
        static readonly Color C_ACCENT2 = Color.FromArgb(255, 59, 130, 246);

        public PairForm()
        {
            Text = "无线调试配对";
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = C_BG2;
            ClientSize = new Size(470, 330);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            // 主界面可能勾了「窗口置顶」，二级窗口必须同样置顶，否则会被主界面盖住
            TopMost = true;

            Label title = new Label();
            title.Text = "请输入手机无线调试配对界面信息";
            title.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            title.ForeColor = C_TEXT2;
            title.AutoSize = false;
            title.Bounds = new Rectangle(20, 14, 430, 26);
            title.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(title);

            Label hint = new Label();
            hint.Text = "手机【无线调试】页点「使用配对码配对设备」后，\n把弹窗里的信息填到下面：";
            hint.Font = new Font("Microsoft YaHei UI", 9F);
            hint.ForeColor = C_SUB2;
            hint.AutoSize = false;
            hint.Bounds = new Rectangle(20, 46, 430, 44);
            Controls.Add(hint);

            Label lblIp = new Label();
            lblIp.Text = "IP地址";
            lblIp.Font = new Font("Microsoft YaHei UI", 9F);
            lblIp.ForeColor = C_SUB2;
            lblIp.Bounds = new Rectangle(20, 108, 90, 30);
            lblIp.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(lblIp);
            Panel wIp = MakeInputWrap(new Rectangle(120, 106, 210, 34));
            _txtIp.Bounds = new Rectangle(10, 7, 190, 20);
            _txtIp.BackColor = C_CARD2;
            _txtIp.ForeColor = C_TEXT2;
            _txtIp.BorderStyle = BorderStyle.None;
            wIp.Controls.Add(_txtIp);

            Label lblPort = new Label();
            lblPort.Text = "端口号";
            lblPort.Font = new Font("Microsoft YaHei UI", 9F);
            lblPort.ForeColor = C_SUB2;
            lblPort.Bounds = new Rectangle(340, 108, 60, 30);
            lblPort.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(lblPort);
            Panel wPort = MakeInputWrap(new Rectangle(400, 106, 60, 34));
            _txtPort.Bounds = new Rectangle(8, 7, 44, 20);
            _txtPort.BackColor = C_CARD2;
            _txtPort.ForeColor = C_TEXT2;
            _txtPort.BorderStyle = BorderStyle.None;
            _txtPort.MaxLength = 5;
            wPort.Controls.Add(_txtPort);

            Label lblCode = new Label();
            lblCode.Text = "6位配对码";
            lblCode.Font = new Font("Microsoft YaHei UI", 9F);
            lblCode.ForeColor = C_SUB2;
            lblCode.Bounds = new Rectangle(20, 152, 90, 30);
            lblCode.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(lblCode);
            Panel wCode = MakeInputWrap(new Rectangle(120, 150, 210, 34));
            _txtCode.Bounds = new Rectangle(10, 7, 190, 20);
            _txtCode.BackColor = C_CARD2;
            _txtCode.ForeColor = C_TEXT2;
            _txtCode.BorderStyle = BorderStyle.None;
            _txtCode.MaxLength = 6;
            wCode.Controls.Add(_txtCode);

            Label note = new Label();
            note.Text = "提示：IP地址:端口 和 6位配对码都在\n手机弹出的配对窗口中，约 1 分钟有效。";
            note.Font = new Font("Microsoft YaHei UI", 8F);
            note.ForeColor = C_SUB2;
            note.AutoSize = false;
            note.Bounds = new Rectangle(20, 188, 430, 40);
            Controls.Add(note);

            RoundButton btnOk = new RoundButton();
            btnOk.Text = "确认";
            btnOk.BackColor = C_ACCENT2;
            btnOk.GradientTop = Color.FromArgb(255, 71, 142, 255);
            btnOk.GradientBottom = Color.FromArgb(255, 47, 110, 232);
            btnOk.HoverTop = Color.FromArgb(255, 98, 160, 255);
            btnOk.HoverBottom = Color.FromArgb(255, 66, 126, 246);
            btnOk.ForeColor = Color.White;
            btnOk.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            btnOk.Radius = 18;
            btnOk.Bounds = new Rectangle(120, 244, 200, 40);
            btnOk.DialogResult = DialogResult.OK;
            Controls.Add(btnOk);

            RoundButton btnCancel = new RoundButton();
            btnCancel.Text = "取消";
            btnCancel.BackColor = C_CARD2;
            btnCancel.GradientTop = UiDraw.Lighten(C_CARD2, 8);
            btnCancel.GradientBottom = C_CARD2;
            btnCancel.HoverTop = UiDraw.Lighten(C_CARD2, 26);
            btnCancel.HoverBottom = UiDraw.Lighten(C_CARD2, 12);
            btnCancel.ForeColor = C_TEXT2;
            btnCancel.Font = new Font("Microsoft YaHei UI", 9F);
            btnCancel.Radius = 18;
            btnCancel.Bounds = new Rectangle(330, 244, 120, 40);
            btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(btnCancel);

            // 数字输入约束
            _txtPort.KeyPress += delegate(object s, KeyPressEventArgs e)
            {
                if (!char.IsDigit(e.KeyChar) && e.KeyChar != 8) e.Handled = true;
            };
            _txtCode.KeyPress += delegate(object s, KeyPressEventArgs e)
            {
                if (!char.IsDigit(e.KeyChar) && e.KeyChar != 8) e.Handled = true;
            };

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        Panel MakeInputWrap(Rectangle r)
        {
            Panel w = new Panel();
            w.Bounds = r;
            w.BackColor = C_CARD2;
            w.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath p = UiDraw.Rounded(new Rectangle(0, 0, w.Width - 1, w.Height - 1), 10))
                {
                    using (SolidBrush br = new SolidBrush(C_CARD2)) e.Graphics.FillPath(br, p);
                    using (Pen pen = new Pen(Color.FromArgb(255, 60, 68, 96))) e.Graphics.DrawPath(pen, p);
                }
            };
            Controls.Add(w);
            return w;
        }
    }

    // ---------------- UI 自绘辅助 ----------------
    static class UiDraw
    {
        public static Color Lighten(Color c, int d)
        {
            return Color.FromArgb(255, Math.Min(255, c.R + d), Math.Min(255, c.G + d), Math.Min(255, c.B + d));
        }
        public static Color Darken(Color c, int d)
        {
            return Color.FromArgb(255, Math.Max(0, c.R - d), Math.Max(0, c.G - d), Math.Max(0, c.B - d));
        }
        public static GraphicsPath Rounded(Rectangle r, int rad)
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
    }

    class RoundButton : Button
    {
        public int Radius = 18;
        public Color GradientTop = Color.Empty;
        public Color GradientBottom = Color.Empty;
        public Color HoverTop = Color.Empty;
        public Color HoverBottom = Color.Empty;
        public Color BorderColor = Color.Empty;
        public Color CornerColor = Color.Empty;   // 圆角外底色；空则用父容器背景
        bool _hover;
        bool _down;

        public RoundButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs mevent) { _down = true; Invalidate(); base.OnMouseDown(mevent); }
        protected override void OnMouseUp(MouseEventArgs mevent) { _down = false; Invalidate(); base.OnMouseUp(mevent); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // 四角先铺底色（优先 CornerColor，其次父容器背景），杜绝圆角外露出主色矩形/杂色
            Color corner = CornerColor != Color.Empty ? CornerColor : (Parent != null ? Parent.BackColor : BackColor);
            g.Clear(corner);
            Rectangle rc = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = UiDraw.Rounded(rc, Math.Min(Radius, Height / 2)))
            {
                Color t, b;
                Color border = BorderColor;
                if (!Enabled)
                {
                    // 禁用态：统一低饱和灰蓝，文字用系统灰
                    t = Color.FromArgb(255, 54, 60, 76);
                    b = Color.FromArgb(255, 44, 49, 62);
                    border = Color.FromArgb(255, 62, 68, 84);
                }
                else
                {
                    t = GradientTop == Color.Empty ? BackColor : GradientTop;
                    b = GradientBottom == Color.Empty ? BackColor : GradientBottom;
                    if (_down)
                    {
                        t = UiDraw.Darken(t, 12); b = UiDraw.Darken(b, 12);
                    }
                    else if (_hover)
                    {
                        t = HoverTop == Color.Empty ? UiDraw.Lighten(t, 24) : HoverTop;
                        b = HoverBottom == Color.Empty ? UiDraw.Lighten(b, 12) : HoverBottom;
                    }
                }
                using (LinearGradientBrush br = new LinearGradientBrush(rc, t, b, 90F))
                {
                    g.FillPath(br, path);
                }
                if (border != Color.Empty)
                {
                    using (Pen pen = new Pen(border))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height),
                Enabled ? ForeColor : SystemColors.GrayText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    class RoundCheckBox : CheckBox
    {
        public RoundCheckBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent == null ? BackColor : Parent.BackColor);
            int box = 18;
            Rectangle rc = new Rectangle(0, (Height - box) / 2, box, box);
            Color back = Checked ? Color.FromArgb(255, 59, 130, 246) : Color.FromArgb(255, 40, 46, 66);
            using (GraphicsPath p = UiDraw.Rounded(rc, 5))
            {
                using (SolidBrush br = new SolidBrush(back)) g.FillPath(br, p);
                using (Pen pen = new Pen(Color.FromArgb(255, 70, 82, 112))) g.DrawPath(pen, p);
            }
            if (Checked)
            {
                using (Pen pen = new Pen(Color.White, 2F))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, rc.Left + 4, rc.Top + 10, rc.Left + 8, rc.Top + 14);
                    g.DrawLine(pen, rc.Left + 8, rc.Top + 14, rc.Left + 15, rc.Top + 5);
                }
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(24, 0, Width - 24, Height), ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    class DeviceRow : Panel
    {
        public string Title = "";
        public string Sub = "";
        public string Badge = "";
        public Color Dot = Color.FromArgb(255, 80, 90, 120);
        public bool Selected = false;
        public bool Running = false;
        public bool Plain = false;   // 纯文本提示行（无卡片轮廓）
        public string Kind = "";
        public string Target = "";
        public RoundButton QuickBtn;
        public Action<DeviceRow> QuickClick;
        bool _hover;

        public DeviceRow()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Cursor = Cursors.Hand;
            BackColor = Color.FromArgb(255, 28, 32, 43);   // 卡片内部背景，四角对齐用
            QuickBtn = new RoundButton();
            QuickBtn.Text = "▶ 投屏";
            QuickBtn.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold);
            QuickBtn.ForeColor = Color.White;
            QuickBtn.BackColor = Color.FromArgb(255, 63, 128, 245);
            QuickBtn.Radius = 10;
            QuickBtn.Cursor = Cursors.Hand;
            QuickBtn.Bounds = new Rectangle(0, 9, 56, 26);
            QuickBtn.Visible = false;
            QuickBtn.Click += delegate { if (QuickClick != null) QuickClick(this); };
            Controls.Add(QuickBtn);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (QuickBtn != null) QuickBtn.Location = new Point(Width - 66, 9);   // 始终贴行右缘
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        // 行内背景色（与 OnPaint 中一致）
        Color RowBackColor()
        {
            return Selected ? Color.FromArgb(255, 44, 58, 92) : (_hover ? Color.FromArgb(255, 37, 43, 58) : Color.FromArgb(255, 31, 36, 48));
        }

        void SyncQuickBtnCorner()
        {
            if (QuickBtn != null)
            {
                QuickBtn.CornerColor = RowBackColor();
                QuickBtn.Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);   // 四角铺卡片内背景，圆角外不露杂色
            SyncQuickBtnCorner();
            if (Plain)
            {
                Font pf = new Font("Microsoft YaHei UI", 8.5F);
                TextRenderer.DrawText(g, Title, pf, new Rectangle(8, 0, Width - 16, Height),
                    Color.FromArgb(255, 120, 130, 155),
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                return;
            }
            Rectangle rc = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = UiDraw.Rounded(rc, 12))
            {
                Color bg = Selected ? Color.FromArgb(255, 44, 58, 92) : (_hover ? Color.FromArgb(255, 37, 43, 58) : Color.FromArgb(255, 31, 36, 48));
                using (SolidBrush br = new SolidBrush(bg)) g.FillPath(br, p);
                Color border = Selected ? Color.FromArgb(255, 96, 165, 247) : Color.FromArgb(255, 45, 52, 68);
                using (Pen pen = new Pen(border)) g.DrawPath(pen, p);
            }
            // 选中左侧指示条
            if (Selected)
            {
                using (GraphicsPath lp = UiDraw.Rounded(new Rectangle(4, Height / 2 - 9, 3, 18), 2))
                using (SolidBrush lb = new SolidBrush(Color.FromArgb(255, 96, 165, 247)))
                {
                    g.FillPath(lb, lp);
                }
            }
            using (SolidBrush b = new SolidBrush(Dot)) g.FillEllipse(b, 13, Height / 2 - 4, 8, 8);
            // 连接方式徽章
            if (Badge.Length > 0)
            {
                Color bc = Kind == "usb" ? Color.FromArgb(255, 59, 130, 246) : (Kind == "wifi" ? Color.FromArgb(255, 34, 197, 94) : Color.FromArgb(255, 70, 80, 105));
                Rectangle brc = new Rectangle(29, Height / 2 - 10, 46, 20);
                using (GraphicsPath bp = UiDraw.Rounded(brc, 10))
                {
                    using (SolidBrush br = new SolidBrush(bc)) g.FillPath(br, bp);
                }
                Font bf = new Font("Microsoft YaHei UI", 7.5F, FontStyle.Bold);
                TextRenderer.DrawText(g, Badge, bf, brc, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
            Font tf = new Font("Microsoft YaHei UI", 9.5F, Selected ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(g, Title, tf, new Rectangle(84, 4, Width - 176, 20),
                Color.FromArgb(255, 229, 231, 235), TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            Font sf = new Font("Microsoft YaHei UI", 8F);
            TextRenderer.DrawText(g, Sub, sf, new Rectangle(84, 25, Width - 176, 16),
                Color.FromArgb(255, 130, 140, 165), TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            // 投屏中状态小绿点
            if (Running)
            {
                using (SolidBrush rb = new SolidBrush(Color.FromArgb(255, 52, 199, 89)))
                {
                    g.FillEllipse(rb, Width - 84, 10, 7, 7);
                }
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(255, 134, 239, 172)))
                {
                    Font rf = new Font("Microsoft YaHei UI", 7.5F);
                    TextRenderer.DrawText(g, "已投屏", rf, new Rectangle(Width - 152, 7, 60, 14),
                        Color.FromArgb(255, 134, 239, 172), TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                }
            }
        }
    }
}
