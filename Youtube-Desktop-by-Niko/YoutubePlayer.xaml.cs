using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

// Made by Niko973
// https://github.com/Nlko973
// https://t.me/niko_973
// Also known as Niko973 or niko_973 or Nlko973 

namespace YoutubeDesktopByNiko
{
    public partial class YoutubePlayer : Window
    {
        private string profilePath;
        private string stateFile = "appstate.json";

        private DispatcherTimer saveTimer;

        // Для возврата из полноэкранного режима
        private bool isVideoFullscreen = false;
        private WindowStyle previousWindowStyle;
        private ResizeMode previousResizeMode;
        private bool previousTopmost;
        private double previousLeft;
        private double previousTop;
        private double previousWidth;
        private double previousHeight;
        private WindowState previousWindowState;

        // WinAPI для полноэкранного режима
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(
            IntPtr hMonitor, ref MONITORINFO lpMonitorInfo);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(
            IntPtr hwnd, uint dwFlags);

        [System.Runtime.InteropServices.StructLayout(
            System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [System.Runtime.InteropServices.StructLayout(
            System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        public YoutubePlayer()
        {
            InitializeComponent();

            InitBrowser();
        }

        private async void InitBrowser()
        {
            // =====================================
            // PROFILE
            // =====================================

            profilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YouTubeDesktopProfile"
            );

            // =====================================
            // ENABLE EXTENSIONS
            // =====================================

            var options = new CoreWebView2EnvironmentOptions
            {
                AreBrowserExtensionsEnabled = true
            };

            var env = await CoreWebView2Environment.CreateAsync(
                null,
                profilePath,
                options
            );

            await Browser.EnsureCoreWebView2Async(env);

            // =====================================
            // LOAD UBLOCK
            // =====================================

            try
            {
                string ublockPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Extensions",
                    "uBlockOrigin"
                );

                if (Directory.Exists(ublockPath))
                {
                    await Browser.CoreWebView2.Profile
                        .AddBrowserExtensionAsync(ublockPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "uBlock load error:\n\n" + ex.Message
                );
            }

            // =====================================
            // LOAD SPONSORBLOCK
            // =====================================

            try
            {
                string sponsorBlockPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Extensions",
                    "SponsorBlock"
                );

                if (Directory.Exists(sponsorBlockPath))
                {
                    await Browser.CoreWebView2.Profile
                        .AddBrowserExtensionAsync(sponsorBlockPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "SponsorBlock load error:\n\n" + ex.Message
                );
            }

            // =====================================
            // LOAD LAST PAGE
            // =====================================

            if (File.Exists(stateFile))
            {
                try
                {
                    string json =
                        File.ReadAllText(stateFile);

                    using JsonDocument doc =
                        JsonDocument.Parse(json);

                    string url =
                        doc.RootElement
                        .GetProperty("url")
                        .GetString();

                    double savedTime =
                        doc.RootElement
                        .GetProperty("time")
                        .GetDouble();

                    string finalUrl =
                        BuildUrlWithTime(url, savedTime);

                    Browser.Source = new Uri(finalUrl);
                }
                catch
                {
                    Browser.Source =
                        new Uri("https://m.youtube.com");
                }
            }
            else
            {
                Browser.Source =
                    new Uri("https://m.youtube.com");
            }

            // =====================================
            // AUTOSAVE
            // =====================================

            saveTimer = new DispatcherTimer();

            saveTimer.Interval =
                TimeSpan.FromSeconds(3);

            saveTimer.Tick += async (_, __) =>
            {
                await SaveState();
            };

            saveTimer.Start();

            // =====================================
            // FULLSCREEN DETECTION
            // =====================================

            Browser.WebMessageReceived += WebMessageReceived;

            // Внедряем скрипт для отслеживания полноэкранного режима
            string fullscreenScript = @"
document.addEventListener('fullscreenchange', () => {
    const isFullscreen = !!document.fullscreenElement;
    window.chrome.webview.postMessage('{""type"":""fullscreen"",""isFullscreen"":' + isFullscreen + '}');
});
";

            await Browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(fullscreenScript);
        }

        // =====================================
        // BUILD URL WITH TIME
        // =====================================

        private string BuildUrlWithTime(
            string url,
            double seconds
        )
        {
            try
            {
                if (seconds < 3)
                    return url;

                UriBuilder builder =
                    new UriBuilder(url);

                var query =
                    HttpUtility.ParseQueryString(
                        builder.Query
                    );

                // Удаляем старый t=
                query.Remove("t");

                query["t"] =
                    ((int)seconds).ToString() + "s";

                builder.Query =
                    query.ToString();

                return builder.ToString();
            }
            catch
            {
                return url;
            }
        }

        // =====================================
        // SAVE STATE
        // =====================================

        private async Task SaveState()
        {
            try
            {
                if (Browser.Source == null)
                    return;

                string currentUrl =
                    Browser.Source.ToString();

                // Сохраняем только страницы видео
                if (!currentUrl.Contains("watch"))
                    return;

                double currentTime = 0;

                try
                {
                    string script = @"
(() => {

    let v = document.querySelector('video');

    if(!v)
        return 0;

    if(v.readyState < 2)
        return 0;

    if(v.duration <= 0)
        return 0;

    return Number(v.currentTime);

})();
";

                    string result =
                        await Browser.ExecuteScriptAsync(script);

                    result =
                        result.Replace("\"", "");

                    double.TryParse(
                        result,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out currentTime
                    );
                }
                catch
                {
                    currentTime = 0;
                }

                // Удаляем t= перед сохранением
                string cleanUrl =
                    RemoveTimeParameter(currentUrl);

                var state = new
                {
                    url = cleanUrl,
                    time = currentTime
                };

                string json =
                    JsonSerializer.Serialize(
                        state,
                        new JsonSerializerOptions
                        {
                            WriteIndented = true
                        }
                    );

                File.WriteAllText(stateFile, json);
            }
            catch
            {
            }
        }

        // =====================================
        // REMOVE t= FROM URL
        // =====================================

        private string RemoveTimeParameter(string url)
        {
            try
            {
                UriBuilder builder =
                    new UriBuilder(url);

                var query =
                    HttpUtility.ParseQueryString(
                        builder.Query
                    );

                query.Remove("t");

                builder.Query =
                    query.ToString();

                return builder.ToString();
            }
            catch
            {
                return url;
            }
        }

        // =====================================
        // FULLSCREEN — ENTER / EXIT
        // =====================================

        private void EnterFullscreen()
        {
            if (isVideoFullscreen)
                return;

            // Сохраняем текущие настройки окна
            previousWindowStyle = WindowStyle;
            previousResizeMode = ResizeMode;
            previousTopmost = Topmost;
            previousLeft = Left;
            previousTop = Top;
            previousWidth = Width;
            previousHeight = Height;
            previousWindowState = WindowState;

            // Убираем рамку и заголовок окна
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;

            // Сбрасываем WindowState, чтобы Left/Top/Width/Height работали
            WindowState = WindowState.Normal;

            // Получаем полный размер монитора через WinAPI (включая под панель задач)
            var hwnd = new WindowInteropHelper(this).Handle;
            var hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(monitorInfo);
            GetMonitorInfo(hMonitor, ref monitorInfo);

            // Растягиваем окно на весь экран
            Left = monitorInfo.rcMonitor.Left;
            Top = monitorInfo.rcMonitor.Top;
            Width = monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left;
            Height = monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top;

            // Закрепляем поверх других окон
            Topmost = true;

            isVideoFullscreen = true;
        }

        private void ExitFullscreen()
        {
            if (!isVideoFullscreen)
                return;

            // Возвращаем настройки окна
            Topmost = false;
            WindowStyle = previousWindowStyle;
            ResizeMode = previousResizeMode;

            // Восстанавливаем позицию и размер
            Left = previousLeft;
            Top = previousTop;
            Width = previousWidth;
            Height = previousHeight;

            WindowState = previousWindowState;

            isVideoFullscreen = false;
        }

        private void WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string message = e.TryGetWebMessageAsString();

                if (message.Contains("\"type\":\"fullscreen\""))
                {
                    bool isFullscreen = message.Contains("\"isFullscreen\":true");

                    if (isFullscreen)
                        EnterFullscreen();
                    else
                        ExitFullscreen();
                }
            }
            catch
            {
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Esc — выход из полноэкранного режима
            if (e.Key == System.Windows.Input.Key.Escape
                && isVideoFullscreen)
            {
                // Вызываем выход из полноэкранного режима в JS
                Browser.ExecuteScriptAsync("if(document.fullscreenElement) document.exitFullscreen();");
                ExitFullscreen();
            }
        }

        // =====================================
        // CLOSE
        // =====================================

        protected override async void OnClosed(EventArgs e)
        {
            await SaveState();

            base.OnClosed(e);
        }
    }
}