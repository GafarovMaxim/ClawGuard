using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Forms = System.Windows.Forms;
using Image = System.Windows.Controls.Image;
using Rectangle = System.Windows.Shapes.Rectangle;
using Path = System.IO.Path;

[assembly: AssemblyTitle("ClawGuard")]
[assembly: AssemblyDescription("ClawGuard Network Security Companion for Claude")]
[assembly: AssemblyProduct("ClawGuard")]
[assembly: AssemblyCompany("ClawGuard")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Gafarov Maxim")]
[assembly: AssemblyVersion("2.5.1.0")]
[assembly: AssemblyFileVersion("2.5.1.0")]

namespace ClawGuard
{
    public static class Win32
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        public static void EnableDarkTitleBar(Window window)
        {
            try
            {
                // In WPF, set a 1x1 transparent icon to remove the default title icon cleanly
                window.Icon = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);

                IntPtr hwnd = new WindowInteropHelper(window).EnsureHandle();
                if (hwnd != IntPtr.Zero)
                {
                    int trueVal = 1;
                    DwmSetWindowAttribute(hwnd, 20, ref trueVal, sizeof(int));
                    DwmSetWindowAttribute(hwnd, 19, ref trueVal, sizeof(int));
                    int captionColor = 0x001c1414; // BGR #14141c
                    DwmSetWindowAttribute(hwnd, 35, ref captionColor, sizeof(int));
                }
            }
            catch { }
        }
    }

    public class TimeoutWebClient : WebClient
    {
        public int TimeoutMs { get; set; }
        public TimeoutWebClient(int timeoutMs) { TimeoutMs = timeoutMs; }
        public TimeoutWebClient() : this(3500) { }

        protected override WebRequest GetWebRequest(Uri uri)
        {
            WebRequest w = base.GetWebRequest(uri);
            w.Timeout = TimeoutMs;
            return w;
        }
    }

    public static class NativeIPResolver
    {
        public static volatile bool IsBusy = false;
        public static volatile bool HasNewResult = false;
        public static string IP = null;
        public static string Country = null;
        public static string CountryCode = null;
        public static string ISP = null;
        public static bool Success = false;

        private static readonly object _lock = new object();

        public static void StartFetch()
        {
            if (IsBusy) return;
            lock (_lock)
            {
                if (IsBusy) return;
                IsBusy = true;
                HasNewResult = false;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                    bool ok = false;
                    string fetchedIP = null, fetchedCountry = null, fetchedCode = null, fetchedISP = null;

                    // 1. ipwho.is
                    try
                    {
                        using (var wc = new TimeoutWebClient(3500))
                        {
                            wc.Headers.Add("User-Agent", "ClawGuard/2.5");
                            string json = wc.DownloadString("https://ipwho.is/");
                            if (!string.IsNullOrEmpty(json) && json.Contains("\"success\":true"))
                            {
                                fetchedIP = MatchRegex(json, "\"ip\"\\s*:\\s*\"([^\"]+)\"");
                                fetchedCountry = MatchRegex(json, "\"country\"\\s*:\\s*\"([^\"]+)\"");
                                fetchedCode = MatchRegex(json, "\"country_code\"\\s*:\\s*\"([^\"]+)\"");
                                fetchedISP = MatchRegex(json, "\"isp\"\\s*:\\s*\"([^\"]+)\"");
                                if (!string.IsNullOrEmpty(fetchedIP)) ok = true;
                            }
                        }
                    }
                    catch { }

                    // 2. ip-api.com
                    if (!ok)
                    {
                        try
                        {
                            using (var wc = new TimeoutWebClient(3500))
                            {
                                wc.Headers.Add("User-Agent", "ClawGuard/2.5");
                                string json = wc.DownloadString("http://ip-api.com/json/?fields=query,isp,country,countryCode");
                                fetchedIP = MatchRegex(json, "\"query\"\\s*:\\s*\"([^\"]+)\"");
                                fetchedCountry = MatchRegex(json, "\"country\"\\s*:\\s*\"([^\"]+)\"");
                                fetchedCode = MatchRegex(json, "\"countryCode\"\\s*:\\s*\"([^\"]+)\"");
                                fetchedISP = MatchRegex(json, "\"isp\"\\s*:\\s*\"([^\"]+)\"");
                                if (!string.IsNullOrEmpty(fetchedIP)) ok = true;
                            }
                        }
                        catch { }
                    }

                    // 3. ipinfo.io
                    if (!ok)
                    {
                        try
                        {
                            using (var wc = new TimeoutWebClient(3500))
                            {
                                wc.Headers.Add("User-Agent", "ClawGuard/2.5");
                                string json = wc.DownloadString("https://ipinfo.io/json");
                                fetchedIP = MatchRegex(json, "\"ip\"\\s*:\\s*\"([^\"]+)\"");
                                fetchedCountry = MatchRegex(json, "\"country\"\\s*:\\s*\"([^\"]+)\"");
                                fetchedCode = MatchRegex(json, "\"country\"\\s*:\\s*\"([^\"]+)\"");
                                fetchedISP = MatchRegex(json, "\"org\"\\s*:\\s*\"([^\"]+)\"");
                                if (!string.IsNullOrEmpty(fetchedIP)) ok = true;
                            }
                        }
                        catch { }
                    }

                    // 4. api.ipify.org
                    if (!ok)
                    {
                        try
                        {
                            using (var wc = new TimeoutWebClient(3500))
                            {
                                wc.Headers.Add("User-Agent", "ClawGuard/2.5");
                                fetchedIP = wc.DownloadString("https://api.ipify.org").Trim();
                                fetchedCountry = "-";
                                fetchedCode = null;
                                fetchedISP = "-";
                                if (!string.IsNullOrEmpty(fetchedIP)) ok = true;
                            }
                        }
                        catch { }
                    }

                    lock (_lock)
                    {
                        Success = ok;
                        IP = fetchedIP;
                        Country = fetchedCountry ?? "-";
                        CountryCode = fetchedCode;
                        ISP = fetchedISP ?? "-";
                        HasNewResult = true;
                        IsBusy = false;
                    }
                }
                catch
                {
                    lock (_lock)
                    {
                        Success = false;
                        IP = null;
                        Country = "-";
                        CountryCode = null;
                        ISP = "-";
                        HasNewResult = true;
                        IsBusy = false;
                    }
                }
            });
        }

        private static string MatchRegex(string text, string pattern)
        {
            var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }
    }

    public class AppConfig
    {
        public string PinnedIP { get; set; }
        public string ProcessName { get; set; }
        public int CheckInterval { get; set; }
        public bool AutoExitWithApp { get; set; }
        public bool NotifyOnTray { get; set; }
        public string Language { get; set; }

        public AppConfig()
        {
            PinnedIP = null;
            ProcessName = "Claude";
            CheckInterval = 2;
            AutoExitWithApp = false;
            NotifyOnTray = true;
            Language = "RU";
        }

        private static string ConfigPath
        {
            get
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string local = Path.Combine(baseDir, "ClawGuard.json");
                if (File.Exists(local)) return local;
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClawGuard");
                if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
                return Path.Combine(appData, "ClawGuard.json");
            }
        }

        public static AppConfig Load()
        {
            try
            {
                string p = ConfigPath;
                if (File.Exists(p))
                {
                    string json = File.ReadAllText(p, Encoding.UTF8);
                    var cfg = new AppConfig();
                    string pinned = Match(json, "\"PinnedIP\"\\s*:\\s*\"([^\"]*)\"");
                    if (!string.IsNullOrEmpty(pinned)) cfg.PinnedIP = pinned;
                    string proc = Match(json, "\"ProcessName\"\\s*:\\s*\"([^\"]*)\"");
                    if (!string.IsNullOrEmpty(proc)) cfg.ProcessName = proc;
                    string interval = Match(json, "\"CheckInterval\"\\s*:\\s*(\\d+)");
                    int ival;
                    if (int.TryParse(interval, out ival) && ival > 0) cfg.CheckInterval = ival;
                    string autoExit = Match(json, "\"AutoExitWithApp\"\\s*:\\s*(true|false)");
                    if (!string.IsNullOrEmpty(autoExit)) cfg.AutoExitWithApp = bool.Parse(autoExit);
                    string notify = Match(json, "\"NotifyOnTray\"\\s*:\\s*(true|false)");
                    if (!string.IsNullOrEmpty(notify)) cfg.NotifyOnTray = bool.Parse(notify);
                    string lang = Match(json, "\"Language\"\\s*:\\s*\"([^\"]*)\"");
                    if (!string.IsNullOrEmpty(lang)) cfg.Language = lang;
                    return cfg;
                }
            }
            catch { }
            return new AppConfig();
        }

        public void Save()
        {
            try
            {
                string p = ConfigPath;
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine(string.Format("  \"PinnedIP\": {0},", PinnedIP == null ? "null" : "\"" + PinnedIP + "\""));
                sb.AppendLine(string.Format("  \"ProcessName\": \"{0}\",", ProcessName ?? "Claude"));
                sb.AppendLine(string.Format("  \"CheckInterval\": {0},", CheckInterval));
                sb.AppendLine(string.Format("  \"AutoExitWithApp\": {0},", AutoExitWithApp ? "true" : "false"));
                sb.AppendLine(string.Format("  \"NotifyOnTray\": {0},", NotifyOnTray ? "true" : "false"));
                sb.AppendLine(string.Format("  \"Language\": \"{0}\"", Language ?? "RU"));
                sb.AppendLine("}");
                File.WriteAllText(p, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static string Match(string text, string pattern)
        {
            var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }
    }

    public class AppState
    {
        public string CurrentIP;
        public string CountryCode;
        public string Country;
        public string ISP;
        public string PinnedIP;
        public bool IsRunning;
        public Process GuardProc;
    }

    public class Program
    {
        private static Mutex _singleInstanceMutex;
        private static Window _window;
        private static AppConfig _config;
        private static AppState _state = new AppState();
        private static Forms.NotifyIcon _notifyIcon;
        private static bool _quitting = false;
        private static bool _isClaudeRunning = false;
        private static string _currentCrabMode = "Sleep";
        private static int _walkLegTimer = 0;
        private static int _walkPhase = 0;
        private static int _awakeBobTimer = 0;
        private static bool _awakeBobToggle = false;
        private static int _awakeTick = 0;
        private static double _zzzOffset = 0.0;
        private static double _zzzDir = 1.0;
        private static int _tickCount = 0;
        private static string _currentDrawerMode = null;

        // UI Element References
        private static Image _imgTitleLogo;
        private static TextBlock _txtAppTitle;
        private static Button _btnConfig;
        private static Button _btnLog;
        private static Button _btnLang;
        private static TextBlock _txtLangBtn;
        private static System.Windows.Shapes.Ellipse _statusDot;
        private static TextBlock _txtStatusShort;

        private static TextBlock _lblPublicIP;
        private static TextBlock _txtIP;
        private static TextBlock _lblCountry;
        private static Border _bdFlag;
        private static Image _imgFlag;
        private static TextBlock _txtCountry;
        private static TextBlock _lblISP;
        private static TextBlock _txtISP;

        private static TextBlock _lblPinnedIP;
        private static TextBlock _txtPinnedIP;
        private static Button _btnPin;

        private static TextBlock _txtGuardDot;
        private static TextBlock _txtGuardStatus;
        private static TextBlock _txtProtecting;

        private static Button _btnStart;
        private static Button _btnStop;
        private static Button _btnClaude;

        private static Image _imgClaudeLogo;
        private static Canvas _trackCanvas;
        private static Canvas _dayLayer;
        private static Canvas _dayCloud1;
        private static Canvas _dayCloud2;
        private static Canvas _sunHost;
        private static Canvas _nightLayer;
        private static TextBlock _star1;
        private static TextBlock _star2;
        private static TextBlock _star3;
        private static TextBlock _star4;
        private static Canvas _nightCloud1;
        private static Canvas _nightCloud2;
        private static Canvas _moonHost;
        private static Canvas _sinisterLayer;
        private static Canvas _stormCloud;
        private static Canvas _bloodMoonHost;
        private static Line _trackLine;
        private static Line _brokenTrackLine;
        private static Canvas _crabHost;
        private static TextBlock _txtZzz;
        private static TextBlock _txtCrabStatus;

        private static Border _drawerOverlay;
        private static Border _sideDrawer;
        private static TextBlock _txtDrawerIcon;
        private static TextBlock _txtDrawerTitle;
        private static Button _btnDrawerClose;
        private static StackPanel _viewConfig;
        private static TextBox _inPinnedIP;
        private static Button _btnCfgUseCurrent;
        private static TextBox _inProcess;
        private static TextBox _inInterval;
        private static CheckBox _chkAutoExit;
        private static CheckBox _chkTray;
        private static Button _btnCfgCancel;
        private static Button _btnCfgSave;
        private static TextBlock _lblCfgPinnedIP;
        private static TextBlock _lblCfgProcess;
        private static TextBlock _lblCfgInterval;

        private static Grid _viewLog;
        private static TextBox _txtLogBody;
        private static Button _btnCopyLog;
        private static Button _btnClearLog;

        private static Dictionary<string, Canvas> _crabFrames;
        private static DropShadowEffect _claudeRedGlow;
        private static DispatcherTimer _crabTimer;
        private static DispatcherTimer _mainTimer;
        private static List<string> _logEntries = new List<string>();

        // Verbatim Localization Dictionary
        private static readonly Dictionary<string, string> StringsEN = new Dictionary<string, string>
        {
            { "AppTitle", "ClawGuard" },
            { "PublicIP", "PUBLIC IP" },
            { "Country", "COUNTRY" },
            { "ISP", "ISP" },
            { "PinnedIP", "PINNED IP" },
            { "PinBtn", "📌  Pin Current" },
            { "PinUpdate", "📌  Update PIN" },
            { "NotSet", "not set" },
            { "GuardActive", "Guard active" },
            { "GuardStopped", "Guard stopped" },
            { "StartBtn", "▶  Start Guard" },
            { "StopBtn", "■  Stop Guard" },
            { "LaunchClaude", "🚀  Launch Claude" },
            { "Protecting", "Protecting: {0}" },
            { "NoPIN", "No PIN" },
            { "Offline", "Offline" },
            { "IPOk", "IP OK" },
            { "IPChanged", "IP changed!" },
            { "NoConnection", "No connection" },
            { "TrayTitle", "ClawGuard" },
            { "TrayBg", "Running in background." },
            { "TrayShow", "Show" },
            { "TrayQuit", "Quit" },
            { "MsgNoIP", "Cannot detect current IP. Check your connection." },
            { "MsgPinFirst", "Pin an IP first." },
            { "MsgMismatch", "IP does not match the pinned address." },
            { "CrabSleep", "[ 💤 GUARD OFF • CRAB IS SLEEPING ]" },
            { "CrabAwake", "[ 🛡️ ON GUARD • WAITING FOR CLAUDE ]" },
            { "CrabWalking", "[ ⚡ CLAUDE ACTIVE • PATROLLING IP ]" },
            { "CrabDead", "[ ☠️ IP LEAK! CRAB DIED, CLAUDE CLOSED ]" },
            { "CfgTitle", "Settings" },
            { "CfgPinnedIP", "Pinned IP:" },
            { "CfgUseCurr", "Current" },
            { "CfgProcess", "Protected App:" },
            { "CfgInterval", "Interval (sec):" },
            { "CfgAutoExit", "Close ClawGuard when Claude exits" },
            { "CfgResetTray", "Notify when minimized to tray" },
            { "CfgSave", "Save" },
            { "CfgCancel", "Cancel" },
            { "LogTitle", "Activity Log" },
            { "LogCopy", "Copy" },
            { "LogClear", "Clear" }
        };

        private static readonly Dictionary<string, string> StringsRU = new Dictionary<string, string>
        {
            { "AppTitle", "ClawGuard" },
            { "PublicIP", "IP-АДРЕС" },
            { "Country", "СТРАНА" },
            { "ISP", "ПРОВАЙДЕР" },
            { "PinnedIP", "ЗАКРЕПЛЁННЫЙ IP" },
            { "PinBtn", "📌  Закрепить" },
            { "PinUpdate", "📌  Обновить" },
            { "NotSet", "не задан" },
            { "GuardActive", "Свитчер активен" },
            { "GuardStopped", "Свитчер остановлен" },
            { "StartBtn", "▶  Запустить" },
            { "StopBtn", "■  Остановить" },
            { "LaunchClaude", "🚀  Запустить Claude" },
            { "Protecting", "Защищает: {0}" },
            { "NoPIN", "Нет IP" },
            { "Offline", "Офлайн" },
            { "IPOk", "OK" },
            { "IPChanged", "Сменился!" },
            { "NoConnection", "Нет соединения" },
            { "TrayTitle", "ClawGuard" },
            { "TrayBg", "Работает в фоновом режиме." },
            { "TrayShow", "Показать" },
            { "TrayQuit", "Выход" },
            { "MsgNoIP", "Не удалось определить текущий IP. Проверьте подключение." },
            { "MsgPinFirst", "Сначала закрепите IP-адрес." },
            { "MsgMismatch", "IP не совпадает с закреплённым адресом." },
            { "CrabSleep", "[ 💤 СВИТЧЕР ВЫКЛЮЧЕН • КРАБ СПИТ ]" },
            { "CrabAwake", "[ 🛡️ НА СТРАЖЕ • ОЖИДАНИЕ CLAUDE ]" },
            { "CrabWalking", "[ ⚡ CLAUDE АКТИВЕН • ПАТРУЛИРОВАНИЕ IP ]" },
            { "CrabDead", "[ ☠️ УТЕЧКА IP! КРАБ ПОГИБ, CLAUDE ЗАКРЫТ ]" },
            { "CfgTitle", "Настройки" },
            { "CfgPinnedIP", "Закреплённый IP:" },
            { "CfgUseCurr", "Текущий" },
            { "CfgProcess", "Защищаемый процесс:" },
            { "CfgInterval", "Интервал (сек):" },
            { "CfgAutoExit", "Закрывать вместе с Claude" },
            { "CfgResetTray", "Уведомлять о сворачивании в трей" },
            { "CfgSave", "Сохранить" },
            { "CfgCancel", "Отмена" },
            { "LogTitle", "Журнал событий" },
            { "LogCopy", "Копировать" },
            { "LogClear", "Очистить" }
        };

        private static string GetText(string key)
        {
            bool isRu = string.Equals(_config.Language, "RU", StringComparison.OrdinalIgnoreCase);
            var dict = isRu ? StringsRU : StringsEN;
            string val;
            if (dict.TryGetValue(key, out val)) return val;
            if (StringsEN.TryGetValue(key, out val)) return val;
            return key;
        }

        [STAThread]
        public static void Main(string[] args)
        {
            bool isNewInstance;
            _singleInstanceMutex = new Mutex(true, "ClawGuard_SingleInstance_Mutex", out isNewInstance);
            if (!isNewInstance)
            {
                try
                {
                    int curId = Process.GetCurrentProcess().Id;
                    foreach (var p in Process.GetProcessesByName("ClawGuard"))
                    {
                        if (p.Id != curId && p.MainWindowHandle != IntPtr.Zero)
                        {
                            Win32.ShowWindowAsync(p.MainWindowHandle, 9); // SW_RESTORE
                            Win32.SetForegroundWindow(p.MainWindowHandle);
                            break;
                        }
                    }
                }
                catch { }
                return;
            }

            _config = AppConfig.Load();
            _state.PinnedIP = _config.PinnedIP;

            var app = new Application();
            BuildUI();
            app.Run(_window);
        }

        private static void Log(string msg, string type = "INFO")
        {
            string line = string.Format("[{0:HH:mm:ss}] [{1}] {2}", DateTime.Now, type, msg);
            _logEntries.Add(line);
            if (_logEntries.Count > 400) _logEntries.RemoveAt(0);
            if (_txtLogBody != null)
            {
                _txtLogBody.Text = string.Join(Environment.NewLine, _logEntries.ToArray());
                _txtLogBody.ScrollToEnd();
            }
        }

        private static void BuildUI()
        {
            // Parse verbatim original XAML
            _window = (Window)XamlReader.Parse(AppXaml.Xaml);
            Win32.EnableDarkTitleBar(_window);

            // Bind Window Elements
            _imgTitleLogo = (Image)_window.FindName("imgTitleLogo");
            _txtAppTitle = (TextBlock)_window.FindName("txtAppTitle");
            _btnConfig = (Button)_window.FindName("btnConfig");
            _btnLog = (Button)_window.FindName("btnLog");
            _btnLang = (Button)_window.FindName("btnLang");
            _txtLangBtn = (TextBlock)_window.FindName("txtLangBtn");
            _statusDot = (System.Windows.Shapes.Ellipse)_window.FindName("statusDot");
            _txtStatusShort = (TextBlock)_window.FindName("txtStatusShort");

            _lblPublicIP = (TextBlock)_window.FindName("lblPublicIP");
            _txtIP = (TextBlock)_window.FindName("txtIP");
            _lblCountry = (TextBlock)_window.FindName("lblCountry");
            _bdFlag = (Border)_window.FindName("bdFlag");
            _imgFlag = (Image)_window.FindName("imgFlag");
            _txtCountry = (TextBlock)_window.FindName("txtCountry");
            _lblISP = (TextBlock)_window.FindName("lblISP");
            _txtISP = (TextBlock)_window.FindName("txtISP");

            _lblPinnedIP = (TextBlock)_window.FindName("lblPinnedIP");
            _txtPinnedIP = (TextBlock)_window.FindName("txtPinnedIP");
            _btnPin = (Button)_window.FindName("btnPin");

            _txtGuardDot = (TextBlock)_window.FindName("txtGuardDot");
            _txtGuardStatus = (TextBlock)_window.FindName("txtGuardStatus");
            _txtProtecting = (TextBlock)_window.FindName("txtProtecting");

            _btnStart = (Button)_window.FindName("btnStart");
            _btnStop = (Button)_window.FindName("btnStop");
            _btnClaude = (Button)_window.FindName("btnClaude");

            // Claude Terminal Panel
            _imgClaudeLogo = (Image)_window.FindName("imgClaudeLogo");
            _trackCanvas = (Canvas)_window.FindName("trackCanvas");
            _dayLayer = (Canvas)_window.FindName("dayLayer");
            _dayCloud1 = (Canvas)_window.FindName("dayCloud1");
            _dayCloud2 = (Canvas)_window.FindName("dayCloud2");
            _sunHost = (Canvas)_window.FindName("sunHost");

            _nightLayer = (Canvas)_window.FindName("nightLayer");
            _star1 = (TextBlock)_window.FindName("star1");
            _star2 = (TextBlock)_window.FindName("star2");
            _star3 = (TextBlock)_window.FindName("star3");
            _star4 = (TextBlock)_window.FindName("star4");
            _nightCloud1 = (Canvas)_window.FindName("nightCloud1");
            _nightCloud2 = (Canvas)_window.FindName("nightCloud2");
            _moonHost = (Canvas)_window.FindName("moonHost");

            _sinisterLayer = (Canvas)_window.FindName("sinisterLayer");
            _stormCloud = (Canvas)_window.FindName("stormCloud");
            _bloodMoonHost = (Canvas)_window.FindName("bloodMoonHost");

            _trackLine = (Line)_window.FindName("trackLine");
            _brokenTrackLine = (Line)_window.FindName("brokenTrackLine");
            _crabHost = (Canvas)_window.FindName("crabHost");
            _txtZzz = (TextBlock)_window.FindName("txtZzz");
            _txtCrabStatus = (TextBlock)_window.FindName("txtCrabStatus");

            // Drawer
            _drawerOverlay = (Border)_window.FindName("drawerOverlay");
            _sideDrawer = (Border)_window.FindName("sideDrawer");
            _txtDrawerIcon = (TextBlock)_window.FindName("txtDrawerIcon");
            _txtDrawerTitle = (TextBlock)_window.FindName("txtDrawerTitle");
            _btnDrawerClose = (Button)_window.FindName("btnDrawerClose");

            _viewConfig = (StackPanel)_window.FindName("viewConfig");
            _lblCfgPinnedIP = (TextBlock)_window.FindName("lblCfgPinnedIP");
            _inPinnedIP = (TextBox)_window.FindName("inPinnedIP");
            _btnCfgUseCurrent = (Button)_window.FindName("btnCfgUseCurrent");
            _lblCfgProcess = (TextBlock)_window.FindName("lblCfgProcess");
            _inProcess = (TextBox)_window.FindName("inProcess");
            _lblCfgInterval = (TextBlock)_window.FindName("lblCfgInterval");
            _inInterval = (TextBox)_window.FindName("inInterval");
            _chkAutoExit = (CheckBox)_window.FindName("chkAutoExit");
            _chkTray = (CheckBox)_window.FindName("chkTray");
            _btnCfgCancel = (Button)_window.FindName("btnCfgCancel");
            _btnCfgSave = (Button)_window.FindName("btnCfgSave");

            _viewLog = (Grid)_window.FindName("viewLog");
            _txtLogBody = (TextBox)_window.FindName("txtLogBody");
            _btnCopyLog = (Button)_window.FindName("btnCopyLog");
            _btnClearLog = (Button)_window.FindName("btnClearLog");

            // Red glow effect for Claude Logo alert
            _claudeRedGlow = new DropShadowEffect
            {
                Color = Color.FromArgb(255, 224, 80, 80),
                BlurRadius = 14,
                ShadowDepth = 0,
                Opacity = 0.85
            };

            // Set Claude Logo to normal terracotta
            _imgClaudeLogo.Source = EmbeddedAssets.GetClaudeLogo();

            // Set Title Logo
            ImageSource titleIcon = EmbeddedAssets.GetAppIconSource();
            if (titleIcon != null) _imgTitleLogo.Source = titleIcon;

            // Build Pixel Art Scenery & Crab Frames
            BuildPixelScenery();
            BuildPixelCrab();

            // Setup Tray Icon
            SetupTray();

            // Wire UI Event Handlers
            _btnConfig.Click += (s, e) => OpenDrawer("config");
            _btnLog.Click += (s, e) => OpenDrawer("log");
            _btnLang.Click += (s, e) => ToggleLanguage();
            _btnDrawerClose.Click += (s, e) => CloseDrawer();
            _drawerOverlay.MouseDown += (s, e) => CloseDrawer();

            _btnCfgUseCurrent.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_state.CurrentIP))
                    _inPinnedIP.Text = _state.CurrentIP;
            };

            _btnCfgSave.Click += (s, e) => SaveConfigFromDrawer();
            _btnCfgCancel.Click += (s, e) => CloseDrawer();

            _btnCopyLog.Click += (s, e) =>
            {
                try { Clipboard.SetText(_txtLogBody.Text ?? ""); Log("Log copied to clipboard.", "UI"); } catch { }
            };
            _btnClearLog.Click += (s, e) =>
            {
                _logEntries.Clear();
                _txtLogBody.Text = "";
                Log("Log buffer cleared.", "UI");
            };

            _btnPin.Click += (s, e) => PinCurrentIP();
            _btnStart.Click += (s, e) => StartGuard();
            _btnStop.Click += (s, e) => StopGuard();
            _btnClaude.Click += (s, e) => LaunchClaude();

            // Timers
            _crabTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
            _crabTimer.Tick += OnCrabTick;

            _mainTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _mainTimer.Tick += OnMainTick;

            _window.Loaded += (s, e) =>
            {
                ApplyLanguage();
                if (!string.IsNullOrEmpty(_state.PinnedIP))
                {
                    _txtPinnedIP.Text = _state.PinnedIP;
                    _txtPinnedIP.Foreground = (Brush)new BrushConverter().ConvertFrom("#2cb67d");
                }
                NativeIPResolver.StartFetch();
                UpdateGuardStatus();
                CheckClaudeProcess();
                UpdateCrabState();
                _mainTimer.Start();
                _crabTimer.Start();
                Log("ClawGuard UI loaded. Ready.", "INIT");
            };

            _window.Closing += (s, e) =>
            {
                if (!_quitting)
                {
                    e.Cancel = true;
                    _window.Hide();
                    if (_config.NotifyOnTray && _notifyIcon != null)
                    {
                        _notifyIcon.ShowBalloonTip(2000, GetText("TrayTitle"), GetText("TrayBg"), Forms.ToolTipIcon.Info);
                    }
                }
            };
        }

        #region Pixel Scenery & Mascot Builder

        private static void BuildPixelScenery()
        {
            double scale = 1.8;
            BrushConverter bc = new BrushConverter();

            Func<int, int, Canvas> makePxCanvas = (w, h) => new Canvas
            {
                Width = w * scale,
                Height = h * scale,
                ClipToBounds = false
            };

            Action<Canvas, int, int, Brush> drawPx = (cv, x, y, br) =>
            {
                var r = new Rectangle
                {
                    Width = scale,
                    Height = scale,
                    Fill = br
                };
                Canvas.SetLeft(r, x * scale);
                Canvas.SetTop(r, y * scale);
                cv.Children.Add(r);
            };

            // 1. Crescent Moon
            Func<bool, Canvas> makeMoon = (isBlood) =>
            {
                Canvas cv = makePxCanvas(9, 9);
                Brush hi = (Brush)bc.ConvertFrom(isBlood ? "#E05050" : "#F0F2FA");
                Brush mid = (Brush)bc.ConvertFrom(isBlood ? "#A83236" : "#B4B8D0");
                Brush dk = (Brush)bc.ConvertFrom(isBlood ? "#5A1A1E" : "#686C88");

                for (int x = 4; x <= 8; x++) drawPx(cv, x, 0, hi);
                drawPx(cv, 2, 1, hi); drawPx(cv, 3, 1, hi); drawPx(cv, 4, 1, mid); drawPx(cv, 8, 1, mid);
                drawPx(cv, 1, 2, hi); drawPx(cv, 2, 2, hi); drawPx(cv, 3, 2, mid); drawPx(cv, 7, 2, dk);
                drawPx(cv, 0, 3, hi); drawPx(cv, 1, 3, hi); drawPx(cv, 2, 3, mid);
                drawPx(cv, 0, 4, hi); drawPx(cv, 1, 4, hi); drawPx(cv, 2, 4, mid);
                drawPx(cv, 0, 5, hi); drawPx(cv, 1, 5, hi); drawPx(cv, 2, 5, mid);
                drawPx(cv, 1, 6, hi); drawPx(cv, 2, 6, hi); drawPx(cv, 3, 6, mid); drawPx(cv, 7, 6, dk);
                drawPx(cv, 2, 7, hi); drawPx(cv, 3, 7, hi); drawPx(cv, 4, 7, mid); drawPx(cv, 8, 7, mid);
                for (int x = 4; x <= 8; x++) drawPx(cv, x, 8, hi);
                return cv;
            };

            // 2. 8-Bit Pixel Sun
            Func<Canvas> makeSun = () =>
            {
                Canvas cv = makePxCanvas(8, 8);
                Brush ray = (Brush)bc.ConvertFrom("#F6C845");
                Brush core = (Brush)bc.ConvertFrom("#FFE660");

                drawPx(cv, 3, 0, ray); drawPx(cv, 4, 0, ray);
                drawPx(cv, 3, 7, ray); drawPx(cv, 4, 7, ray);
                drawPx(cv, 0, 3, ray); drawPx(cv, 0, 4, ray);
                drawPx(cv, 7, 3, ray); drawPx(cv, 7, 4, ray);
                drawPx(cv, 1, 1, ray); drawPx(cv, 6, 1, ray);
                drawPx(cv, 1, 6, ray); drawPx(cv, 6, 6, ray);
                for (int y = 2; y <= 5; y++)
                {
                    for (int x = 2; x <= 5; x++) drawPx(cv, x, y, core);
                }
                drawPx(cv, 3, 1, core); drawPx(cv, 4, 1, core);
                drawPx(cv, 3, 6, core); drawPx(cv, 4, 6, core);
                drawPx(cv, 1, 3, core); drawPx(cv, 1, 4, core);
                drawPx(cv, 6, 3, core); drawPx(cv, 6, 4, core);
                return cv;
            };

            // 3. Stepped Cloud Silhouette
            Func<string, string, Canvas> makeCloud = (colHex, hiHex) =>
            {
                Canvas cv = makePxCanvas(18, 6);
                Brush col = (Brush)bc.ConvertFrom(colHex);
                Brush hi = (Brush)bc.ConvertFrom(hiHex);

                for (int x = 4; x <= 10; x++) { drawPx(cv, x, 0, hi); drawPx(cv, x, 1, col); }
                for (int x = 2; x <= 14; x++) { drawPx(cv, x, 2, hi); drawPx(cv, x, 3, col); }
                for (int x = 0; x <= 17; x++) { drawPx(cv, x, 4, col); drawPx(cv, x, 5, col); }
                return cv;
            };

            _sunHost.Children.Add(makeSun());
            _moonHost.Children.Add(makeMoon(false));
            _bloodMoonHost.Children.Add(makeMoon(true));

            _dayCloud1.Children.Add(makeCloud("#454868", "#8588AC"));
            _dayCloud2.Children.Add(makeCloud("#454868", "#8588AC"));

            _nightCloud1.Children.Add(makeCloud("#28283c", "#3a3a54"));
            _nightCloud2.Children.Add(makeCloud("#28283c", "#3a3a54"));

            _stormCloud.Children.Add(makeCloud("#222230", "#5c222a"));
        }

        private static void BuildPixelCrab()
        {
            double scale = 2.4;
            BrushConverter bc = new BrushConverter();
            Brush cCrab = (Brush)bc.ConvertFrom("#D97757");
            Brush cEyeCutout = (Brush)bc.ConvertFrom("#0f0f16");
            Brush cRed = (Brush)bc.ConvertFrom("#E05050");
            Brush cWhite = (Brush)bc.ConvertFrom("#FFFFFF");

            Func<string, Canvas> makeFrame = (state) =>
            {
                Canvas c = new Canvas
                {
                    Width = 14 * scale,
                    Height = 11 * scale,
                    ClipToBounds = false
                };

                Brush b = (state == "Dead") ? cRed : cCrab;
                int yBob = (state == "AwakeB") ? 1 : 0;

                Action<int, int, Brush> px = (x, y, brush) =>
                {
                    var r = new Rectangle
                    {
                        Width = scale,
                        Height = scale,
                        Fill = brush
                    };
                    Canvas.SetLeft(r, x * scale);
                    Canvas.SetTop(r, y * scale);
                    c.Children.Add(r);
                };

                // Horns/Pincers
                int[] hornX = new int[] { 2, 3, 10, 11 };
                if (state == "AwakeLeft") hornX = new int[] { 1, 2, 9, 10 };
                if (state == "AwakeRight") hornX = new int[] { 3, 4, 11, 12 };
                foreach (int x in hornX)
                {
                    px(x, 0 + yBob, b);
                    px(x, 1 + yBob, b);
                }

                // Head top
                for (int x = 2; x <= 11; x++) px(x, 2 + yBob, b);

                // Main body
                for (int y = 3; y <= 6; y++)
                {
                    for (int x = 0; x <= 13; x++) px(x, y + yBob, b);
                }

                // Body bottom
                for (int x = 2; x <= 11; x++) px(x, 7 + yBob, b);

                // Eyes
                if (state == "Dead")
                {
                    px(3, 3, cWhite); px(4, 4, cWhite);
                    px(4, 3, cEyeCutout); px(3, 4, cEyeCutout);
                    px(9, 3, cWhite); px(10, 4, cWhite);
                    px(10, 3, cEyeCutout); px(9, 4, cEyeCutout);
                }
                else if (state == "Sleep")
                {
                    px(3, 4, cEyeCutout); px(4, 4, cEyeCutout);
                    px(9, 4, cEyeCutout); px(10, 4, cEyeCutout);
                }
                else if (state == "AwakeLeft")
                {
                    px(2, 3 + yBob, cEyeCutout); px(3, 3 + yBob, cEyeCutout);
                    px(2, 4 + yBob, cEyeCutout); px(3, 4 + yBob, cEyeCutout);
                    px(8, 3 + yBob, cEyeCutout); px(9, 3 + yBob, cEyeCutout);
                    px(8, 4 + yBob, cEyeCutout); px(9, 4 + yBob, cEyeCutout);
                }
                else if (state == "AwakeRight")
                {
                    px(4, 3 + yBob, cEyeCutout); px(5, 3 + yBob, cEyeCutout);
                    px(4, 4 + yBob, cEyeCutout); px(5, 4 + yBob, cEyeCutout);
                    px(10, 3 + yBob, cEyeCutout); px(11, 3 + yBob, cEyeCutout);
                    px(10, 4 + yBob, cEyeCutout); px(11, 4 + yBob, cEyeCutout);
                }
                else
                {
                    px(3, 3 + yBob, cEyeCutout); px(4, 3 + yBob, cEyeCutout);
                    px(3, 4 + yBob, cEyeCutout); px(4, 4 + yBob, cEyeCutout);
                    px(9, 3 + yBob, cEyeCutout); px(10, 3 + yBob, cEyeCutout);
                    px(9, 4 + yBob, cEyeCutout); px(10, 4 + yBob, cEyeCutout);
                }

                // Legs (4 rhythmic phases)
                int[] legCols;
                switch (state)
                {
                    case "Walk1": legCols = new int[] { 0, 1, 4, 5, 8, 9, 12, 13 }; break;
                    case "Walk2": legCols = new int[] { 1, 2, 4, 5, 8, 9, 11, 12 }; break;
                    case "Walk3": legCols = new int[] { 1, 2, 5, 6, 7, 8, 11, 12 }; break;
                    case "Walk4": legCols = new int[] { 1, 2, 4, 5, 8, 9, 11, 12 }; break;
                    default: legCols = new int[] { 1, 2, 4, 5, 8, 9, 11, 12 }; break;
                }
                foreach (int x in legCols)
                {
                    px(x, 8 + yBob, b);
                    px(x, 9 + yBob, b);
                }

                return c;
            };

            _crabFrames = new Dictionary<string, Canvas>
            {
                { "Sleep", makeFrame("Sleep") },
                { "AwakeA", makeFrame("AwakeA") },
                { "AwakeB", makeFrame("AwakeB") },
                { "AwakeLeft", makeFrame("AwakeLeft") },
                { "AwakeRight", makeFrame("AwakeRight") },
                { "Walk1", makeFrame("Walk1") },
                { "Walk2", makeFrame("Walk2") },
                { "Walk3", makeFrame("Walk3") },
                { "Walk4", makeFrame("Walk4") },
                { "Dead", makeFrame("Dead") }
            };

            foreach (var kvp in _crabFrames)
            {
                kvp.Value.Visibility = Visibility.Collapsed;
                _crabHost.Children.Add(kvp.Value);
            }
            _crabFrames["Sleep"].Visibility = Visibility.Visible;
        }

        private static void SetActiveCrabFrame(string frameKey)
        {
            if (_crabFrames == null) return;
            foreach (var kvp in _crabFrames)
            {
                kvp.Value.Visibility = (kvp.Key == frameKey) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        #endregion

        #region Language & Localization

        private static void ApplyLanguage()
        {
            _txtAppTitle.Text = GetText("AppTitle");
            _lblPublicIP.Text = GetText("PublicIP");
            _lblCountry.Text = GetText("Country");
            _lblISP.Text = GetText("ISP");
            _lblPinnedIP.Text = GetText("PinnedIP");

            _btnPin.Content = string.IsNullOrEmpty(_state.PinnedIP) ? GetText("PinBtn") : GetText("PinUpdate");
            _btnStart.Content = GetText("StartBtn");
            _btnStop.Content = GetText("StopBtn");
            _btnClaude.Content = GetText("LaunchClaude");

            _txtLangBtn.Text = string.Equals(_config.Language, "RU", StringComparison.OrdinalIgnoreCase) ? "RU" : "EN";

            // Drawer
            if (_currentDrawerMode == "config")
                _txtDrawerTitle.Text = GetText("CfgTitle");
            else if (_currentDrawerMode == "log")
                _txtDrawerTitle.Text = GetText("LogTitle");

            _lblCfgPinnedIP.Text = GetText("CfgPinnedIP");
            _btnCfgUseCurrent.Content = GetText("CfgUseCurr");
            _lblCfgProcess.Text = GetText("CfgProcess");
            _lblCfgInterval.Text = GetText("CfgInterval");
            _chkAutoExit.Content = GetText("CfgAutoExit");
            _chkTray.Content = GetText("CfgResetTray");
            _btnCfgSave.Content = GetText("CfgSave");
            _btnCfgCancel.Content = GetText("CfgCancel");

            _btnCopyLog.Content = GetText("LogCopy");
            _btnClearLog.Content = GetText("LogClear");

            UpdateStatusIndicator();
            UpdateGuardStatus();
            UpdateCrabState();
        }

        private static void ToggleLanguage()
        {
            _config.Language = string.Equals(_config.Language, "RU", StringComparison.OrdinalIgnoreCase) ? "EN" : "RU";
            _config.Save();
            ApplyLanguage();
            Log("Language switched to " + _config.Language, "UI");
        }

        #endregion

        #region Status Updates & Timer Loops

        private static void OnCrabTick(object sender, EventArgs e)
        {
            if (NativeIPResolver.HasNewResult)
            {
                NativeIPResolver.HasNewResult = false;
                ApplyIPInfoResult(NativeIPResolver.Success);
            }

            if (!_window.IsVisible) return; // 0% CPU in tray

            switch (_currentCrabMode)
            {
                case "Walking":
                    _txtZzz.Visibility = Visibility.Collapsed;
                    Canvas.SetLeft(_crabHost, 152);

                    _trackLine.StrokeDashOffset += 0.22;
                    if (_trackLine.StrokeDashOffset > 1000) _trackLine.StrokeDashOffset = 0;

                    _walkLegTimer++;
                    if (_walkLegTimer >= 3)
                    {
                        _walkLegTimer = 0;
                        _walkPhase = (_walkPhase + 1) % 4;
                        switch (_walkPhase)
                        {
                            case 0: SetActiveCrabFrame("Walk1"); break;
                            case 1: SetActiveCrabFrame("Walk2"); break;
                            case 2: SetActiveCrabFrame("Walk3"); break;
                            case 3: SetActiveCrabFrame("Walk4"); break;
                        }
                    }
                    break;

                case "Sleep":
                    Canvas.SetLeft(_crabHost, 152);
                    SetActiveCrabFrame("Sleep");
                    _txtZzz.Visibility = Visibility.Visible;
                    _zzzOffset += (0.10 * _zzzDir);
                    if (_zzzOffset > 3.5) _zzzDir = -1.0;
                    if (_zzzOffset < -1.5) _zzzDir = 1.0;
                    Canvas.SetTop(_txtZzz, 18 + _zzzOffset);
                    break;

                case "Awake":
                    Canvas.SetLeft(_crabHost, 152);
                    _txtZzz.Visibility = Visibility.Collapsed;

                    _awakeTick++;
                    if (_awakeTick > 140) _awakeTick = 0;

                    if (_awakeTick >= 46 && _awakeTick <= 75)
                    {
                        SetActiveCrabFrame("AwakeLeft");
                    }
                    else if (_awakeTick >= 96 && _awakeTick <= 125)
                    {
                        SetActiveCrabFrame("AwakeRight");
                    }
                    else
                    {
                        _awakeBobTimer++;
                        if (_awakeBobTimer >= 16)
                        {
                            _awakeBobTimer = 0;
                            _awakeBobToggle = !_awakeBobToggle;
                        }
                        SetActiveCrabFrame(_awakeBobToggle ? "AwakeB" : "AwakeA");
                    }
                    break;

                case "Dead":
                    Canvas.SetLeft(_crabHost, 152);
                    _txtZzz.Visibility = Visibility.Collapsed;
                    SetActiveCrabFrame("Dead");
                    break;
            }
        }

        private static void OnMainTick(object sender, EventArgs e)
        {
            CheckClaudeProcess();
            if (_quitting) return;

            UpdateCrabState();

            _tickCount++;
            if (_tickCount >= 5)
            {
                _tickCount = 0;
                NativeIPResolver.StartFetch();
                UpdateGuardStatus();
            }
        }

        private static void ApplyIPInfoResult(bool success)
        {
            var bc = new BrushConverter();
            if (success && !string.IsNullOrEmpty(NativeIPResolver.IP))
            {
                _state.CurrentIP = NativeIPResolver.IP;
                _state.CountryCode = NativeIPResolver.CountryCode;
                _state.Country = NativeIPResolver.Country;
                _state.ISP = NativeIPResolver.ISP;

                _txtIP.Text = _state.CurrentIP;
                _txtCountry.Text = _state.Country;
                _txtISP.Text = _state.ISP;

                BitmapImage flag = EmbeddedAssets.GetFlagBitmap(_state.CountryCode);
                if (flag != null)
                {
                    _imgFlag.Source = flag;
                    _bdFlag.Visibility = Visibility.Visible;
                }
                else
                {
                    _bdFlag.Visibility = Visibility.Collapsed;
                }

                Log(string.Format("IP verified: {0} ({1}, {2})", _state.CurrentIP, _state.Country, _state.ISP), "NET");

                // Fail-closed enforcement if guard active
                if (_state.IsRunning && !string.IsNullOrEmpty(_state.PinnedIP) && _state.CurrentIP != _state.PinnedIP)
                {
                    CloseProtectedApps("IP mismatch detected");
                }
            }
            else
            {
                _state.CurrentIP = null;
                _state.CountryCode = null;
                _state.Country = null;
                _state.ISP = null;

                _txtIP.Text = GetText("NoConnection");
                _txtCountry.Text = "—";
                _txtISP.Text = "—";
                _bdFlag.Visibility = Visibility.Collapsed;
                Log("Unable to fetch public IP (Offline).", "WARN");
            }

            UpdateStatusIndicator();
            UpdateGuardStatus();
            UpdateCrabState();
        }

        private static void UpdateStatusIndicator()
        {
            var bc = new BrushConverter();
            if (string.IsNullOrEmpty(_state.PinnedIP))
            {
                _statusDot.Fill = (Brush)bc.ConvertFrom("#444466");
                _txtStatusShort.Text = GetText("NoPIN");
                return;
            }
            if (string.IsNullOrEmpty(_state.CurrentIP))
            {
                _statusDot.Fill = (Brush)bc.ConvertFrom("#e05050");
                _txtStatusShort.Text = GetText("Offline");
                return;
            }
            if (_state.CurrentIP == _state.PinnedIP)
            {
                _statusDot.Fill = (Brush)bc.ConvertFrom("#2cb67d");
                _txtStatusShort.Text = GetText("IPOk");
            }
            else
            {
                _statusDot.Fill = (Brush)bc.ConvertFrom("#e05050");
                _txtStatusShort.Text = GetText("IPChanged");
            }
        }

        private static void UpdateGuardStatus()
        {
            var bc = new BrushConverter();
            if (_state.IsRunning)
            {
                _txtGuardDot.Text = "●";
                _txtGuardDot.Foreground = (Brush)bc.ConvertFrom("#2cb67d");
                _txtGuardStatus.Text = GetText("GuardActive");
                _txtProtecting.Text = string.Format(GetText("Protecting"), _config.ProcessName);
                _btnStop.IsEnabled = true;
                _btnStart.IsEnabled = false;
            }
            else
            {
                _txtGuardDot.Text = "○";
                _txtGuardDot.Foreground = (Brush)bc.ConvertFrom("#e05050");
                _txtGuardStatus.Text = GetText("GuardStopped");
                _txtProtecting.Text = "";
                _btnStop.IsEnabled = false;
                _btnStart.IsEnabled = !string.IsNullOrEmpty(_state.PinnedIP);
            }

            _btnClaude.IsEnabled = (
                !string.IsNullOrEmpty(_state.PinnedIP) &&
                !string.IsNullOrEmpty(_state.CurrentIP) &&
                _state.CurrentIP == _state.PinnedIP
            );
        }

        private static void UpdateCrabState()
        {
            var bc = new BrushConverter();

            // 1. IP Mismatch / Unsafe -> Dead
            if (!string.IsNullOrEmpty(_state.PinnedIP) && !string.IsNullOrEmpty(_state.CurrentIP) && _state.CurrentIP != _state.PinnedIP)
            {
                _currentCrabMode = "Dead";
                _txtCrabStatus.Text = GetText("CrabDead");
                _txtCrabStatus.Foreground = (Brush)bc.ConvertFrom("#E05050");

                _imgClaudeLogo.Source = EmbeddedAssets.GetClaudeLogoRed();
                _imgClaudeLogo.Effect = _claudeRedGlow;

                _sinisterLayer.Visibility = Visibility.Visible;
                _dayLayer.Visibility = Visibility.Collapsed;
                _nightLayer.Visibility = Visibility.Collapsed;
                _brokenTrackLine.Visibility = Visibility.Visible;
                _trackLine.Visibility = Visibility.Collapsed;
                _txtZzz.Visibility = Visibility.Collapsed;
                SetActiveCrabFrame("Dead");
                return;
            }

            // Normal matching IP: reset Claude logo to terracotta
            _imgClaudeLogo.Source = EmbeddedAssets.GetClaudeLogo();
            _imgClaudeLogo.Effect = null;
            _brokenTrackLine.Visibility = Visibility.Collapsed;
            _trackLine.Visibility = Visibility.Visible;
            _sinisterLayer.Visibility = Visibility.Collapsed;

            // 2. Guard Stopped -> Sleep
            if (!_state.IsRunning)
            {
                _currentCrabMode = "Sleep";
                _txtCrabStatus.Text = GetText("CrabSleep");
                _txtCrabStatus.Foreground = (Brush)bc.ConvertFrom("#8888aa");

                _nightLayer.Visibility = Visibility.Visible;
                _dayLayer.Visibility = Visibility.Collapsed;
                _txtZzz.Visibility = Visibility.Visible;
                SetActiveCrabFrame("Sleep");
                return;
            }

            // 3. Guard Active -> Day Sky
            _dayLayer.Visibility = Visibility.Visible;
            _nightLayer.Visibility = Visibility.Collapsed;
            _txtZzz.Visibility = Visibility.Collapsed;

            if (_isClaudeRunning)
            {
                if (_currentCrabMode != "Walking")
                {
                    _walkLegTimer = 0;
                    _walkPhase = 0;
                    SetActiveCrabFrame("Walk1");
                }
                _currentCrabMode = "Walking";
                _txtCrabStatus.Text = GetText("CrabWalking");
                _txtCrabStatus.Foreground = (Brush)bc.ConvertFrom("#D97757");
            }
            else
            {
                if (_currentCrabMode != "Awake")
                {
                    _awakeTick = 0;
                    _awakeBobTimer = 0;
                    SetActiveCrabFrame("AwakeA");
                }
                _currentCrabMode = "Awake";
                _txtCrabStatus.Text = GetText("CrabAwake");
                _txtCrabStatus.Foreground = (Brush)bc.ConvertFrom("#2cb67d");
            }
        }

        private static void CheckClaudeProcess()
        {
            string procName = Path.GetFileNameWithoutExtension(_config.ProcessName ?? "Claude");
            bool running = false;
            try
            {
                var list = Process.GetProcessesByName(procName);
                running = (list != null && list.Length > 0);
            }
            catch { }

            bool prev = _isClaudeRunning;
            _isClaudeRunning = running;

            if (prev && !running && _config.AutoExitWithApp && _state.IsRunning)
            {
                Log(string.Format("Protected process {0} exited. Auto-closing ClawGuard.", procName), "GUARD");
                ExitApp();
            }
        }

        private static void CloseProtectedApps(string reason)
        {
            string procName = Path.GetFileNameWithoutExtension(_config.ProcessName ?? "Claude");
            try
            {
                var procs = Process.GetProcessesByName(procName);
                if (procs != null && procs.Length > 0)
                {
                    foreach (var p in procs)
                    {
                        try { p.CloseMainWindow(); } catch { }
                        try { p.Kill(); } catch { }
                    }
                    Log(string.Format("FAIL-CLOSED: Terminated {0} processes ({1})!", procs.Length, reason), "GUARD");
                    if (_notifyIcon != null)
                    {
                        _notifyIcon.ShowBalloonTip(3000, "ClawGuard Alert", string.Format("IP Mismatch! Terminated {0}.", procName), Forms.ToolTipIcon.Error);
                    }
                }
            }
            catch { }
        }

        #endregion

        #region Actions & Controls Handlers

        private static void PinCurrentIP()
        {
            if (string.IsNullOrEmpty(_state.CurrentIP))
            {
                MessageBox.Show(GetText("MsgNoIP"), "ClawGuard", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _state.PinnedIP = _state.CurrentIP;
            _config.PinnedIP = _state.PinnedIP;
            _config.Save();

            _txtPinnedIP.Text = _state.PinnedIP;
            _txtPinnedIP.Foreground = (Brush)new BrushConverter().ConvertFrom("#2cb67d");
            _btnPin.Content = GetText("PinUpdate");

            UpdateStatusIndicator();
            UpdateGuardStatus();
            UpdateCrabState();
            Log("Pinned IP locked to: " + _state.PinnedIP, "PIN");
        }

        private static void StartGuard()
        {
            if (string.IsNullOrEmpty(_state.PinnedIP))
            {
                MessageBox.Show(GetText("MsgPinFirst"), "ClawGuard", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _state.IsRunning = true;
            UpdateGuardStatus();
            UpdateCrabState();
            Log(string.Format("Guard started. Monitoring IPv4 == {0}", _state.PinnedIP), "GUARD");

            // Perform immediate check
            if (!string.IsNullOrEmpty(_state.CurrentIP) && _state.CurrentIP != _state.PinnedIP)
            {
                CloseProtectedApps("Initial start mismatch");
            }
        }

        private static void StopGuard()
        {
            _state.IsRunning = false;
            UpdateGuardStatus();
            UpdateCrabState();
            Log("Guard stopped by user.", "GUARD");
        }

        private static void LaunchClaude()
        {
            if (string.IsNullOrEmpty(_state.PinnedIP) || string.IsNullOrEmpty(_state.CurrentIP) || _state.CurrentIP != _state.PinnedIP)
            {
                MessageBox.Show(GetText("MsgMismatch"), "ClawGuard", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_state.IsRunning)
            {
                StartGuard();
            }

            try
            {
                // Try launching Claude desktop app
                Process.Start("explorer.exe", @"shell:AppsFolder\Claude_pzs8sxrjxfjjc!Claude");
                Log("Launched Claude desktop app.", "APP");
            }
            catch (Exception ex)
            {
                try
                {
                    Process.Start("claude");
                    Log("Launched Claude via PATH.", "APP");
                }
                catch
                {
                    Log("Failed to launch Claude: " + ex.Message, "ERROR");
                }
            }
        }

        #endregion

        #region Drawer & Settings

        private static void OpenDrawer(string mode)
        {
            _currentDrawerMode = mode;
            if (mode == "config")
            {
                _txtDrawerIcon.Text = "⚙";
                _txtDrawerTitle.Text = GetText("CfgTitle");
                _viewConfig.Visibility = Visibility.Visible;
                _viewLog.Visibility = Visibility.Collapsed;

                _inPinnedIP.Text = _config.PinnedIP ?? "";
                _inProcess.Text = _config.ProcessName ?? "Claude";
                _inInterval.Text = _config.CheckInterval.ToString();
                _chkAutoExit.IsChecked = _config.AutoExitWithApp;
                _chkTray.IsChecked = _config.NotifyOnTray;
            }
            else
            {
                _txtDrawerIcon.Text = "📋";
                _txtDrawerTitle.Text = GetText("LogTitle");
                _viewConfig.Visibility = Visibility.Collapsed;
                _viewLog.Visibility = Visibility.Visible;
                _txtLogBody.Text = string.Join(Environment.NewLine, _logEntries.ToArray());
                _txtLogBody.ScrollToEnd();
            }

            _sideDrawer.Visibility = Visibility.Visible;
            _drawerOverlay.Visibility = Visibility.Visible;
        }

        private static void CloseDrawer()
        {
            _sideDrawer.Visibility = Visibility.Collapsed;
            _drawerOverlay.Visibility = Visibility.Collapsed;
            _currentDrawerMode = null;
        }

        private static void SaveConfigFromDrawer()
        {
            string newPinned = _inPinnedIP.Text.Trim();
            _config.PinnedIP = string.IsNullOrEmpty(newPinned) ? null : newPinned;
            _state.PinnedIP = _config.PinnedIP;

            string newProc = _inProcess.Text.Trim();
            if (!string.IsNullOrEmpty(newProc)) _config.ProcessName = newProc;

            int ival;
            if (int.TryParse(_inInterval.Text.Trim(), out ival) && ival > 0) _config.CheckInterval = ival;

            _config.AutoExitWithApp = (_chkAutoExit.IsChecked == true);
            _config.NotifyOnTray = (_chkTray.IsChecked == true);
            _config.Save();

            _txtPinnedIP.Text = _state.PinnedIP ?? GetText("NotSet");
            _txtPinnedIP.Foreground = (Brush)new BrushConverter().ConvertFrom(!string.IsNullOrEmpty(_state.PinnedIP) ? "#2cb67d" : "#e05050");
            _btnPin.Content = string.IsNullOrEmpty(_state.PinnedIP) ? GetText("PinBtn") : GetText("PinUpdate");

            UpdateStatusIndicator();
            UpdateGuardStatus();
            UpdateCrabState();
            CloseDrawer();
            Log("Settings saved successfully.", "CONFIG");
        }

        #endregion

        #region System Tray & Exit

        private static void SetupTray()
        {
            _notifyIcon = new Forms.NotifyIcon();
            _notifyIcon.Icon = EmbeddedAssets.GetAppIcon();
            _notifyIcon.Text = "ClawGuard";
            _notifyIcon.Visible = true;

            var menu = new Forms.ContextMenu();
            var itemShow = new Forms.MenuItem(GetText("TrayShow"), (s, e) => RestoreWindow());
            var itemQuit = new Forms.MenuItem(GetText("TrayQuit"), (s, e) => ExitApp());
            menu.MenuItems.Add(itemShow);
            menu.MenuItems.Add(itemQuit);
            _notifyIcon.ContextMenu = menu;

            _notifyIcon.DoubleClick += (s, e) => RestoreWindow();
            _notifyIcon.Click += (s, e) =>
            {
                var me = e as Forms.MouseEventArgs;
                if (me != null && me.Button == Forms.MouseButtons.Left) RestoreWindow();
            };
        }

        private static void RestoreWindow()
        {
            if (_window != null)
            {
                _window.Show();
                _window.WindowState = WindowState.Normal;
                _window.Activate();
            }
        }

        private static void ExitApp()
        {
            _quitting = true;
            _mainTimer.Stop();
            _crabTimer.Stop();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            if (_singleInstanceMutex != null)
            {
                try { _singleInstanceMutex.ReleaseMutex(); } catch { }
            }
            Application.Current.Shutdown();
        }

        #endregion
    }
}
