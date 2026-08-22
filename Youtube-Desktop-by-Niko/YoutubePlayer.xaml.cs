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
        private string profilePath = string.Empty;
        private readonly string stateFile = "appstate.json";

        private CoreWebView2Environment? webViewEnvironment;
        private DispatcherTimer? saveTimer;
        private FloatingPlayerWindow? floatingPlayerWindow;

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
        private const int HOTKEY_PICTURE_IN_PICTURE = 973;
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_P = 0x50;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool RegisterHotKey(
            IntPtr hWnd,
            int id,
            uint fsModifiers,
            uint vk
        );

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(
            IntPtr hWnd,
            int id
        );

        public YoutubePlayer()
        {
            InitializeComponent();

            SourceInitialized += YoutubePlayer_SourceInitialized;
            InitBrowser();
        }

        private void YoutubePlayer_SourceInitialized(object? sender, EventArgs e)
        {
            IntPtr handle =
                new WindowInteropHelper(this).Handle;

            HwndSource? source =
                HwndSource.FromHwnd(handle);

            source?.AddHook(WndProc);
            RegisterHotKey(handle, HOTKEY_PICTURE_IN_PICTURE, MOD_CONTROL, VK_P);
        }

        private IntPtr WndProc(
            IntPtr hwnd,
            int msg,
            IntPtr wParam,
            IntPtr lParam,
            ref bool handled
        )
        {
            if (msg == WM_HOTKEY
                && wParam.ToInt32() == HOTKEY_PICTURE_IN_PICTURE)
            {
                _ = TogglePictureInPicture();
                handled = true;
            }

            return IntPtr.Zero;
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

            webViewEnvironment = await CoreWebView2Environment.CreateAsync(
                null,
                profilePath,
                options
            );

            await Browser.EnsureCoreWebView2Async(webViewEnvironment);

            Browser.NavigationCompleted += async (_, __) =>
            {
                await InjectPictureInPictureScript();
            };

            Browser.CoreWebView2.DOMContentLoaded += async (_, __) =>
            {
                await InjectPictureInPictureScript();
            };

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

                    string? url =
                        doc.RootElement
                        .GetProperty("url")
                        .GetString();

                    if (string.IsNullOrWhiteSpace(url))
                        throw new InvalidDataException();

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

            await Browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(GetPictureInPictureScript());
            await InjectPictureInPictureScript();
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
        // PICTURE-IN-PICTURE
        // =====================================

        private async Task TogglePictureInPicture()
        {
            try
            {
                await InjectPictureInPictureScript();
                string result =
                    await Browser.ExecuteScriptAsync("window.__ytDesktopTogglePictureInPicture ? window.__ytDesktopTogglePictureInPicture() : false;");

                if (result == "true")
                    return;
            }
            catch
            {
            }

            await OpenFallbackFloatingPlayer();
        }

        private async Task InjectPictureInPictureScript()
        {
            try
            {
                if (Browser.CoreWebView2 == null)
                    return;

                await Browser.ExecuteScriptAsync(GetPictureInPictureScript());
            }
            catch
            {
            }
        }

        private string GetPictureInPictureScript()
        {
            return @"
(() => {
    if (window.__ytDesktopPictureInPictureInstalled)
        return;

    window.__ytDesktopPictureInPictureInstalled = true;

    const buttonId = 'yt-desktop-pip-button';

    window.__ytDesktopTogglePictureInPicture = async () => {
        const video = document.querySelector('video');

        if (!video || !document.pictureInPictureEnabled)
            return false;

        try {
            if (document.pictureInPictureElement) {
                await document.exitPictureInPicture();
                return true;
            }

            if (video.readyState < 1)
                await video.play().catch(() => {});

            await video.requestPictureInPicture();
            return true;
        } catch {
            return false;
        }
    };

    const ensureButton = () => {
        if (!location.hostname.includes('youtube.com'))
            return;

        let button = document.getElementById(buttonId);

        if (!button) {
            button = document.createElement('button');
            button.id = buttonId;
            button.type = 'button';
            button.title = 'Картинка в картинке';
            button.textContent = '⧉';
            button.addEventListener('click', async event => {
                event.preventDefault();
                event.stopPropagation();
                const opened = await window.__ytDesktopTogglePictureInPicture();

                if (!opened && window.chrome && window.chrome.webview)
                    window.chrome.webview.postMessage('{""type"":""pipFallback""}');
            });

            Object.assign(button.style, {
                position: 'fixed',
                right: '18px',
                top: '76px',
                width: '42px',
                height: '42px',
                zIndex: '2147483647',
                border: '1px solid rgba(255,255,255,.45)',
                borderRadius: '21px',
                background: 'rgba(20,20,20,.78)',
                color: '#fff',
                font: '700 22px Arial, sans-serif',
                lineHeight: '38px',
                cursor: 'pointer',
                boxShadow: '0 4px 16px rgba(0,0,0,.28)',
                padding: '0'
            });

            document.documentElement.appendChild(button);
        }

        const hasVideo = !!document.querySelector('video');
        button.style.display = hasVideo ? 'block' : 'none';
    };

    document.addEventListener('keydown', event => {
        if (event.ctrlKey && !event.shiftKey && !event.altKey && event.code === 'KeyP') {
            event.preventDefault();
            event.stopPropagation();
            window.__ytDesktopTogglePictureInPicture();
        }
    }, true);

    ensureButton();
    setInterval(ensureButton, 1000);
    new MutationObserver(ensureButton).observe(document.documentElement, {
        childList: true,
        subtree: true
    });
})();
";
        }

        private async Task OpenFallbackFloatingPlayer()
        {
            CoreWebView2Environment? environment =
                webViewEnvironment;

            if (Browser.Source == null || environment == null)
                return;

            string currentUrl =
                Browser.Source.ToString();

            if (!currentUrl.Contains("youtube.com")
                && !currentUrl.Contains("youtu.be"))
            {
                MessageBox.Show(
                    "Откройте видео на YouTube, чтобы запустить плавающее окно.",
                    "Плавающий плеер",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                return;
            }

            double currentTime =
                await GetCurrentVideoTime();

            string floatingUrl =
                BuildUrlWithTime(currentUrl, currentTime);

            if (floatingPlayerWindow == null || !floatingPlayerWindow.IsLoaded)
            {
                floatingPlayerWindow =
                    new FloatingPlayerWindow(environment)
                    {
                        Owner = this
                    };

                floatingPlayerWindow.Closed += (_, __) =>
                {
                    floatingPlayerWindow = null;
                };

                floatingPlayerWindow.Show();
            }
            else
            {
                floatingPlayerWindow.Show();
                floatingPlayerWindow.Activate();
            }

            await floatingPlayerWindow.NavigateAsync(floatingUrl);
        }

        private async Task<double> GetCurrentVideoTime()
        {
            try
            {
                string script = @"
(() => {

    let v = document.querySelector('video');

    if(!v)
        return 0;

    return Number(v.currentTime || 0);

})();
";

                string result =
                    await Browser.ExecuteScriptAsync(script);

                result =
                    result.Replace("\"", "");

                if (double.TryParse(
                    result,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double currentTime))
                {
                    return currentTime;
                }
            }
            catch
            {
            }

            return 0;
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

        private void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
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
                else if (message.Contains("\"type\":\"pipFallback\""))
                {
                    _ = OpenFallbackFloatingPlayer();
                }
            }
            catch
            {
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.P
                && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                _ = TogglePictureInPicture();
                e.Handled = true;
                return;
            }

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
            IntPtr handle =
                new WindowInteropHelper(this).Handle;

            UnregisterHotKey(handle, HOTKEY_PICTURE_IN_PICTURE);

            await SaveState();

            base.OnClosed(e);
        }
    }
}
