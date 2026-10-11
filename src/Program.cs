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
using ColorConverter = System.Windows.Media.ColorConverter;
using Forms = System.Windows.Forms;
using Image = System.Windows.Controls.Image;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using Path = System.IO.Path;

[assembly: AssemblyTitle("ClawGuard")]
[assembly: AssemblyDescription("ClawGuard Fail-Closed VPN Kill Switch for Claude")]
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

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        public const int GWL_EXSTYLE = -20;
        public const long WS_EX_DLGMODALFRAME = 0x00000001L;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_FRAMECHANGED = 0x0020;
        public const uint WM_SETICON = 0x0080;

        public static void EnableDarkTitleBar(Window window)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(window).EnsureHandle();
                if (hwnd != IntPtr.Zero)
                {
                    int trueVal = 1;
                    DwmSetWindowAttribute(hwnd, 20, ref trueVal, sizeof(int));
                    DwmSetWindowAttribute(hwnd, 19, ref trueVal, sizeof(int));
                    int captionColor = 0x001c1414; // BGR #14141c
                    DwmSetWindowAttribute(hwnd, 35, ref captionColor, sizeof(int));
                    RemoveTitleIcon(hwnd);
                }
            }
            catch { }
        }

        public static void RemoveTitleIcon(IntPtr hwnd)
        {
            try
            {
                using (Bitmap bmp = new Bitmap(16, 16))
                {
                    IntPtr hIcon = bmp.GetHicon();
                    SendMessage(hwnd, WM_SETICON, IntPtr.Zero, hIcon);
                }
                if (IntPtr.Size == 8)
                {
                    long style = GetWindowLongPtr64(hwnd, GWL_EXSTYLE).ToInt64();
                    SetWindowLongPtr64(hwnd, GWL_EXSTYLE, new IntPtr(style | WS_EX_DLGMODALFRAME));
                }
                else
                {
                    int style = GetWindowLong32(hwnd, GWL_EXSTYLE);
                    SetWindowLong32(hwnd, GWL_EXSTYLE, (int)(style | WS_EX_DLGMODALFRAME));
                }
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
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
                        HasNewResult = true;
                        IsBusy = false;
                    }
                }
            });
        }

        private static string MatchRegex(string text, string pattern)
        {
            Match m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            return m.Success && m.Groups.Count > 1 ? m.Groups[1].Value : null;
        }
    }

    public class AppConfig
    {
        public string PinnedIP { get; set; }
        public string ProcessName { get; set; }
        public int CheckInterval { get; set; }
        public string Language { get; set; }
        public bool AutoExitWithApp { get; set; }
        public bool TrayNotified { get; set; }

        public AppConfig()
        {
            PinnedIP = null;
            ProcessName = "Claude.exe";
            CheckInterval = 2;
            Language = "RU";
            AutoExitWithApp = false;
            TrayNotified = false;
        }

        public static string GetConfigPath()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string localConfig = Path.Combine(exeDir, "ClawGuard.json");
            string exeName = Process.GetCurrentProcess().ProcessName;
            if (exeName.IndexOf("Portable", StringComparison.OrdinalIgnoreCase) >= 0 || File.Exists(localConfig))
            {
                return localConfig;
            }
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClawGuard");
            if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
            return Path.Combine(appData, "ClawGuard.json");
        }

        public static AppConfig Load()
        {
            string path = GetConfigPath();
            var cfg = new AppConfig();
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    cfg.PinnedIP = ExtractJsonString(json, "PinnedIP");
                    string p = ExtractJsonString(json, "ProcessName");
                    if (!string.IsNullOrEmpty(p)) cfg.ProcessName = p;
                    string lang = ExtractJsonString(json, "Language");
                    if (!string.IsNullOrEmpty(lang)) cfg.Language = lang;
                    string ival = ExtractJsonNumber(json, "CheckInterval");
                    int iv;
                    if (!string.IsNullOrEmpty(ival) && int.TryParse(ival, out iv)) cfg.CheckInterval = iv;
                    cfg.AutoExitWithApp = json.Contains("\"AutoExitWithApp\": true");
                    cfg.TrayNotified = json.Contains("\"TrayNotified\": true");
                }
                catch { }
            }
            return cfg;
        }

        public void Save()
        {
            try
            {
                string path = GetConfigPath();
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine(string.Format("    \"PinnedIP\": {0},", PinnedIP != null ? "\"" + PinnedIP + "\"" : "null"));
                sb.AppendLine(string.Format("    \"ProcessName\": \"{0}\",", ProcessName));
                sb.AppendLine(string.Format("    \"CheckInterval\": {0},", CheckInterval));
                sb.AppendLine(string.Format("    \"Language\": \"{0}\",", Language));
                sb.AppendLine(string.Format("    \"AutoExitWithApp\": {0},", AutoExitWithApp ? "true" : "false"));
                sb.AppendLine(string.Format("    \"TrayNotified\": {0}", TrayNotified ? "true" : "false"));
                sb.AppendLine("}");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static string ExtractJsonString(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string ExtractJsonNumber(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*([0-9]+)");
            return m.Success ? m.Groups[1].Value : null;
        }
    }

    public static class I18n
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Dictionaries = new Dictionary<string, Dictionary<string, string>>
        {
            {
                "EN", new Dictionary<string, string>
                {
                    { "PublicIP", "PUBLIC IP" },
                    { "Country", "COUNTRY" },
                    { "ISP", "PROVIDER" },
                    { "PinnedIP", "PINNED IP" },
                    { "PinBtn", "Pin IP" },
                    { "LaunchBtn", "Launch Claude" },
                    { "StopGuard", "Stop Guard" },
                    { "StartGuard", "Start Guard" },
                    { "Protecting", "protecting" },
                    { "GuardActive", "Guard active" },
                    { "GuardStopped", "Guard stopped" },
                    { "IpMismatch", "IP MISMATCH!" },
                    { "ClaudeRunning", "Claude running" },
                    { "ClaudeNotRunning", "Claude not running" },
                    { "CrabWalking", "Tunnel secure • Guard active" },
                    { "CrabAwake", "Ready • Standing guard" },
                    { "CrabSleep", "Idle • Sleeping" },
                    { "CrabDead", "THREAT DETECTED • Claude terminated" },
                    { "NotSet", "not set" },
                    { "ShortSafe", "SAFE" },
                    { "ShortMismatch", "LEAK!" },
                    { "ShortInactive", "IDLE" },
                    { "TrayKillTitle", "ClawGuard — Kill Switch Triggered!" },
                    { "TrayKillMsg", "IP changed to {0}! Claude terminated to prevent IP leaks." },
                    { "TrayHideTitle", "ClawGuard is active" },
                    { "TrayHideMsg", "App minimized to tray. Double-click or right-click to manage." },
                    { "TrayShow", "Show ClawGuard" },
                    { "TrayLaunch", "Launch Claude" },
                    { "TrayPin", "Pin Current IP" },
                    { "TrayQuit", "Quit" },
                    { "MsgNoIP", "Could not detect current public IP yet." },
                    { "CfgTitle", "Settings" },
                    { "CfgPinnedIP", "Pinned IP:" },
                    { "CfgProcess", "Target Process:" },
                    { "CfgInterval", "Check Interval (s):" },
                    { "CfgAutoExit", "Auto-close with Claude" },
                    { "CfgResetTray", "Notify on minimize to tray" },
                    { "CfgSave", "Save" },
                    { "CfgCancel", "Cancel" },
                    { "LogTitle", "Activity Log" },
                    { "LogCopy", "Copy" },
                    { "LogClear", "Clear" }
                }
            },
            {
                "RU", new Dictionary<string, string>
                {
                    { "PublicIP", "IP-АДРЕС" },
                    { "Country", "СТРАНА" },
                    { "ISP", "ПРОВАЙДЕР" },
                    { "PinnedIP", "ЗАКРЕПЛЁННЫЙ IP" },
                    { "PinBtn", "Закрепить" },
                    { "LaunchBtn", "Запустить Claude" },
                    { "StopGuard", "Остановить" },
                    { "StartGuard", "Включить защиту" },
                    { "Protecting", "защита активна" },
                    { "GuardActive", "Свитчер активен" },
                    { "GuardStopped", "Свитчер остановлен" },
                    { "IpMismatch", "IP НЕ СОВПАДАЕТ!" },
                    { "ClaudeRunning", "Claude запущен" },
                    { "ClaudeNotRunning", "Claude не запущен" },
                    { "CrabWalking", "Туннель защищён • Защита активна" },
                    { "CrabAwake", "На страже • Готов к запуску" },
                    { "CrabSleep", "Спит • Свитчер остановлен" },
                    { "CrabDead", "УГРОЗА • Claude экстренно закрыт" },
                    { "NotSet", "не задан" },
                    { "ShortSafe", "НОРМА" },
                    { "ShortMismatch", "УТЕЧКА!" },
                    { "ShortInactive", "ПАУЗА" },
                    { "TrayKillTitle", "ClawGuard — Сработал Kill Switch!" },
                    { "TrayKillMsg", "IP сменился на {0}! Claude экстренно закрыт для защиты от утечки." },
                    { "TrayHideTitle", "ClawGuard работает в фоне" },
                    { "TrayHideMsg", "Приложение свёрнуто в трей. Двойной клик разворачивает окно." },
                    { "TrayShow", "Открыть ClawGuard" },
                    { "TrayLaunch", "Запустить Claude" },
                    { "TrayPin", "Закрепить текущий IP" },
                    { "TrayQuit", "Выход" },
                    { "MsgNoIP", "Текущий публичный IP ещё не определён." },
                    { "CfgTitle", "Настройки" },
                    { "CfgPinnedIP", "Закреплённый IP:" },
                    { "CfgProcess", "Защищаемый процесс:" },
                    { "CfgInterval", "Интервал проверки (сек):" },
                    { "CfgAutoExit", "Закрывать вместе с Claude" },
                    { "CfgResetTray", "Уведомлять о сворачивании в трей" },
                    { "CfgSave", "Сохранить" },
                    { "CfgCancel", "Отмена" },
                    { "LogTitle", "Журнал событий" },
                    { "LogCopy", "Копировать" },
                    { "LogClear", "Очистить" }
                }
            }
        };

        public static string T(string lang, string key)
        {
            if (string.IsNullOrEmpty(lang)) lang = "RU";
            if (Dictionaries.ContainsKey(lang) && Dictionaries[lang].ContainsKey(key))
                return Dictionaries[lang][key];
            if (Dictionaries["EN"].ContainsKey(key))
                return Dictionaries["EN"][key];
            return key;
        }
    }

    public static class PixelArt
    {
        private static Brush B(string hex)
        {
            return (Brush)new BrushConverter().ConvertFrom(hex);
        }

        public static void DrawPx(Canvas c, int x, int y, Brush brush, double scale)
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
        }

        public static Canvas MakeSun(double scale)
        {
            var c = new Canvas { Width = 8 * scale, Height = 8 * scale, ClipToBounds = false };
            Brush ray = B("#F6C845");
            Brush core = B("#FFE660");

            DrawPx(c, 3, 0, ray, scale); DrawPx(c, 4, 0, ray, scale);
            DrawPx(c, 3, 7, ray, scale); DrawPx(c, 4, 7, ray, scale);
            DrawPx(c, 0, 3, ray, scale); DrawPx(c, 0, 4, ray, scale);
            DrawPx(c, 7, 3, ray, scale); DrawPx(c, 7, 4, ray, scale);
            DrawPx(c, 1, 1, ray, scale); DrawPx(c, 6, 1, ray, scale);
            DrawPx(c, 1, 6, ray, scale); DrawPx(c, 6, 6, ray, scale);
            for (int y = 2; y <= 5; y++)
            {
                for (int x = 2; x <= 5; x++) DrawPx(c, x, y, core, scale);
            }
            DrawPx(c, 3, 1, core, scale); DrawPx(c, 4, 1, core, scale);
            DrawPx(c, 3, 6, core, scale); DrawPx(c, 4, 6, core, scale);
            DrawPx(c, 1, 3, core, scale); DrawPx(c, 1, 4, core, scale);
            DrawPx(c, 6, 3, core, scale); DrawPx(c, 6, 4, core, scale);
            return c;
        }

        public static Canvas MakeMoon(bool isBlood, double scale)
        {
            var c = new Canvas { Width = 9 * scale, Height = 9 * scale, ClipToBounds = false };
            Brush hi = isBlood ? B("#E05050") : B("#F0F2FA");
            Brush mid = isBlood ? B("#A83236") : B("#B4B8D0");
            Brush dk = isBlood ? B("#5A1A1E") : B("#686C88");

            for (int x = 4; x <= 8; x++) DrawPx(c, x, 0, hi, scale);
            DrawPx(c, 2, 1, hi, scale); DrawPx(c, 3, 1, hi, scale); DrawPx(c, 4, 1, mid, scale); DrawPx(c, 8, 1, mid, scale);
            DrawPx(c, 1, 2, hi, scale); DrawPx(c, 2, 2, hi, scale); DrawPx(c, 3, 2, mid, scale); DrawPx(c, 7, 2, dk, scale);
            DrawPx(c, 0, 3, hi, scale); DrawPx(c, 1, 3, hi, scale); DrawPx(c, 2, 3, mid, scale);
            DrawPx(c, 0, 4, hi, scale); DrawPx(c, 1, 4, hi, scale); DrawPx(c, 2, 4, mid, scale);
            DrawPx(c, 0, 5, hi, scale); DrawPx(c, 1, 5, hi, scale); DrawPx(c, 2, 5, mid, scale);
            DrawPx(c, 1, 6, hi, scale); DrawPx(c, 2, 6, hi, scale); DrawPx(c, 3, 6, mid, scale); DrawPx(c, 7, 6, dk, scale);
            DrawPx(c, 2, 7, hi, scale); DrawPx(c, 3, 7, hi, scale); DrawPx(c, 4, 7, mid, scale); DrawPx(c, 8, 7, mid, scale);
            for (int x = 4; x <= 8; x++) DrawPx(c, x, 8, hi, scale);
            return c;
        }

        public static Canvas MakeCloud(string colHex, string hiHex, double scale)
        {
            var c = new Canvas { Width = 18 * scale, Height = 6 * scale, ClipToBounds = false };
            Brush col = B(colHex);
            Brush hi = B(hiHex);

            for (int x = 4; x <= 10; x++) { DrawPx(c, x, 0, hi, scale); DrawPx(c, x, 1, col, scale); }
            for (int x = 2; x <= 14; x++) { DrawPx(c, x, 2, hi, scale); DrawPx(c, x, 3, col, scale); }
            for (int x = 0; x <= 17; x++) { DrawPx(c, x, 4, col, scale); DrawPx(c, x, 5, col, scale); }
            return c;
        }

        public static Canvas MakeCrabFrame(string state, double scale)
        {
            var c = new Canvas { Width = 14 * scale, Height = 11 * scale, ClipToBounds = false };
            Brush b = state == "Dead" ? B("#E05050") : B("#D97757");
            Brush cEyeCutout = B("#0f0f16");
            Brush cWhite = Brushes.White;

            int yBob = state == "AwakeB" ? 1 : 0;

            // Horns / Pincers
            int[] hornX = new int[] { 2, 3, 10, 11 };
            if (state == "AwakeLeft") hornX = new int[] { 1, 2, 9, 10 };
            if (state == "AwakeRight") hornX = new int[] { 3, 4, 11, 12 };
            foreach (int x in hornX)
            {
                DrawPx(c, x, 0 + yBob, b, scale);
                DrawPx(c, x, 1 + yBob, b, scale);
            }

            // Head top
            for (int x = 2; x <= 11; x++) DrawPx(c, x, 2 + yBob, b, scale);

            // Main body
            for (int y = 3; y <= 6; y++)
            {
                for (int x = 0; x <= 13; x++) DrawPx(c, x, y + yBob, b, scale);
            }

            // Body bottom
            for (int x = 2; x <= 11; x++) DrawPx(c, x, 7 + yBob, b, scale);

            // Eyes
            if (state == "Dead")
            {
                DrawPx(c, 3, 3, cWhite, scale); DrawPx(c, 4, 4, cWhite, scale);
                DrawPx(c, 4, 3, cEyeCutout, scale); DrawPx(c, 3, 4, cEyeCutout, scale);
                DrawPx(c, 9, 3, cWhite, scale); DrawPx(c, 10, 4, cWhite, scale);
                DrawPx(c, 10, 3, cEyeCutout, scale); DrawPx(c, 9, 4, cEyeCutout, scale);
            }
            else if (state == "Sleep")
            {
                DrawPx(c, 3, 4, cEyeCutout, scale); DrawPx(c, 4, 4, cEyeCutout, scale);
                DrawPx(c, 9, 4, cEyeCutout, scale); DrawPx(c, 10, 4, cEyeCutout, scale);
            }
            else if (state == "AwakeLeft")
            {
                DrawPx(c, 2, 3 + yBob, cEyeCutout, scale); DrawPx(c, 3, 3 + yBob, cEyeCutout, scale);
                DrawPx(c, 2, 4 + yBob, cEyeCutout, scale); DrawPx(c, 3, 4 + yBob, cEyeCutout, scale);
                DrawPx(c, 8, 3 + yBob, cEyeCutout, scale); DrawPx(c, 9, 3 + yBob, cEyeCutout, scale);
                DrawPx(c, 8, 4 + yBob, cEyeCutout, scale); DrawPx(c, 9, 4 + yBob, cEyeCutout, scale);
            }
            else if (state == "AwakeRight")
            {
                DrawPx(c, 4, 3 + yBob, cEyeCutout, scale); DrawPx(c, 5, 3 + yBob, cEyeCutout, scale);
                DrawPx(c, 4, 4 + yBob, cEyeCutout, scale); DrawPx(c, 5, 4 + yBob, cEyeCutout, scale);
                DrawPx(c, 10, 3 + yBob, cEyeCutout, scale); DrawPx(c, 11, 3 + yBob, cEyeCutout, scale);
                DrawPx(c, 10, 4 + yBob, cEyeCutout, scale); DrawPx(c, 11, 4 + yBob, cEyeCutout, scale);
            }
            else
            {
                DrawPx(c, 3, 3 + yBob, cEyeCutout, scale); DrawPx(c, 4, 3 + yBob, cEyeCutout, scale);
                DrawPx(c, 3, 4 + yBob, cEyeCutout, scale); DrawPx(c, 4, 4 + yBob, cEyeCutout, scale);
                DrawPx(c, 9, 3 + yBob, cEyeCutout, scale); DrawPx(c, 10, 3 + yBob, cEyeCutout, scale);
                DrawPx(c, 9, 4 + yBob, cEyeCutout, scale); DrawPx(c, 10, 4 + yBob, cEyeCutout, scale);
            }

            // Legs
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
                DrawPx(c, x, 8 + yBob, b, scale);
                DrawPx(c, x, 9 + yBob, b, scale);
            }

            return c;
        }
    }

    public class Program
    {
        private static Mutex _appMutex;
        private static Forms.NotifyIcon _notifyIcon;
        private static Window _window;
        private static AppConfig _config;

        private static bool _isClaudeRunning = false;
        private static bool _hasSeenClaude = false;
        private static int _claudeClosedTicks = 0;
        private static string _currentCrabMode = "Sleep";

        private static int _walkLegTimer = 0;
        private static int _walkPhase = 0;
        private static int _awakeBobTimer = 0;
        private static bool _awakeBobToggle = false;
        private static int _awakeTick = 0;
        private static double _zzzOffset = 0.0;
        private static double _zzzDir = 1.0;
        private static int _tickCount = 0;

        private static Dictionary<string, Canvas> _crabFrames = new Dictionary<string, Canvas>();

        // Controls
        private static Canvas _crabHost;
        private static Line _trackLine;
        private static Line _brokenTrackLine;
        private static TextBlock _txtZzz;
        private static Grid _dayLayer;
        private static Grid _nightLayer;
        private static Grid _sinisterLayer;

        private static TextBlock _lblPublicIP, _txtIP;
        private static TextBlock _lblCountry, _txtCountry;
        private static TextBlock _lblISP, _txtISP;
        private static TextBlock _lblPinnedIP, _txtPinnedIP;
        private static TextBlock _txtGuardDot, _txtGuardStatus, _txtProtecting;
        private static TextBlock _txtCrabStatus, _txtStatusShort;
        private static Ellipse _statusDot;
        private static Border _bdFlag;
        private static Image _imgFlag;
        private static Button _btnPin, _btnLaunch, _btnToggleGuard;
        private static Button _btnConfig, _btnLog, _btnLang;
        private static TextBlock _txtLangBtn;
        private static Border _drawerPanel;
        private static TextBlock _txtDrawerTitle;
        private static StackPanel _viewConfig;
        private static Grid _viewLog;
        private static TextBox _inPinnedIP, _inProcess, _inInterval, _txtLogBody;
        private static CheckBox _chkAutoExit, _chkTray;
        private static Image _imgTitleLogo;

        private static List<string> _logEntries = new List<string>();

        [STAThread]
        public static void Main()
        {
            const string mutexName = @"Global\ClawGuard_SingleInstance_Mutex";
            bool createdNew;
            try
            {
                _appMutex = new Mutex(true, mutexName, out createdNew);
            }
            catch
            {
                createdNew = false;
            }

            if (!createdNew)
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

            var app = new Application();
            BuildUI();
            app.Run(_window);
        }

        private static void Log(string msg, string type = "INFO")
        {
            string line = string.Format("[{0:HH:mm:ss}] [{1}] {2}", DateTime.Now, type, msg);
            _logEntries.Add(line);
            if (_logEntries.Count > 300) _logEntries.RemoveAt(0);
            if (_txtLogBody != null)
            {
                _txtLogBody.Text = string.Join(Environment.NewLine, _logEntries.ToArray());
                _txtLogBody.ScrollToEnd();
            }
        }

        private static void BuildUI()
        {
            string xaml = @"<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        Title=""ClawGuard"" Height=""498"" Width=""390""
        WindowStartupLocation=""CenterScreen"" ResizeMode=""CanMinimize""
        Background=""#14141c"" Foreground=""White"" FontFamily=""Segoe UI"">
    <Window.Resources>
        <Style x:Key=""Card"" TargetType=""Border"">
            <Setter Property=""Background"" Value=""#1a1a28""/>
            <Setter Property=""CornerRadius"" Value=""10""/>
            <Setter Property=""Padding"" Value=""14,10""/>
            <Setter Property=""Margin"" Value=""0,0,0,8""/>
            <Setter Property=""BorderBrush"" Value=""#292942""/>
            <Setter Property=""BorderThickness"" Value=""1""/>
        </Style>
        <Style x:Key=""Lbl"" TargetType=""TextBlock"">
            <Setter Property=""FontSize"" Value=""9.5""/>
            <Setter Property=""Foreground"" Value=""#656592""/>
            <Setter Property=""FontWeight"" Value=""SemiBold""/>
            <Setter Property=""Margin"" Value=""0,0,0,2""/>
        </Style>
        <Style x:Key=""Val"" TargetType=""TextBlock"">
            <Setter Property=""FontSize"" Value=""13.5""/>
            <Setter Property=""Foreground"" Value=""#dedef8""/>
            <Setter Property=""FontWeight"" Value=""SemiBold""/>
        </Style>
        <Style x:Key=""Btn"" TargetType=""Button"">
            <Setter Property=""Height"" Value=""34""/>
            <Setter Property=""FontSize"" Value=""12.5""/>
            <Setter Property=""FontWeight"" Value=""SemiBold""/>
            <Setter Property=""Foreground"" Value=""White""/>
            <Setter Property=""BorderThickness"" Value=""0""/>
            <Setter Property=""Cursor"" Value=""Hand""/>
            <Setter Property=""Template"">
                <Setter.Value>
                    <ControlTemplate TargetType=""Button"">
                        <Border x:Name=""bd"" Background=""{TemplateBinding Background}"" CornerRadius=""8"" Padding=""12,0"">
                            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""bd"" Property=""Opacity"" Value=""0.82""/></Trigger>
                            <Trigger Property=""IsEnabled"" Value=""False""><Setter TargetName=""bd"" Property=""Opacity"" Value=""0.35""/></Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <Style x:Key=""HeaderBtn"" TargetType=""Button"">
            <Setter Property=""Height"" Value=""23""/>
            <Setter Property=""FontSize"" Value=""10.5""/>
            <Setter Property=""FontWeight"" Value=""Bold""/>
            <Setter Property=""Foreground"" Value=""#b0b0dc""/>
            <Setter Property=""Background"" Value=""#252538""/>
            <Setter Property=""Cursor"" Value=""Hand""/>
            <Setter Property=""Margin"" Value=""0,0,6,0""/>
            <Setter Property=""Template"">
                <Setter.Value>
                    <ControlTemplate TargetType=""Button"">
                        <Border x:Name=""bdH"" Background=""{TemplateBinding Background}"" CornerRadius=""4"" Padding=""7,2"">
                            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""bdH"" Property=""Background"" Value=""#3a3a58""/></Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Window.Resources>
    <Grid>
        <StackPanel x:Name=""mainPanel"" Margin=""14,2,14,10"">
            <Grid Margin=""0,0,0,8"">
                <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
                <StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center"">
                    <Border Width=""22"" Height=""17"" Margin=""0,0,8,0"" Background=""#281a17"" CornerRadius=""3"" VerticalAlignment=""Center"" BorderBrush=""#503028"" BorderThickness=""1"">
                        <Image x:Name=""imgTitleLogo"" Width=""16"" Height=""13"" Stretch=""Uniform""/>
                    </Border>
                    <TextBlock Text=""ClawGuard"" FontSize=""18"" FontWeight=""Bold"" Foreground=""#D97757"" VerticalAlignment=""Center""/>
                </StackPanel>
                <StackPanel Grid.Column=""1"" Orientation=""Horizontal"" VerticalAlignment=""Center"">
                    <Button x:Name=""btnConfig"" Style=""{StaticResource HeaderBtn}"" Content=""⚙ CFG""/>
                    <Button x:Name=""btnLog"" Style=""{StaticResource HeaderBtn}"" Content=""📋 LOG""/>
                    <Button x:Name=""btnLang"" Style=""{StaticResource HeaderBtn}"" Margin=""0,0,8,0"">
                        <TextBlock x:Name=""txtLangBtn"" Text=""RU"" FontWeight=""Bold"" Foreground=""#e0af85""/>
                    </Button>
                    <StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center"">
                        <Ellipse x:Name=""statusDot"" Width=""8"" Height=""8"" Fill=""#444466"" Margin=""0,0,4,0"" VerticalAlignment=""Center""/>
                        <TextBlock x:Name=""txtStatusShort"" Text=""—"" FontSize=""11"" Foreground=""#606090"" VerticalAlignment=""Center""/>
                    </StackPanel>
                </StackPanel>
            </Grid>

            <Border Style=""{StaticResource Card}"">
                <Grid>
                    <Grid.ColumnDefinitions><ColumnDefinition Width=""135""/><ColumnDefinition Width=""12""/><ColumnDefinition Width=""*""/></Grid.ColumnDefinitions>
                    <StackPanel Grid.Column=""0"" VerticalAlignment=""Center"">
                        <TextBlock x:Name=""lblPublicIP"" Style=""{StaticResource Lbl}"" Text=""IP-АДРЕС""/>
                        <TextBlock x:Name=""txtIP"" Style=""{StaticResource Val}"" Text=""—""/>
                    </StackPanel>
                    <Rectangle Grid.Column=""1"" Width=""1"" Fill=""#282845"" HorizontalAlignment=""Center"" Margin=""0,2""/>
                    <StackPanel Grid.Column=""2"" VerticalAlignment=""Center"" Margin=""2,0,0,0"">
                        <TextBlock x:Name=""lblCountry"" Style=""{StaticResource Lbl}"" Text=""СТРАНА""/>
                        <StackPanel Orientation=""Horizontal"" Margin=""0,1,0,5"" VerticalAlignment=""Center"">
                            <Border x:Name=""bdFlag"" CornerRadius=""2"" Margin=""0,0,6,0"" Visibility=""Collapsed"" VerticalAlignment=""Center"" ClipToBounds=""True"" BorderBrush=""#353555"" BorderThickness=""1"">
                                <Image x:Name=""imgFlag"" Width=""19"" Height=""13"" Stretch=""UniformToFill""/>
                            </Border>
                            <TextBlock x:Name=""txtCountry"" Style=""{StaticResource Val}"" Text=""—"" FontSize=""12.5"" VerticalAlignment=""Center"" TextTrimming=""CharacterEllipsis""/>
                        </StackPanel>
                        <TextBlock x:Name=""lblISP"" Style=""{StaticResource Lbl}"" Text=""ПРОВАЙДЕР""/>
                        <TextBlock x:Name=""txtISP"" Foreground=""#9292bd"" FontSize=""11.5"" Text=""—"" TextTrimming=""CharacterEllipsis""/>
                    </StackPanel>
                </Grid>
            </Border>

            <Border Style=""{StaticResource Card}"">
                <Grid>
                    <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""8""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
                    <StackPanel Grid.Column=""0"" VerticalAlignment=""Center"">
                        <TextBlock x:Name=""lblPinnedIP"" Style=""{StaticResource Lbl}"" Text=""ЗАКРЕПЛЁННЫЙ IP""/>
                        <TextBlock x:Name=""txtPinnedIP"" Style=""{StaticResource Val}"" Text=""не задан"" Foreground=""#e05050"" FontSize=""12.5""/>
                    </StackPanel>
                    <Button x:Name=""btnPin"" Grid.Column=""2"" Style=""{StaticResource Btn}"" Background=""#5a3fbf"" Content=""Закрепить"" MinWidth=""112"" Padding=""12,0""/>
                </Grid>
            </Border>

            <Border Style=""{StaticResource Card}"" Padding=""13,8"">
                <Grid>
                    <Grid.ColumnDefinitions><ColumnDefinition Width=""Auto""/><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
                    <TextBlock x:Name=""txtGuardDot"" Text=""○"" FontSize=""14"" Foreground=""#e05050"" VerticalAlignment=""Center"" Margin=""0,0,7,0""/>
                    <TextBlock x:Name=""txtGuardStatus"" Grid.Column=""1"" Text=""Свитчер остановлен"" Foreground=""#8f8fba"" FontSize=""12"" VerticalAlignment=""Center""/>
                    <TextBlock x:Name=""txtProtecting"" Grid.Column=""2"" Text="""" Foreground=""#505072"" FontSize=""10.5"" VerticalAlignment=""Center""/>
                </Grid>
            </Border>

            <Border Background=""#09090e"" CornerRadius=""10"" BorderBrush=""#222238"" BorderThickness=""1"" Height=""110"" Margin=""0,0,0,10"" ClipToBounds=""True"">
                <Grid>
                    <Grid x:Name=""dayLayer"" Visibility=""Collapsed"">
                        <Canvas x:Name=""sunHost"" Canvas.Left=""290"" Canvas.Top=""10""/>
                        <Canvas x:Name=""dayCloud1"" Canvas.Left=""25"" Canvas.Top=""15""/>
                        <Canvas x:Name=""dayCloud2"" Canvas.Left=""200"" Canvas.Top=""35""/>
                    </Grid>
                    <Grid x:Name=""nightLayer"" Visibility=""Visible"">
                        <Canvas x:Name=""moonHost"" Canvas.Left=""275"" Canvas.Top=""12""/>
                        <Canvas x:Name=""nightCloud1"" Canvas.Left=""20"" Canvas.Top=""20""/>
                        <Canvas x:Name=""nightCloud2"" Canvas.Left=""180"" Canvas.Top=""36""/>
                    </Grid>
                    <Grid x:Name=""sinisterLayer"" Visibility=""Collapsed"">
                        <Canvas x:Name=""bloodMoonHost"" Canvas.Left=""275"" Canvas.Top=""12""/>
                        <Canvas x:Name=""stormCloud"" Canvas.Left=""60"" Canvas.Top=""22""/>
                    </Grid>

                    <Canvas>
                        <Line x:Name=""trackLine"" X1=""-20"" Y1=""48"" X2=""380"" Y2=""48"" Stroke=""#3a3a58"" StrokeThickness=""2"" StrokeDashArray=""6,6""/>
                        <Line x:Name=""brokenTrackLine"" X1=""-20"" Y1=""48"" X2=""380"" Y2=""48"" Stroke=""#e05050"" StrokeThickness=""2"" Visibility=""Collapsed""/>
                        <Canvas x:Name=""crabHost"" Canvas.Left=""152"" Canvas.Top=""24""/>
                        <TextBlock x:Name=""txtZzz"" Text=""Z z z"" FontSize=""11"" FontWeight=""Bold"" Foreground=""#6a6a94"" Canvas.Left=""194"" Canvas.Top=""18""/>
                    </Canvas>
                    <TextBlock x:Name=""txtCrabStatus"" Text=""Спит • Свитчер остановлен"" FontSize=""11"" Foreground=""#8888aa"" HorizontalAlignment=""Center"" VerticalAlignment=""Bottom"" Margin=""0,0,0,8""/>
                </Grid>
            </Border>

            <Grid>
                <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""10""/><ColumnDefinition Width=""*""/></Grid.ColumnDefinitions>
                <Button x:Name=""btnLaunch"" Grid.Column=""0"" Style=""{StaticResource Btn}"" Background=""#1d9766"" Content=""Запустить Claude"" IsEnabled=""False""/>
                <Button x:Name=""btnToggleGuard"" Grid.Column=""2"" Style=""{StaticResource Btn}"" Background=""#3a3a55"" Content=""Включить защиту""/>
            </Grid>
        </StackPanel>

        <!-- Slide-Out Drawer -->
        <Border x:Name=""drawerPanel"" Background=""#141420"" BorderBrush=""#2d2d48"" BorderThickness=""1,0,0,0""
                HorizontalAlignment=""Right"" Width=""320"" Margin=""0,0,-330,0"" Visibility=""Collapsed"">
            <Grid Margin=""16,14"">
                <Grid.RowDefinitions><RowDefinition Height=""Auto""/><RowDefinition Height=""*""/></Grid.RowDefinitions>
                <Grid Grid.Row=""0"" Margin=""0,0,0,14"">
                    <TextBlock x:Name=""txtDrawerTitle"" Text=""Настройки"" FontSize=""15"" FontWeight=""Bold"" Foreground=""#dedef8""/>
                    <Button x:Name=""btnCloseDrawer"" Content=""✕"" HorizontalAlignment=""Right"" Width=""24"" Height=""24""
                            Background=""Transparent"" Foreground=""#8585aa"" BorderThickness=""0"" Cursor=""Hand""/>
                </Grid>
                <Grid Grid.Row=""1"">
                    <StackPanel x:Name=""viewConfig"" Visibility=""Visible"">
                        <TextBlock Text=""Закреплённый IP:"" FontSize=""10"" Foreground=""#8585aa"" Margin=""0,0,0,3""/>
                        <Grid Margin=""0,0,0,10"">
                            <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""6""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
                            <TextBox x:Name=""inPinnedIP"" Background=""#1c1c28"" Foreground=""White"" BorderBrush=""#353550"" Height=""28"" VerticalContentAlignment=""Center""/>
                            <Button x:Name=""btnCfgUseCurrent"" Grid.Column=""2"" Content=""Текущий"" Height=""28"" Padding=""8,0"" Background=""#353554"" Foreground=""#e0e0ff"" BorderThickness=""0"" Cursor=""Hand""/>
                        </Grid>
                        <TextBlock Text=""Защищаемый процесс:"" FontSize=""10"" Foreground=""#8585aa"" Margin=""0,0,0,3""/>
                        <TextBox x:Name=""inProcess"" Background=""#1c1c28"" Foreground=""White"" BorderBrush=""#353550"" Height=""28"" Margin=""0,0,0,10"" VerticalContentAlignment=""Center""/>
                        <TextBlock Text=""Интервал проверки (сек):"" FontSize=""10"" Foreground=""#8585aa"" Margin=""0,0,0,3""/>
                        <TextBox x:Name=""inInterval"" Background=""#1c1c28"" Foreground=""White"" BorderBrush=""#353550"" Height=""28"" Margin=""0,0,0,14"" VerticalContentAlignment=""Center""/>
                        <CheckBox x:Name=""chkAutoExit"" Content=""Закрывать вместе с Claude"" Foreground=""#dedef8"" Margin=""0,0,0,10""/>
                        <CheckBox x:Name=""chkTray"" Content=""Уведомлять о сворачивании в трей"" Foreground=""#dedef8"" Margin=""0,0,0,16""/>
                        <StackPanel Orientation=""Horizontal"" HorizontalAlignment=""Right"">
                            <Button x:Name=""btnCfgCancel"" Content=""Отмена"" Width=""80"" Height=""28"" Background=""#252538"" Foreground=""White"" BorderThickness=""0"" Margin=""0,0,8,0"" Cursor=""Hand""/>
                            <Button x:Name=""btnCfgSave"" Content=""Сохранить"" Width=""95"" Height=""28"" Background=""#1d9766"" Foreground=""White"" BorderThickness=""0"" FontWeight=""SemiBold"" Cursor=""Hand""/>
                        </StackPanel>
                    </StackPanel>

                    <Grid x:Name=""viewLog"" Visibility=""Collapsed"">
                        <Grid.RowDefinitions><RowDefinition Height=""*""/><RowDefinition Height=""Auto""/></Grid.RowDefinitions>
                        <TextBox x:Name=""txtLogBody"" Grid.Row=""0"" Background=""#101018"" Foreground=""#85e0a3"" BorderBrush=""#252538""
                                 FontFamily=""Consolas"" FontSize=""10.5"" IsReadOnly=""True"" TextWrapping=""Wrap"" VerticalScrollBarVisibility=""Auto""/>
                        <StackPanel Grid.Row=""1"" Orientation=""Horizontal"" HorizontalAlignment=""Right"" Margin=""0,10,0,0"">
                            <Button x:Name=""btnCopyLog"" Content=""Копировать"" Width=""85"" Height=""26"" Background=""#252538"" Foreground=""White"" BorderThickness=""0"" Margin=""0,0,8,0"" Cursor=""Hand""/>
                            <Button x:Name=""btnClearLog"" Content=""Очистить"" Width=""75"" Height=""26"" Background=""#252538"" Foreground=""White"" BorderThickness=""0"" Cursor=""Hand""/>
                        </StackPanel>
                    </Grid>
                </Grid>
            </Grid>
        </Border>
    </Grid>
</Window>";

            _window = (Window)XamlReader.Parse(xaml);
            Win32.EnableDarkTitleBar(_window);

            // Bind Elements
            _crabHost = (Canvas)_window.FindName("crabHost");
            _trackLine = (Line)_window.FindName("trackLine");
            _brokenTrackLine = (Line)_window.FindName("brokenTrackLine");
            _txtZzz = (TextBlock)_window.FindName("txtZzz");
            _dayLayer = (Grid)_window.FindName("dayLayer");
            _nightLayer = (Grid)_window.FindName("nightLayer");
            _sinisterLayer = (Grid)_window.FindName("sinisterLayer");

            _lblPublicIP = (TextBlock)_window.FindName("lblPublicIP");
            _txtIP = (TextBlock)_window.FindName("txtIP");
            _lblCountry = (TextBlock)_window.FindName("lblCountry");
            _txtCountry = (TextBlock)_window.FindName("txtCountry");
            _lblISP = (TextBlock)_window.FindName("lblISP");
            _txtISP = (TextBlock)_window.FindName("txtISP");
            _lblPinnedIP = (TextBlock)_window.FindName("lblPinnedIP");
            _txtPinnedIP = (TextBlock)_window.FindName("txtPinnedIP");
            _txtGuardDot = (TextBlock)_window.FindName("txtGuardDot");
            _txtGuardStatus = (TextBlock)_window.FindName("txtGuardStatus");
            _txtProtecting = (TextBlock)_window.FindName("txtProtecting");
            _txtCrabStatus = (TextBlock)_window.FindName("txtCrabStatus");
            _txtStatusShort = (TextBlock)_window.FindName("txtStatusShort");
            _statusDot = (Ellipse)_window.FindName("statusDot");
            _bdFlag = (Border)_window.FindName("bdFlag");
            _imgFlag = (Image)_window.FindName("imgFlag");

            _btnPin = (Button)_window.FindName("btnPin");
            _btnLaunch = (Button)_window.FindName("btnLaunch");
            _btnToggleGuard = (Button)_window.FindName("btnToggleGuard");
            _btnConfig = (Button)_window.FindName("btnConfig");
            _btnLog = (Button)_window.FindName("btnLog");
            _btnLang = (Button)_window.FindName("btnLang");
            _txtLangBtn = (TextBlock)_window.FindName("txtLangBtn");

            _drawerPanel = (Border)_window.FindName("drawerPanel");
            _txtDrawerTitle = (TextBlock)_window.FindName("txtDrawerTitle");
            _viewConfig = (StackPanel)_window.FindName("viewConfig");
            _viewLog = (Grid)_window.FindName("viewLog");
            _inPinnedIP = (TextBox)_window.FindName("inPinnedIP");
            _inProcess = (TextBox)_window.FindName("inProcess");
            _inInterval = (TextBox)_window.FindName("inInterval");
            _txtLogBody = (TextBox)_window.FindName("txtLogBody");
            _chkAutoExit = (CheckBox)_window.FindName("chkAutoExit");
            _chkTray = (CheckBox)_window.FindName("chkTray");
            _imgTitleLogo = (Image)_window.FindName("imgTitleLogo");

            var btnCloseDrawer = (Button)_window.FindName("btnCloseDrawer");
            var btnCfgUseCurrent = (Button)_window.FindName("btnCfgUseCurrent");
            var btnCfgCancel = (Button)_window.FindName("btnCfgCancel");
            var btnCfgSave = (Button)_window.FindName("btnCfgSave");
            var btnCopyLog = (Button)_window.FindName("btnCopyLog");
            var btnClearLog = (Button)_window.FindName("btnClearLog");

            // Attach Pixel Scenery
            ((Canvas)_window.FindName("sunHost")).Children.Add(PixelArt.MakeSun(1.8));
            ((Canvas)_window.FindName("moonHost")).Children.Add(PixelArt.MakeMoon(false, 1.8));
            ((Canvas)_window.FindName("bloodMoonHost")).Children.Add(PixelArt.MakeMoon(true, 1.8));
            ((Canvas)_window.FindName("dayCloud1")).Children.Add(PixelArt.MakeCloud("#454868", "#8588AC", 1.8));
            ((Canvas)_window.FindName("dayCloud2")).Children.Add(PixelArt.MakeCloud("#454868", "#8588AC", 1.8));
            ((Canvas)_window.FindName("nightCloud1")).Children.Add(PixelArt.MakeCloud("#28283c", "#3a3a54", 1.8));
            ((Canvas)_window.FindName("nightCloud2")).Children.Add(PixelArt.MakeCloud("#28283c", "#3a3a54", 1.8));
            ((Canvas)_window.FindName("stormCloud")).Children.Add(PixelArt.MakeCloud("#222230", "#5c222a", 1.8));

            // Attach Crab Frames
            string[] frameKeys = new string[] { "Sleep", "AwakeA", "AwakeB", "AwakeLeft", "AwakeRight", "Walk1", "Walk2", "Walk3", "Walk4", "Dead" };
            foreach (string k in frameKeys)
            {
                var frame = PixelArt.MakeCrabFrame(k, 2.4);
                frame.Visibility = Visibility.Collapsed;
                _crabFrames[k] = frame;
                _crabHost.Children.Add(frame);
            }
            _crabFrames["Sleep"].Visibility = Visibility.Visible;

            // Load Icon from local file or executable
            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ClawGuard.ico");
                if (File.Exists(icoPath))
                {
                    _window.Icon = new BitmapImage(new Uri(icoPath));
                    _imgTitleLogo.Source = _window.Icon;
                }
            }
            catch { }

            // Attach System Tray
            AttachTray();

            // Setup Events
            _btnPin.Click += (s, e) => PinCurrentIP();
            _btnLaunch.Click += (s, e) => LaunchClaude();
            _btnToggleGuard.Click += (s, e) => ToggleGuard();
            _btnLang.Click += (s, e) => ToggleLanguage();
            _btnConfig.Click += (s, e) => OpenDrawer(true);
            _btnLog.Click += (s, e) => OpenDrawer(false);
            btnCloseDrawer.Click += (s, e) => CloseDrawer();
            btnCfgCancel.Click += (s, e) => CloseDrawer();
            btnCfgUseCurrent.Click += (s, e) => { if (!string.IsNullOrEmpty(NativeIPResolver.IP)) _inPinnedIP.Text = NativeIPResolver.IP; };
            btnCfgSave.Click += (s, e) => SaveDrawerConfig();
            btnCopyLog.Click += (s, e) => { if (!string.IsNullOrEmpty(_txtLogBody.Text)) Clipboard.SetText(_txtLogBody.Text); };
            btnClearLog.Click += (s, e) => { _logEntries.Clear(); _txtLogBody.Text = ""; };

            _window.Closing += (s, e) =>
            {
                e.Cancel = true;
                _window.Hide();
                if (!_config.TrayNotified)
                {
                    _config.TrayNotified = true;
                    _config.Save();
                    _notifyIcon.ShowBalloonTip(3000, I18n.T(_config.Language, "TrayHideTitle"), I18n.T(_config.Language, "TrayHideMsg"), Forms.ToolTipIcon.Info);
                }
            };

            // Timers
            var crabTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
            crabTimer.Tick += OnCrabTick;
            crabTimer.Start();

            var mainTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            mainTimer.Tick += OnMainTick;
            mainTimer.Start();

            ApplyLanguage();
            if (!string.IsNullOrEmpty(_config.PinnedIP))
            {
                _txtPinnedIP.Text = _config.PinnedIP;
                _txtPinnedIP.Foreground = (Brush)new BrushConverter().ConvertFrom("#2cb67d");
            }

            Log("ClawGuard 2.5 initialized in native C#.", "INIT");
            NativeIPResolver.StartFetch();
            UpdateGuardStatus();
        }

        private static void SetActiveCrabFrame(string frameKey)
        {
            foreach (var kv in _crabFrames)
            {
                kv.Value.Visibility = kv.Key == frameKey ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static void OnCrabTick(object sender, EventArgs e)
        {
            if (NativeIPResolver.HasNewResult)
            {
                NativeIPResolver.HasNewResult = false;
                ApplyIPResult();
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
            UpdateCrabState();

            _tickCount++;
            if (_tickCount >= 5)
            {
                _tickCount = 0;
                NativeIPResolver.StartFetch();
                UpdateGuardStatus();
            }
        }

        private static void CheckClaudeProcess()
        {
            string procName = Path.GetFileNameWithoutExtension(_config.ProcessName);
            if (string.IsNullOrEmpty(procName)) procName = "Claude";

            Process[] procs = Process.GetProcessesByName(procName);
            if (procs.Length > 0)
            {
                _isClaudeRunning = true;
                _hasSeenClaude = true;
                _claudeClosedTicks = 0;

                // Fail-Closed Kill Switch check!
                if (!string.IsNullOrEmpty(_config.PinnedIP) && !string.IsNullOrEmpty(NativeIPResolver.IP) &&
                    NativeIPResolver.IP != _config.PinnedIP)
                {
                    Log(string.Format("KILL-SWITCH TRIGGERED: IP {0} does not match pinned {1}!", NativeIPResolver.IP, _config.PinnedIP), "KILL");
                    foreach (var p in procs)
                    {
                        try { p.Kill(); } catch { }
                    }
                    _isClaudeRunning = false;
                    _notifyIcon.ShowBalloonTip(4000, I18n.T(_config.Language, "TrayKillTitle"),
                        string.Format(I18n.T(_config.Language, "TrayKillMsg"), NativeIPResolver.IP), Forms.ToolTipIcon.Error);
                }
            }
            else
            {
                _isClaudeRunning = false;
                if (_hasSeenClaude)
                {
                    if (_config.AutoExitWithApp)
                    {
                        _claudeClosedTicks++;
                        if (_claudeClosedTicks >= 2)
                        {
                            Log("Claude closed and AutoExitWithApp enabled. Exiting.", "EXIT");
                            ExitApp();
                        }
                    }
                    else
                    {
                        _hasSeenClaude = false;
                        _claudeClosedTicks = 0;
                    }
                }
            }
        }

        private static void UpdateCrabState()
        {
            // 1. Mismatch -> Dead
            if (!string.IsNullOrEmpty(_config.PinnedIP) && !string.IsNullOrEmpty(NativeIPResolver.IP) &&
                NativeIPResolver.IP != _config.PinnedIP)
            {
                _currentCrabMode = "Dead";
                _txtCrabStatus.Text = I18n.T(_config.Language, "CrabDead");
                _txtCrabStatus.Foreground = (Brush)new BrushConverter().ConvertFrom("#E05050");
                _sinisterLayer.Visibility = Visibility.Visible;
                _dayLayer.Visibility = Visibility.Collapsed;
                _nightLayer.Visibility = Visibility.Collapsed;
                _brokenTrackLine.Visibility = Visibility.Visible;
                _trackLine.Visibility = Visibility.Collapsed;
                _txtZzz.Visibility = Visibility.Collapsed;
                SetActiveCrabFrame("Dead");
                return;
            }

            _brokenTrackLine.Visibility = Visibility.Collapsed;
            _trackLine.Visibility = Visibility.Visible;
            _sinisterLayer.Visibility = Visibility.Collapsed;

            // 2. Stopped -> Sleep
            if (_btnToggleGuard.Content.ToString() == I18n.T(_config.Language, "StartGuard"))
            {
                _currentCrabMode = "Sleep";
                _txtCrabStatus.Text = I18n.T(_config.Language, "CrabSleep");
                _txtCrabStatus.Foreground = (Brush)new BrushConverter().ConvertFrom("#8888aa");
                _nightLayer.Visibility = Visibility.Visible;
                _dayLayer.Visibility = Visibility.Collapsed;
                _txtZzz.Visibility = Visibility.Visible;
                SetActiveCrabFrame("Sleep");
                return;
            }

            // 3. Active -> Day
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
                _txtCrabStatus.Text = I18n.T(_config.Language, "CrabWalking");
                _txtCrabStatus.Foreground = (Brush)new BrushConverter().ConvertFrom("#D97757");
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
                _txtCrabStatus.Text = I18n.T(_config.Language, "CrabAwake");
                _txtCrabStatus.Foreground = (Brush)new BrushConverter().ConvertFrom("#2cb67d");
            }
        }

        private static void ApplyIPResult()
        {
            if (NativeIPResolver.Success && !string.IsNullOrEmpty(NativeIPResolver.IP))
            {
                _txtIP.Text = NativeIPResolver.IP;
                _txtCountry.Text = NativeIPResolver.Country ?? "—";
                _txtISP.Text = NativeIPResolver.ISP ?? "—";

                if (!string.IsNullOrEmpty(NativeIPResolver.CountryCode))
                {
                    LoadFlag(NativeIPResolver.CountryCode);
                }
            }
            UpdateGuardStatus();
        }

        private static void LoadFlag(string code)
        {
            try
            {
                string url = string.Format("https://flagcdn.com/w40/{0}.png", code.ToLower());
                var bi = new BitmapImage(new Uri(url));
                _imgFlag.Source = bi;
                _bdFlag.Visibility = Visibility.Visible;
            }
            catch { }
        }

        private static void UpdateGuardStatus()
        {
            bool isGuardOn = _btnToggleGuard.Content.ToString() == I18n.T(_config.Language, "StopGuard");
            string curIP = NativeIPResolver.IP;
            string pinIP = _config.PinnedIP;

            if (string.IsNullOrEmpty(pinIP))
            {
                _txtPinnedIP.Text = I18n.T(_config.Language, "NotSet");
                _txtPinnedIP.Foreground = (Brush)new BrushConverter().ConvertFrom("#e05050");
            }
            else
            {
                _txtPinnedIP.Text = pinIP;
            }

            if (!string.IsNullOrEmpty(pinIP) && !string.IsNullOrEmpty(curIP) && curIP != pinIP)
            {
                _txtGuardDot.Text = "●";
                _txtGuardDot.Foreground = (Brush)new BrushConverter().ConvertFrom("#e05050");
                _txtGuardStatus.Text = I18n.T(_config.Language, "IpMismatch");
                _txtGuardStatus.Foreground = (Brush)new BrushConverter().ConvertFrom("#e05050");
                _statusDot.Fill = (Brush)new BrushConverter().ConvertFrom("#e05050");
                _txtStatusShort.Text = I18n.T(_config.Language, "ShortMismatch");
                _txtStatusShort.Foreground = (Brush)new BrushConverter().ConvertFrom("#e05050");
                _btnLaunch.IsEnabled = false;
            }
            else if (isGuardOn)
            {
                _txtGuardDot.Text = "●";
                _txtGuardDot.Foreground = (Brush)new BrushConverter().ConvertFrom("#2cb67d");
                _txtGuardStatus.Text = I18n.T(_config.Language, "GuardActive");
                _txtGuardStatus.Foreground = (Brush)new BrushConverter().ConvertFrom("#2cb67d");
                _statusDot.Fill = (Brush)new BrushConverter().ConvertFrom("#2cb67d");
                _txtStatusShort.Text = I18n.T(_config.Language, "ShortSafe");
                _txtStatusShort.Foreground = (Brush)new BrushConverter().ConvertFrom("#2cb67d");
                _btnLaunch.IsEnabled = (!string.IsNullOrEmpty(pinIP) && curIP == pinIP);
            }
            else
            {
                _txtGuardDot.Text = "○";
                _txtGuardDot.Foreground = (Brush)new BrushConverter().ConvertFrom("#e05050");
                _txtGuardStatus.Text = I18n.T(_config.Language, "GuardStopped");
                _txtGuardStatus.Foreground = (Brush)new BrushConverter().ConvertFrom("#8f8fba");
                _statusDot.Fill = (Brush)new BrushConverter().ConvertFrom("#444466");
                _txtStatusShort.Text = I18n.T(_config.Language, "ShortInactive");
                _txtStatusShort.Foreground = (Brush)new BrushConverter().ConvertFrom("#606090");
                _btnLaunch.IsEnabled = false;
            }
        }

        private static void PinCurrentIP()
        {
            if (string.IsNullOrEmpty(NativeIPResolver.IP))
            {
                MessageBox.Show(I18n.T(_config.Language, "MsgNoIP"), "ClawGuard", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _config.PinnedIP = NativeIPResolver.IP;
            _config.Save();
            _txtPinnedIP.Text = _config.PinnedIP;
            _txtPinnedIP.Foreground = (Brush)new BrushConverter().ConvertFrom("#2cb67d");
            Log(string.Format("Pinned IP set to {0}", _config.PinnedIP), "CONFIG");
            UpdateGuardStatus();
        }

        private static void ToggleGuard()
        {
            bool isGuardOn = _btnToggleGuard.Content.ToString() == I18n.T(_config.Language, "StopGuard");
            if (isGuardOn)
            {
                _btnToggleGuard.Content = I18n.T(_config.Language, "StartGuard");
                _btnToggleGuard.Background = (Brush)new BrushConverter().ConvertFrom("#3a3a55");
            }
            else
            {
                _btnToggleGuard.Content = I18n.T(_config.Language, "StopGuard");
                _btnToggleGuard.Background = (Brush)new BrushConverter().ConvertFrom("#5a3fbf");
            }
            UpdateGuardStatus();
            UpdateCrabState();
        }

        private static void LaunchClaude()
        {
            string proc = _config.ProcessName;
            try
            {
                Process.Start(proc);
                Log(string.Format("Launched process: {0}", proc), "LAUNCH");
            }
            catch (Exception ex)
            {
                Log(string.Format("Failed to launch {0}: {1}", proc, ex.Message), "ERROR");
                MessageBox.Show(string.Format("Could not launch {0}: {1}", proc, ex.Message), "ClawGuard", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void ToggleLanguage()
        {
            _config.Language = _config.Language == "RU" ? "EN" : "RU";
            _config.Save();
            ApplyLanguage();
            UpdateGuardStatus();
            UpdateCrabState();
        }

        private static void ApplyLanguage()
        {
            string lang = _config.Language;
            _txtLangBtn.Text = lang;
            _lblPublicIP.Text = I18n.T(lang, "PublicIP");
            _lblCountry.Text = I18n.T(lang, "Country");
            _lblISP.Text = I18n.T(lang, "ISP");
            _lblPinnedIP.Text = I18n.T(lang, "PinnedIP");
            _btnPin.Content = I18n.T(lang, "PinBtn");
            _btnLaunch.Content = I18n.T(lang, "LaunchBtn");
            _btnToggleGuard.Content = I18n.T(lang, "StartGuard");
        }

        private static void OpenDrawer(bool isConfig)
        {
            _drawerPanel.Visibility = Visibility.Visible;
            _txtDrawerTitle.Text = I18n.T(_config.Language, isConfig ? "CfgTitle" : "LogTitle");
            _viewConfig.Visibility = isConfig ? Visibility.Visible : Visibility.Collapsed;
            _viewLog.Visibility = isConfig ? Visibility.Collapsed : Visibility.Visible;

            if (isConfig)
            {
                _inPinnedIP.Text = _config.PinnedIP ?? "";
                _inProcess.Text = _config.ProcessName ?? "Claude.exe";
                _inInterval.Text = _config.CheckInterval.ToString();
                _chkAutoExit.IsChecked = _config.AutoExitWithApp;
                _chkTray.IsChecked = _config.TrayNotified;
            }

            _drawerPanel.Margin = new Thickness(0);
            _drawerPanel.Visibility = Visibility.Visible;
        }

        private static void CloseDrawer()
        {
            _drawerPanel.Visibility = Visibility.Collapsed;
            _drawerPanel.Margin = new Thickness(0, 0, -330, 0);
        }

        private static void SaveDrawerConfig()
        {
            _config.PinnedIP = string.IsNullOrEmpty(_inPinnedIP.Text.Trim()) ? null : _inPinnedIP.Text.Trim();
            _config.ProcessName = string.IsNullOrEmpty(_inProcess.Text.Trim()) ? "Claude.exe" : _inProcess.Text.Trim();
            int ival;
            if (int.TryParse(_inInterval.Text, out ival) && ival >= 1) _config.CheckInterval = ival;
            _config.AutoExitWithApp = _chkAutoExit.IsChecked == true;
            _config.TrayNotified = _chkTray.IsChecked == true;
            _config.Save();
            Log("Configuration updated from settings drawer.", "CONFIG");
            UpdateGuardStatus();
            CloseDrawer();
        }

        private static void AttachTray()
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Text = "ClawGuard — Fail-Closed Kill Switch",
                Visible = true
            };

            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ClawGuard.ico");
                if (File.Exists(icoPath))
                {
                    _notifyIcon.Icon = new Icon(icoPath);
                }
                else
                {
                    _notifyIcon.Icon = SystemIcons.Shield;
                }
            }
            catch
            {
                _notifyIcon.Icon = SystemIcons.Shield;
            }

            _notifyIcon.DoubleClick += (s, e) =>
            {
                _window.Show();
                _window.WindowState = WindowState.Normal;
                _window.Activate();
            };

            var cm = new Forms.ContextMenu();
            cm.MenuItems.Add(I18n.T(_config.Language, "TrayShow"), (s, e) =>
            {
                _window.Show();
                _window.WindowState = WindowState.Normal;
                _window.Activate();
            });
            cm.MenuItems.Add(I18n.T(_config.Language, "TrayLaunch"), (s, e) => LaunchClaude());
            cm.MenuItems.Add(I18n.T(_config.Language, "TrayPin"), (s, e) => PinCurrentIP());
            cm.MenuItems.Add("-");
            cm.MenuItems.Add(I18n.T(_config.Language, "TrayQuit"), (s, e) => ExitApp());

            _notifyIcon.ContextMenu = cm;
        }

        private static void ExitApp()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            Environment.Exit(0);
        }
    }
}

