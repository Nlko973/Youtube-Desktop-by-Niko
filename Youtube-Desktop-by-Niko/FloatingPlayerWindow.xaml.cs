using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace YoutubeDesktopByNiko
{
    public partial class FloatingPlayerWindow : Window
    {
        private readonly CoreWebView2Environment webViewEnvironment;
        private bool isBrowserReady;

        public FloatingPlayerWindow(CoreWebView2Environment webViewEnvironment)
        {
            InitializeComponent();

            this.webViewEnvironment = webViewEnvironment;
        }

        public async Task NavigateAsync(string url)
        {
            if (!isBrowserReady)
            {
                await FloatingBrowser.EnsureCoreWebView2Async(webViewEnvironment);
                await FloatingBrowser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(GetVideoOnlyScript());
                FloatingBrowser.NavigationCompleted += async (_, __) =>
                {
                    await InjectVideoOnlyScript();
                };
                isBrowserReady = true;
            }

            FloatingBrowser.Source = new Uri(url);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                return;
            }

            base.OnKeyDown(e);
        }

        private async Task InjectVideoOnlyScript()
        {
            try
            {
                await FloatingBrowser.ExecuteScriptAsync(GetVideoOnlyScript());
            }
            catch
            {
            }
        }

        private string GetVideoOnlyScript()
        {
            return @"
(() => {
    if (window.__ytDesktopVideoOnlyInstalled)
        return;

    window.__ytDesktopVideoOnlyInstalled = true;

    const style = document.createElement('style');
    style.textContent = `
        html,
        body {
            margin: 0 !important;
            padding: 0 !important;
            width: 100vw !important;
            height: 100vh !important;
            overflow: hidden !important;
            background: #000 !important;
        }

        body > *:not(video):not(.html5-video-player):not(#movie_player) {
            visibility: hidden !important;
        }

        ytd-app,
        ytm-app,
        #page-manager,
        ytd-watch-flexy,
        #columns,
        #primary,
        #primary-inner,
        #player,
        #player-container,
        #movie_player,
        .html5-video-player,
        .html5-video-container {
            visibility: visible !important;
            display: block !important;
            position: fixed !important;
            inset: 0 !important;
            width: 100vw !important;
            height: 100vh !important;
            max-width: none !important;
            max-height: none !important;
            margin: 0 !important;
            padding: 0 !important;
            background: #000 !important;
            z-index: 2147483647 !important;
        }

        video {
            visibility: visible !important;
            position: fixed !important;
            inset: 0 !important;
            width: 100vw !important;
            height: 100vh !important;
            object-fit: contain !important;
            background: #000 !important;
            z-index: 2147483647 !important;
        }
    `;
    document.documentElement.appendChild(style);

    const fitVideo = () => {
        const video = document.querySelector('video');

        if (!video)
            return;

        video.controls = true;
        video.play().catch(() => {});
    };

    fitVideo();
    setInterval(fitVideo, 1000);
})();
";
        }
    }
}
