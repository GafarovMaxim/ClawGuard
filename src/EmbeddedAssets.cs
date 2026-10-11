using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClawGuard
{
    public static class EmbeddedAssets
    {
        private static readonly Dictionary<string, string> EmbeddedFlags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "US", "iVBORw0KGgoAAAANSUhEUgAAACgAAAAVBAMAAADGNLEtAAAALVBMVEX////GU3LZjKGzGUINM2PsxtAYPWotT3hed5enma46WoBPa46zGkMkR3Jrgp+6ntXWAAAAiklEQVQY02Nwc0lJcQMSxghgw+CRVdaWkVXWooQEGFxaPLxKWjxcGJCBn1fJExAWRAJglUuAKlEEPdyXgA1A0e62LTu9bFt2CopF3iCh9LItKE5yX5tVe31tVgmq9pL0NLcy95RQJMAAdiUQo2ifCQUoKrECY0xgw6CEBWDXLogFYBfECrBahM1JAItMTcEiMDpsAAAAAElFTkSuQmCC" },
            { "RU", "iVBORw0KGgoAAAANSUhEUgAAACgAAAAbAgMAAABzfiX9AAAACVBMVEUAOabVKx7///95ANL1AAAAFUlEQVQY02NYBQcMtGLSAYTCAa2YAJQLWaccQRfwAAAAAElFTkSuQmCC" }
        };

        private static readonly Dictionary<string, BitmapImage> _cache = new Dictionary<string, BitmapImage>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new object();

        public static BitmapImage GetClaudeLogo()
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ClawGuard.claude_logo.png"))
                {
                    if (stream != null)
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = stream;
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                }
            }
            catch { }
            return null;
        }

        public static BitmapImage GetClaudeLogoRed()
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ClawGuard.claude_logo_red.png"))
                {
                    if (stream != null)
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = stream;
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                }
            }
            catch { }
            return null;
        }

        public static Icon GetAppIcon()
        {
            try
            {
                string procPath = Process.GetCurrentProcess().MainModule.FileName;
                Icon assoc = Icon.ExtractAssociatedIcon(procPath);
                if (assoc != null) return assoc;
            }
            catch { }
            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ClawGuard.ico");
                if (File.Exists(icoPath)) return new Icon(icoPath);
            }
            catch { }
            return SystemIcons.Application;
        }

        public static ImageSource GetAppIconSource()
        {
            try
            {
                using (Icon ico = GetAppIcon())
                {
                    if (ico != null)
                    {
                        return Imaging.CreateBitmapSourceFromHIcon(
                            ico.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                    }
                }
            }
            catch { }
            return null;
        }

        public static BitmapImage GetFlagBitmap(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length != 2) return null;
            string cc = code.ToUpperInvariant();

            lock (_lock)
            {
                if (_cache.ContainsKey(cc)) return _cache[cc];
            }

            if (EmbeddedFlags.ContainsKey(cc))
            {
                BitmapImage bi = Base64ToBitmapImage(EmbeddedFlags[cc]);
                if (bi != null)
                {
                    lock (_lock) { _cache[cc] = bi; }
                    return bi;
                }
            }

            try
            {
                using (var wc = new TimeoutWebClient(3000))
                {
                    wc.Headers.Add("User-Agent", "ClawGuard/2.5");
                    byte[] bytes = wc.DownloadData("https://flagcdn.com/w40/" + cc.ToLowerInvariant() + ".png");
                    using (MemoryStream ms = new MemoryStream(bytes))
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.StreamSource = ms;
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();
                        lock (_lock) { _cache[cc] = bi; }
                        return bi;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public static BitmapImage Base64ToBitmapImage(string base64)
        {
            try
            {
                if (string.IsNullOrEmpty(base64)) return null;
                byte[] bytes = Convert.FromBase64String(base64);
                using (var ms = new MemoryStream(bytes))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}