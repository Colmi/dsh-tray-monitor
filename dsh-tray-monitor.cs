using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Web.Script.Serialization;

namespace DshTray
{
    internal static class Program
    {
        private static int Port = 3080;
        private static string Url = "http://127.0.0.1:3080";
        private static string LogFile = "";
        private static string DataDir = "";
        private static string StartScript = "启动DSH.ps1";
        private static string StopScript = "停止DSH.ps1";
        private static string DshRepo = "";
        private static string DshHome = "";
        private static string NodePath = "";
        private static string DeployDir = "";
        private static bool ConfigLoaded = false;

        private const string RunKeyName = "DSHTrayMonitor";
        private const string RunKeyNameDsh = "DSHWebService";
        private const string DshRepoUrl = "https://github.com/deepseek-ai/deepseek-harness";
        private const int ProbeConnectMs = 400;   // 单次 TCP 探测超时(ms)
        private const int UpConfirmTicks = 3;     // 连续 N 次探测一致才判定“已开启”（防瞬时误报）
        private const int DownConfirmTicks = 2;   // 连续 N 次探测一致才判定“已停止”
        private static readonly string TrayDir = AppDomain.CurrentDomain.BaseDirectory;

        private static NotifyIcon _ni;
        private static ContextMenuStrip _menu;
        private static System.Windows.Forms.Timer _timer;
        private static ToolStripMenuItem _miStatus, _miStart, _miStop, _miRestart, _miAuto, _miAutoDsh, _miConfig, _miExit;
        private static Icon _iconRunning, _iconStopped;
        private static bool _lastUp;
        private static string _pidStr = "";
        private static int _upStreak = 0;
        private static int _downStreak = 0;

        [STAThread]
        private static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                WriteLog("ui exception: " + (e.Exception == null ? "" : e.Exception.ToString()));
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                WriteLog("unhandled exception: " + (e.ExceptionObject as Exception));
            };

            bool createdNew;
            using (var mutex = new Mutex(true, @"Local\DSH-Tray-Monitor-4DSH", out createdNew))
            {
                if (!createdNew) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                LoadConfig();
                StartScript = ResolvePath(StartScript);
                StopScript = ResolvePath(StopScript);
                if (string.IsNullOrEmpty(LogFile)) LogFile = Path.Combine(TrayDir, "tray.log");
                if (string.IsNullOrEmpty(DataDir)) DataDir = TrayDir;

                _iconRunning = LoadIcon("dsh-logo-running.ico");
                _iconStopped = LoadIcon("dsh-logo-stopped.ico");

                bool initialUp = IsUp();
                _lastUp = initialUp;
                _ni = new NotifyIcon
                {
                    Icon = initialUp ? _iconRunning : _iconStopped,
                    Visible = true,
                    Text = Truncate("DSH " + (initialUp ? "运行中" : "已停止"), 63)
                };
                BuildMenu();
                _ni.ContextMenuStrip = _menu;
                _ni.DoubleClick += (s, e) => { try { Process.Start(Url); } catch { } };

                _timer = new System.Windows.Forms.Timer { Interval = 3000 };
                _timer.Tick += (s, e) => UpdateStatus();

                WriteLog("tray monitor started (exe, config-loaded)" + (initialUp ? ", dsh up" : ", dsh down"));
                UpdateStatus();
                _timer.Start();
                Application.Run();
                _ni.Visible = false;
            }
        }

        private static string ResolvePath(string p)
        {
            if (string.IsNullOrEmpty(p)) return p;
            return Path.IsPathRooted(p) ? p : Path.Combine(TrayDir, p);
        }

        private static void LoadConfig()
        {
            try
            {
                string cfgPath = Path.Combine(TrayDir, "config.json");
                if (!File.Exists(cfgPath)) { ConfigLoaded = false; return; }
                string json = File.ReadAllText(cfgPath);
                var ser = new JavaScriptSerializer();
                var dict = ser.Deserialize<Dictionary<string, object>>(json);
                if (dict == null) return;
                if (dict.ContainsKey("url")) Url = dict["url"].ToString();
                if (dict.ContainsKey("port")) Port = Convert.ToInt32(dict["port"]);
                if (dict.ContainsKey("logFile")) LogFile = dict["logFile"].ToString();
                if (dict.ContainsKey("dataDir")) DataDir = dict["dataDir"].ToString();
                if (dict.ContainsKey("startScript")) StartScript = dict["startScript"].ToString();
                if (dict.ContainsKey("stopScript")) StopScript = dict["stopScript"].ToString();
                if (dict.ContainsKey("dshRepo")) DshRepo = dict["dshRepo"].ToString();
                if (dict.ContainsKey("dshHome")) DshHome = dict["dshHome"].ToString();
                if (dict.ContainsKey("nodePath")) NodePath = dict["nodePath"].ToString();
                if (dict.ContainsKey("deployDir")) DeployDir = dict["deployDir"].ToString();
                if (!dict.ContainsKey("url")) Url = "http://127.0.0.1:" + Port;
                ConfigLoaded = true;
            }
            catch (Exception ex) { WriteLog("config load error: " + ex.Message); }
        }

        private static Icon LoadIcon(string name)
        {
            try { string p = Path.Combine(TrayDir, "ico", name); if (File.Exists(p)) return new Icon(p); } catch { }
            try { string p = Path.Combine(TrayDir, name); if (File.Exists(p)) return new Icon(p); } catch { }
            try
            {
                using (var s = typeof(Program).Assembly.GetManifestResourceStream("DshTray.Resources." + name))
                {
                    if (s != null) return new Icon(s);
                }
            }
            catch { }
            return SystemIcons.Application;
        }

        private static void WriteLog(string msg)
        {
            try
            {
                string dir = Path.GetDirectoryName(LogFile);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + "\r\n", new System.Text.UTF8Encoding(false));
            }
            catch { }
        }

        // 仅 TCP 连通性（快速路径：用于连续计数）
        private static bool TcpProbe()
        {
            try
            {
                using (var c = new TcpClient())
                {
                    var ar = c.BeginConnect("127.0.0.1", Port, null, null);
                    return ar.AsyncWaitHandle.WaitOne(ProbeConnectMs, false) && c.Connected;
                }
            }
            catch { return false; }
        }

        // 取监听该端口的进程 PID（0 = 未解析到）
        private static int GetListeningPid()
        {
            try
            {
                var psi = new ProcessStartInfo("netstat", "-ano -p tcp")
                { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, RedirectStandardOutput = true, UseShellExecute = false };
                using (var p = Process.Start(psi))
                {
                    string outStr = p.StandardOutput.ReadToEnd();
                    string portMark = ":" + Port;
                    foreach (string raw in outStr.Split('\n'))
                    {
                        string line = (raw ?? "").Trim();
                        if (line.IndexOf(portMark, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (line.IndexOf("LISTENING", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 5)
                        {
                            int n;
                            if (int.TryParse(parts[parts.Length - 1], out n)) return n;
                        }
                    }
                }
            }
            catch { }
            return 0;
        }

        // 端口占用者是否像 DSH（node / dsh 进程）；无法判定时不作否证（返回 true）
        private static bool PortOwnerLooksLikeDsh(out string info)
        {
            info = "";
            try
            {
                int pid = GetListeningPid();
                if (pid <= 0) { info = "PID 未解析"; return true; }
                string name = "";
                try { using (var p = Process.GetProcessById(pid)) { name = p.ProcessName ?? ""; } } catch { }
                info = "PID " + pid + (name.Length > 0 ? " " + name : "");
                if (name.Length == 0) return true;
                string lower = name.ToLowerInvariant();
                return lower.Contains("node") || lower.Contains("dsh");
            }
            catch { return true; }
        }

        // 判定 DSH 是否在运行：TCP 可连 且 端口占用者像 DSH（node/dsh）
        private static bool IsUp()
        {
            if (!TcpProbe()) return false;
            string info;
            return PortOwnerLooksLikeDsh(out info);
        }

        private static string GetPidString()
        {
            int pid = GetListeningPid();
            return pid > 0 ? " (PID " + pid + ")" : "";
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max);
        }

        private static void BuildMenu()
        {
            _menu = new ContextMenuStrip();
            _miStatus = new ToolStripMenuItem("状态：检测中...") { Enabled = false };
            _miStart = new ToolStripMenuItem("启动 DSH");
            _miStop = new ToolStripMenuItem("停止 DSH");
            _miRestart = new ToolStripMenuItem("重启 DSH");
            var miOpen = new ToolStripMenuItem("打开 Web UI");
            var miData = new ToolStripMenuItem("打开数据目录");
            var miLog = new ToolStripMenuItem("打开日志");
            _miConfig = new ToolStripMenuItem("配置…");
            _miAuto = new ToolStripMenuItem("监控开机自启（关）");
            _miAutoDsh = new ToolStripMenuItem("DSH 开机自启（关）");
            _miExit = new ToolStripMenuItem("退出监控");

            _menu.Items.Add(_miStatus);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_miStart);
            _menu.Items.Add(_miStop);
            _menu.Items.Add(_miRestart);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(miOpen);
            _menu.Items.Add(miData);
            _menu.Items.Add(miLog);
            _menu.Items.Add(_miConfig);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_miAuto);
            _menu.Items.Add(_miAutoDsh);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_miExit);

            _miStart.Click += (s, e) => InvokeAction("start");
            _miStop.Click += (s, e) => InvokeAction("stop");
            _miRestart.Click += (s, e) => InvokeAction("restart");
            miOpen.Click += (s, e) => { try { Process.Start(Url); } catch { } };
            miData.Click += (s, e) => { try { Process.Start("explorer.exe", DataDir); } catch { } };
            miLog.Click += (s, e) => { try { Process.Start("notepad.exe", LogFile); } catch { } };
            _miConfig.Click += (s, e) => OpenConfig();
            _miAuto.Click += (s, e) => ToggleAutoStart();
            _miAutoDsh.Click += (s, e) => ToggleAutoStartDsh();
            _miExit.Click += (s, e) =>
            {
                _ni.Visible = false;
                _timer.Stop();
                Application.Exit();
            };
        }
        private static void InvokeAction(string action)
        {
            WriteLog("action: " + action);
            try
            {
                switch (action)
                {
                    case "start":
                        if (IsUp()) { WriteLog("start: already running"); break; }
                        RunHidden(StartScript);
                        WriteLog("start: requested");
                        break;
                    case "stop":
                        RunHidden(StopScript);
                        WriteLog("stop: requested");
                        break;
                    case "restart":
                        RunHidden(StopScript);
                        Thread.Sleep(3000);
                        RunHidden(StartScript);
                        WriteLog("restart: requested");
                        break;
                }
            }
            catch (Exception ex) { WriteLog("action error: " + ex.Message); }
            Thread.Sleep(800);
            UpdateStatus();
        }

        private static void RunHidden(string script)
        {
            var psi = new ProcessStartInfo("powershell", "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\"")
            { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true };
            Process.Start(psi);
        }

        private static bool IsAutoStartOn()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return k != null && k.GetValue(RunKeyName) != null;
                }
            }
            catch { return false; }
        }

        private static void ToggleAutoStart()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (k == null) return;
                    if (IsAutoStartOn())
                    {
                        k.DeleteValue(RunKeyName, false);
                        WriteLog("autostart disabled");
                    }
                    else
                    {
                        k.SetValue(RunKeyName, "\"" + Path.Combine(TrayDir, "dsh-tray-monitor.exe") + "\"");
                        WriteLog("autostart enabled");
                    }
                }
            }
            catch (Exception ex) { WriteLog("autostart error: " + ex.Message); }
            UpdateStatus();
        }

        private static bool IsAutoStartDshOn()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return k != null && k.GetValue(RunKeyNameDsh) != null;
                }
            }
            catch { return false; }
        }

        private static void ToggleAutoStartDsh()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (k == null) return;
                    if (IsAutoStartDshOn())
                    {
                        k.DeleteValue(RunKeyNameDsh, false);
                        WriteLog("dsh autostart disabled");
                    }
                    else
                    {
                        string ps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                            @"System32\WindowsPowerShell\v1.0\powershell.exe");
                        string value = "\"" + ps + "\" -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + StartScript + "\"";
                        k.SetValue(RunKeyNameDsh, value);
                        WriteLog("dsh autostart enabled: " + value);
                    }
                }
            }
            catch (Exception ex) { WriteLog("dsh autostart error: " + ex.Message); }
            UpdateStatus();
        }

        private static void UpdateStatus()
        {
            // 连续计数：单次瞬时占用不再触发状态切换（避免“已开启→已停止”抖动）
            bool tcpUp = TcpProbe();
            if (tcpUp) { _upStreak++; _downStreak = 0; } else { _downStreak++; _upStreak = 0; }

            if (!_lastUp && _upStreak >= UpConfirmTicks)
            {
                string owner;
                if (PortOwnerLooksLikeDsh(out owner))
                {
                    _lastUp = true;
                    _ni.Icon = _iconRunning;
                    _pidStr = GetPidString();
                    _ni.ShowBalloonTip(2000, "DSH 已开启", "DSH Web 服务已就绪：" + Url, ToolTipIcon.Info);
                    WriteLog("status: running [" + owner + "]");
                }
                else if (_upStreak == UpConfirmTicks)
                {
                    WriteLog("probe: 端口被非 DSH 进程占用，已忽略 [" + owner + "]");
                }
            }
            else if (_lastUp && _downStreak >= DownConfirmTicks)
            {
                _lastUp = false;
                _ni.Icon = _iconStopped;
                _pidStr = "";
                _ni.ShowBalloonTip(2000, "DSH 已停止", "DSH Web 服务当前未运行，可右键菜单启动", ToolTipIcon.Warning);
                WriteLog("status: stopped");
            }

            string state = _lastUp ? "运行中" : "已停止";
            _ni.Text = Truncate("DSH " + state + _pidStr, 63);
            _miStatus.Text = "状态：" + state + _pidStr;
            _miStart.Enabled = !_lastUp;
            _miStop.Enabled = _lastUp;
            _miRestart.Enabled = _lastUp;
            _miAuto.Text = "监控开机自启（" + (IsAutoStartOn() ? "开" : "关") + "）";
            _miAutoDsh.Text = "DSH 开机自启（" + (IsAutoStartDshOn() ? "开" : "关") + "）";
        }

        private static void OpenConfig()
        {
            try { using (var f = new ConfigForm()) { f.ShowDialog(); } }
            catch (Exception ex) { WriteLog("config dialog error: " + ex.Message); }
        }

        private static void ApplyDeployedConfig(int port, string node, string repo, string home, string logDir, string deployDir)
        {
            Port = port;
            Url = "http://127.0.0.1:" + port;
            NodePath = node;
            DshRepo = repo;
            DshHome = home;
            DataDir = home;
            LogFile = Path.Combine(logDir, "tray.log");
            StartScript = Path.Combine(deployDir, "启动DSH.ps1");
            StopScript = Path.Combine(deployDir, "停止DSH.ps1");
            DeployDir = deployDir;
            bool up = IsUp();
            _lastUp = up;
            _upStreak = 0;
            _downStreak = 0;
            _ni.Icon = up ? _iconRunning : _iconStopped;
            UpdateStatus();
            WriteLog("config applied: port=" + port + ", deploy=" + deployDir);
        }

        private static void SetAutoStart(string name, bool enabled, string value)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (k == null) return;
                    if (enabled) k.SetValue(name, value);
                    else k.DeleteValue(name, false);
                }
            }
            catch (Exception ex) { WriteLog("autostart set error: " + ex.Message); }
        }

        private static void CopyIcons(string deployDir)
        {
            string dst = Path.Combine(deployDir, "ico");
            Directory.CreateDirectory(dst);
            string[] names = { "dsh-logo-running.ico", "dsh-logo-stopped.ico", "dsh-logo.ico" };
            foreach (string name in names)
            {
                string target = Path.Combine(dst, name);
                if (File.Exists(target)) continue;
                bool ok = false;
                try { string p = Path.Combine(TrayDir, "ico", name); if (File.Exists(p)) { File.Copy(p, target, false); ok = true; } } catch { }
                if (!ok) { try { string p = Path.Combine(TrayDir, name); if (File.Exists(p)) { File.Copy(p, target, false); ok = true; } } catch { } }
                if (!ok)
                {
                    try
                    {
                        using (var s = typeof(Program).Assembly.GetManifestResourceStream("DshTray.Resources." + name))
                        {
                            if (s != null)
                            {
                                using (var fs = new FileStream(target, FileMode.Create, FileAccess.Write))
                                {
                                    s.CopyTo(fs);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        private static void SwitchTo(string deployDir)
        {
            string newExe = Path.Combine(deployDir, "dsh-tray-monitor.exe");
            string tmp = Path.Combine(Path.GetTempPath(), "dsh-tray-switch.cmd");
            File.WriteAllText(tmp,
                "@echo off\r\nping 127.0.0.1 -n 3 >nul\r\nstart \"\" /D \"" + deployDir + "\" \"" + newExe + "\"\r\n",
                new System.Text.UTF8Encoding(false));
            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + tmp + "\"")
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    WorkingDirectory = deployDir
                });
            }
            catch { }
            _ni.Visible = false;
            _timer.Stop();
            Environment.Exit(0);
        }
        private static string BuildConfigJson(int port, string node, string repo, string home, string logDir, string deployDir)
        {
            var dict = new Dictionary<string, object>
            {
                { "url", "http://127.0.0.1:" + port },
                { "port", port },
                { "logFile", Path.Combine(logDir, "tray.log") },
                { "dataDir", home },
                { "startScript", "启动DSH.ps1" },
                { "stopScript", "停止DSH.ps1" },
                { "dshRepo", repo },
                { "dshHome", home },
                { "nodePath", node },
                { "deployDir", deployDir }
            };
            return new JavaScriptSerializer().Serialize(dict);
        }

        private static string BuildStartScript(string node, string repo, string home, string logDir, int port)
        {
            return (@"# ============================================================
#  DeepSeek Harness (DSH) 启动脚本（由 dsh-tray-monitor 配置自动生成）
# ============================================================
$ErrorActionPreference = 'Stop'
$repo    = '__REPO__'
$env:DSH_HOME = '__HOME__'
$logDir  = '__LOGDIR__'
$node    = '__NODE__'
$port    = __PORT__

if (-not (Test-Path $repo)) { Write-Host ""[错误] 未找到部署目录 $repo"" -ForegroundColor Red; exit 1 }
if (-not (Test-Path $node))  { Write-Host ""[错误] 未找到 Node.js: $node"" -ForegroundColor Red; exit 1 }

if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
    Write-Host ""DSH 已在运行：http://127.0.0.1:$port"" -ForegroundColor Green
    exit 0
}

New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$p = Start-Process -FilePath $node -ArgumentList '--import','tsx/esm','apps/cli/src/bin.ts','web','--no-open','--host','127.0.0.1','--port',$port `
    -WorkingDirectory $repo `
    -RedirectStandardOutput ""$logDir\web.out.log"" `
    -RedirectStandardError  ""$logDir\web.err.log"" `
    -WindowStyle Hidden -PassThru

Start-Sleep -Seconds 10
if (-not $p.HasExited) {
    Write-Host ""DSH 已启动 (PID $($p.Id))：http://127.0.0.1:$port"" -ForegroundColor Green
} else {
    Write-Host '[错误] DSH 启动失败，日志如下：' -ForegroundColor Red
    Get-Content ""$logDir\web.err.log"" -ErrorAction SilentlyContinue | Select-Object -Last 30
    exit 1
}
")
                .Replace("__REPO__", repo)
                .Replace("__HOME__", home)
                .Replace("__LOGDIR__", logDir)
                .Replace("__NODE__", node)
                .Replace("__PORT__", port.ToString());
        }

        private static string BuildStopScript(int port)
        {
            return (@"# ============================================================
#  DeepSeek Harness (DSH) 停止脚本（由 dsh-tray-monitor 配置自动生成）
# ============================================================
$port = __PORT__
$conn = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
if (-not $conn) { Write-Host 'DSH 未在运行'; exit 0 }
$ids = $conn | Select-Object -ExpandProperty OwningProcess -Unique
foreach ($id in $ids) { Stop-Process -Id $id -Force; Write-Host ""已停止 DSH 进程 PID $id"" }
")
                .Replace("__PORT__", port.ToString());
        }

        private static string BuildLauncherCmd()
        {
            return "@echo off\r\nrem DSH Tray Monitor launcher (generated by config)\r\nstart \"\" \"%~dp0dsh-tray-monitor.exe\"\r\n";
        }

        // ============ 配置窗体 ============

        private sealed class ConfigForm : Form
        {
            private const int EM_SETCUEBANNER = 0x1501;

            [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

            private readonly TextBox txtPort = new TextBox();
            private readonly TextBox txtNode = new TextBox();
            private readonly TextBox txtRepo = new TextBox();
            private readonly TextBox txtHome = new TextBox();
            private readonly TextBox txtLog = new TextBox();
            private readonly TextBox txtDeploy = new TextBox();
            private readonly CheckBox chkMon = new CheckBox();
            private readonly CheckBox chkDsh = new CheckBox();
            private readonly Label lblStatus = new Label();

            public ConfigForm()
            {
                Text = "DSH 托盘监控 配置";
                StartPosition = FormStartPosition.CenterScreen;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ClientSize = new Size(660, 332);
                BuildUi();
                LoadCurrent();
            }

            protected override void OnLoad(EventArgs e)
            {
                base.OnLoad(e);
                SetCue(txtNode, "例如 C:\\Program Files\\nodejs\\node.exe");
                SetCue(txtRepo, "例如 D:\\Deepseek-harness（DSH 源码目录）");
                SetCue(txtHome, "例如 D:\\Deepseek-harness-data（DSH_HOME）");
                SetCue(txtLog, "例如 D:\\Deepseek-harness-data\\logs（留空则 <DSH_HOME>\\logs）");
                SetCue(txtDeploy, "留空则默认 <DSH_HOME>\\dsh-tray-monitor");
            }

            private static void SetCue(TextBox tb, string text)
            {
                try { SendMessage(tb.Handle, EM_SETCUEBANNER, (IntPtr)1, text); } catch { }
            }

            private void AddLabel(string text, int y)
            {
                Controls.Add(new Label { Text = text, Left = 12, Top = y + 4, Width = 150, TextAlign = ContentAlignment.MiddleRight });
            }

            private Button Btn(string text, int left, int top, int width)
            {
                var b = new Button { Text = text, Left = left, Top = top, Width = width };
                Controls.Add(b);
                return b;
            }

            private void BuildUi()
            {
                int x = 170, w = 400, bx = 576;
                AddLabel("Web 服务端口", 14);
                txtPort.SetBounds(x, 14, 80, 25); Controls.Add(txtPort);

                AddLabel("Node 可执行文件", 44);
                txtNode.SetBounds(x, 44, w, 25); Controls.Add(txtNode);
                Btn("浏览…", bx, 44, 64).Click += (s, e) => PickFile(txtNode, "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*");

                AddLabel("DSH 部署目录", 74);
                txtRepo.SetBounds(x, 74, w, 25); Controls.Add(txtRepo);
                Btn("浏览…", bx, 74, 64).Click += (s, e) => PickFolder(txtRepo);

                AddLabel("DSH 数据目录 (DSH_HOME)", 104);
                txtHome.SetBounds(x, 104, w, 25); Controls.Add(txtHome);
                Btn("浏览…", bx, 104, 64).Click += (s, e) => PickFolder(txtHome);

                AddLabel("日志目录", 134);
                txtLog.SetBounds(x, 134, w, 25); Controls.Add(txtLog);
                Btn("浏览…", bx, 134, 64).Click += (s, e) => PickFolder(txtLog);

                AddLabel("部署目录", 164);
                txtDeploy.SetBounds(x, 164, w, 25); Controls.Add(txtDeploy);
                Btn("浏览…", bx, 164, 64).Click += (s, e) => PickFolder(txtDeploy);

                chkMon.SetBounds(x, 198, 230, 24); chkMon.Text = "监控开机自启"; Controls.Add(chkMon);
                chkDsh.SetBounds(x + 240, 198, 230, 24); chkDsh.Text = "DSH 开机自启"; Controls.Add(chkDsh);

                Btn("自动检测", x, 228, 88).Click += (s, e) => DetectAll();
                Btn("验证", x + 96, 228, 76).Click += (s, e) => RunValidate();
                Btn("部署", x + 176, 228, 76).Click += (s, e) => Deploy();
                Btn("取消", x + 256, 228, 76).Click += (s, e) => Close();

                lblStatus.SetBounds(12, 264, 636, 56);
                lblStatus.Text = "";
                Controls.Add(lblStatus);
            }

            private void LoadCurrent()
            {
                txtPort.Text = Program.Port.ToString();
                chkMon.Checked = Program.IsAutoStartOn();
                chkDsh.Checked = Program.IsAutoStartDshOn();

                if (Program.ConfigLoaded)
                {
                    txtNode.Text = Program.NodePath;
                    txtRepo.Text = Program.DshRepo;
                    txtHome.Text = Program.DshHome;
                    txtLog.Text = Path.GetDirectoryName(Program.LogFile) ?? "";
                    txtDeploy.Text = Program.DeployDir;
                }
                else
                {
                    txtNode.Text = "";
                    txtRepo.Text = "";
                    txtHome.Text = "";
                    txtLog.Text = "";
                    txtDeploy.Text = "";
                }
            }

            private void PickFile(TextBox tb, string filter)
            {
                using (var d = new OpenFileDialog { Filter = filter })
                {
                    if (d.ShowDialog() == DialogResult.OK) tb.Text = d.FileName;
                }
            }

            private void PickFolder(TextBox tb)
            {
                using (var d = new FolderBrowserDialog())
                {
                    if (d.ShowDialog() == DialogResult.OK) tb.Text = d.SelectedPath;
                }
            }

            private string FindNode()
            {
                foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
                {
                    try { string p = Path.Combine(dir.Trim(), "node.exe"); if (File.Exists(p)) return p; } catch { }
                }
                string[] candidates =
                {
                    @"C:\Program Files\nodejs\node.exe",
                    @"C:\Program Files (x86)\nodejs\node.exe",
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Programs\nodejs\node.exe")
                };
                foreach (string p in candidates) { try { if (File.Exists(p)) return p; } catch { } }
                return null;
            }

            private List<string> FixedRoots()
            {
                var list = new List<string>();
                try
                {
                    foreach (var d in DriveInfo.GetDrives())
                    {
                        try { if (d.DriveType == DriveType.Fixed && d.IsReady) list.Add(d.RootDirectory.FullName); } catch { }
                    }
                }
                catch { }
                return list;
            }

            private string FindDshRepo()
            {
                var candidates = new List<string>();
                foreach (string root in FixedRoots())
                {
                    candidates.Add(Path.Combine(root, "Deepseek-harness"));
                    candidates.Add(Path.Combine(root, "deepseek-harness"));
                    try
                    {
                        foreach (string d in Directory.GetDirectories(root))
                        {
                            if (Path.GetFileName(d).IndexOf("harness", StringComparison.OrdinalIgnoreCase) >= 0) candidates.Add(d);
                        }
                    }
                    catch { }
                }
                try { candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Deepseek-harness")); } catch { }
                foreach (string c in candidates)
                {
                    try { if (File.Exists(Path.Combine(c, "apps", "cli", "src", "bin.ts"))) return c; } catch { }
                }
                return null;
            }

            private string FindDshHome(string repo)
            {
                var candidates = new List<string>();
                if (!string.IsNullOrEmpty(repo))
                {
                    try
                    {
                        string parent = Path.GetDirectoryName(repo);
                        if (!string.IsNullOrEmpty(parent))
                        {
                            candidates.Add(Path.Combine(parent, "Deepseek-harness-data"));
                            foreach (string d in Directory.GetDirectories(parent))
                            {
                                if (Path.GetFileName(d).IndexOf("harness-data", StringComparison.OrdinalIgnoreCase) >= 0) candidates.Add(d);
                            }
                        }
                    }
                    catch { }
                }
                foreach (string root in FixedRoots())
                {
                    candidates.Add(Path.Combine(root, "Deepseek-harness-data"));
                    try
                    {
                        foreach (string d in Directory.GetDirectories(root))
                        {
                            if (Path.GetFileName(d).IndexOf("harness-data", StringComparison.OrdinalIgnoreCase) >= 0) candidates.Add(d);
                        }
                    }
                    catch { }
                }
                try { candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Deepseek-harness-data")); } catch { }
                foreach (string c in candidates)
                {
                    try { if (Directory.Exists(c) && Directory.Exists(Path.Combine(c, "profiles"))) return c; } catch { }
                }
                foreach (string c in candidates)
                {
                    try { if (Directory.Exists(c)) return c; } catch { }
                }
                return null;
            }

            private void DetectAll()
            {
                var found = new List<string>();
                var missed = new List<string>();

                string node = FindNode();
                if (node != null) { txtNode.Text = node; found.Add("Node"); } else missed.Add("Node");

                string repo = FindDshRepo();
                if (repo != null) { txtRepo.Text = repo; found.Add("DSH 部署目录"); } else missed.Add("DSH 部署目录");

                string home = FindDshHome(repo);
                if (home == null && !string.IsNullOrWhiteSpace(txtHome.Text)) home = txtHome.Text.Trim();
                if (home != null) { txtHome.Text = home; found.Add("DSH 数据目录"); } else missed.Add("DSH 数据目录");

                if (!string.IsNullOrEmpty(home))
                {
                    txtLog.Text = Path.Combine(home, "logs");
                    txtDeploy.Text = Path.Combine(home, "dsh-tray-monitor");
                    found.Add("日志目录");
                    found.Add("部署目录");
                }

                string msg = "已自动检测：" + (found.Count > 0 ? string.Join("、", found) : "无");
                if (missed.Count > 0) msg += "；未检测到：" + string.Join("、", missed) + "（请手动选择）";
                SetStatus(msg, missed.Count > 0);
            }

            private bool Validate(out string err, out string actionUrl)
            {
                actionUrl = null;
                int port;
                if (!int.TryParse(txtPort.Text.Trim(), out port) || port < 1 || port > 65535)
                { err = "端口必须是 1-65535 的整数。"; return false; }
                if (string.IsNullOrWhiteSpace(txtNode.Text) || !File.Exists(txtNode.Text.Trim()))
                { err = "未检测到 Node.js（可点「自动检测」或「浏览…」选择 node.exe），DSH 依赖 Node.js 运行（Node 22.19+ 或 24+）。"; actionUrl = "https://nodejs.org/"; return false; }
                if (string.IsNullOrWhiteSpace(txtRepo.Text) || !Directory.Exists(txtRepo.Text.Trim()))
                { err = "DSH 部署目录不存在，请先部署 DeepSeek Harness 或点「自动检测」。"; actionUrl = Program.DshRepoUrl; return false; }
                if (!File.Exists(Path.Combine(txtRepo.Text.Trim(), "apps", "cli", "src", "bin.ts")))
                { err = "DSH 部署目录中未找到 apps\\cli\\src\\bin.ts，请确认选择的是 DSH 源码目录。"; actionUrl = Program.DshRepoUrl; return false; }
                if (string.IsNullOrWhiteSpace(txtHome.Text))
                { err = "请填写 DSH 数据目录（DSH_HOME），例如 D:\\Deepseek-harness-data。"; return false; }
                err = "";
                return true;
            }

            private bool RunValidate()
            {
                string err, url;
                if (Validate(out err, out url)) { SetStatus("校验通过。", false); return true; }
                SetStatus(err, true);
                if (!string.IsNullOrEmpty(url))
                {
                    var r = MessageBox.Show(err + "\n\n是否打开官方下载/仓库页面？", "环境缺失", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r == DialogResult.Yes) { try { Process.Start(url); } catch { } }
                }
                return false;
            }

            private void Deploy()
            {
                if (!RunValidate()) return;

                int port = int.Parse(txtPort.Text.Trim());
                string node = txtNode.Text.Trim();
                string repo = txtRepo.Text.Trim();
                string home = txtHome.Text.Trim();
                string logDir = txtLog.Text.Trim();
                if (string.IsNullOrWhiteSpace(logDir)) logDir = Path.Combine(home, "logs");
                string deployDir = txtDeploy.Text.Trim();
                if (string.IsNullOrWhiteSpace(deployDir)) deployDir = Path.Combine(home, "dsh-tray-monitor");

                try
                {
                    Directory.CreateDirectory(deployDir);
                    Directory.CreateDirectory(Path.Combine(deployDir, "ico"));
                    Directory.CreateDirectory(logDir);

                    string exe = Path.Combine(Program.TrayDir, "dsh-tray-monitor.exe");
                    string destExe = Path.Combine(deployDir, "dsh-tray-monitor.exe");
                    bool sameDir = string.Equals(
                        Path.GetFullPath(deployDir).TrimEnd('\\'),
                        Path.GetFullPath(Program.TrayDir).TrimEnd('\\'),
                        StringComparison.OrdinalIgnoreCase);
                    if (!sameDir) File.Copy(exe, destExe, true);

                    Program.CopyIcons(deployDir);

                    File.WriteAllText(Path.Combine(deployDir, "config.json"), Program.BuildConfigJson(port, node, repo, home, logDir, deployDir), new System.Text.UTF8Encoding(false));
                    File.WriteAllText(Path.Combine(deployDir, "启动DSH.ps1"), Program.BuildStartScript(node, repo, home, logDir, port), new System.Text.UTF8Encoding(true));
                    File.WriteAllText(Path.Combine(deployDir, "停止DSH.ps1"), Program.BuildStopScript(port), new System.Text.UTF8Encoding(true));
                    File.WriteAllText(Path.Combine(deployDir, "启动托盘.cmd"), Program.BuildLauncherCmd(), new System.Text.UTF8Encoding(false));

                    Program.SetAutoStart(Program.RunKeyName, chkMon.Checked, "\"" + destExe + "\"");
                    string ps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe");
                    string startAbs = Path.Combine(deployDir, "启动DSH.ps1");
                    Program.SetAutoStart(Program.RunKeyNameDsh, chkDsh.Checked,
                        "\"" + ps + "\" -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + startAbs + "\"");

                    if (sameDir)
                    {
                        Program.ApplyDeployedConfig(port, node, repo, home, logDir, deployDir);
                        SetStatus("已部署并应用配置。", false);
                    }
                    else
                    {
                        var r = MessageBox.Show(
                            "已部署到：\n" + deployDir + "\n\n是否切换到新目录并重启监控？\n（选“是”：当前监控退出并启动新实例）",
                            "部署完成", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (r == DialogResult.Yes)
                        {
                            Program.SwitchTo(deployDir);
                        }
                        else
                        {
                            SetStatus("已部署到新目录（当前实例未切换）。", false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    SetStatus("部署失败：" + ex.Message, true);
                }
            }

            private void SetStatus(string s, bool isError)
            {
                lblStatus.Text = s;
                lblStatus.ForeColor = isError ? Color.Red : Color.Green;
            }
        }
    }
}