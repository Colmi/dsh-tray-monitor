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
        // ---- 默认配置（可用同目录 config.json 覆盖）----
        private static int Port = 3080;
        private static string Url = "http://127.0.0.1:3080";
        private static string LogFile = @"D:\Deepseek-harness-data\logs\tray.log";
        private static string DataDir = @"D:\Deepseek-harness-data";
        private static string StartScript = "启动DSH.ps1";
        private static string StopScript = "停止DSH.ps1";

        private const string RunKeyName = "DSHTrayMonitor";
        private static readonly string TrayDir = AppDomain.CurrentDomain.BaseDirectory;

        private static NotifyIcon _ni;
        private static ContextMenuStrip _menu;
        private static System.Windows.Forms.Timer _timer;
        private static ToolStripMenuItem _miStatus, _miStart, _miStop, _miRestart, _miAuto, _miExit;
        private static Icon _iconRunning, _iconStopped;
        private static bool _lastUp;
        private static string _pidStr = "";

        [STAThread]
        private static void Main()
        {
            // 全局异常保护：记录日志，避免托盘因个别异常退出
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

                _iconRunning = LoadIcon("dsh-logo-running.ico");
                _iconStopped = LoadIcon("dsh-logo-stopped.ico");

                _ni = new NotifyIcon { Icon = _iconRunning, Visible = true, Text = "DSH 状态检测中..." };
                BuildMenu();
                _ni.ContextMenuStrip = _menu;
                _ni.DoubleClick += (s, e) => { try { Process.Start(Url); } catch { } };

                _timer = new System.Windows.Forms.Timer { Interval = 3000 };
                _timer.Tick += (s, e) => UpdateStatus();

                WriteLog("tray monitor started (exe, config-loaded)");
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
                if (!File.Exists(cfgPath)) return;
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
            }
            catch (Exception ex) { WriteLog("config load error: " + ex.Message); }
        }

        private static Icon LoadIcon(string name)
        {
            try { return new Icon(Path.Combine(TrayDir, name)); }
            catch { return SystemIcons.Application; }
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

        private static bool IsUp()
        {
            try
            {
                using (var c = new TcpClient())
                {
                    var ar = c.BeginConnect("127.0.0.1", Port, null, null);
                    if (ar.AsyncWaitHandle.WaitOne(600, false) && c.Connected) return true;
                    return false;
                }
            }
            catch { return false; }
        }

        // 通过 netstat 解析监听 3080 的进程 PID（避免 PowerShell 出错串）
        private static string GetPidString()
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
                            if (int.TryParse(parts[parts.Length - 1], out n))
                                return " (PID " + n + ")";
                        }
                    }
                }
            }
            catch { }
            return "";
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
            _miAuto = new ToolStripMenuItem("开机自启（关）");
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
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_miAuto);
            _menu.Items.Add(_miExit);

            _miStart.Click += (s, e) => InvokeAction("start");
            _miStop.Click += (s, e) => InvokeAction("stop");
            _miRestart.Click += (s, e) => InvokeAction("restart");
            miOpen.Click += (s, e) => { try { Process.Start(Url); } catch { } };
            miData.Click += (s, e) => { try { Process.Start("explorer.exe", DataDir); } catch { } };
            miLog.Click += (s, e) => { try { Process.Start("notepad.exe", LogFile); } catch { } };
            _miAuto.Click += (s, e) => ToggleAutoStart();
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

        private static void UpdateStatus()
        {
            bool up = IsUp();
            if (up != _lastUp)
            {
                _lastUp = up;
                if (up)
                {
                    _ni.Icon = _iconRunning;
                    _ni.ShowBalloonTip(2000, "DSH 运行中", "DSH Web 服务已就绪：" + Url, ToolTipIcon.Info);
                    WriteLog("status: running");
                }
                else
                {
                    _ni.Icon = _iconStopped;
                    _ni.ShowBalloonTip(2000, "DSH 已停止", "DSH Web 服务当前未运行，可右键菜单启动", ToolTipIcon.Warning);
                    WriteLog("status: stopped");
                }
                _pidStr = up ? GetPidString() : "";
            }
            string state = up ? "运行中" : "已停止";
            _ni.Text = Truncate("DSH " + state + _pidStr, 63);
            _miStatus.Text = "状态：" + state + _pidStr;
            _miStart.Enabled = !up;
            _miStop.Enabled = up;
            _miRestart.Enabled = up;
            _miAuto.Text = "开机自启（" + (IsAutoStartOn() ? "开" : "关") + "）";
        }
    }
}