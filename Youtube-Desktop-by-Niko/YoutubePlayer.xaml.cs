using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
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
        // CLOSE
        // =====================================

        protected override async void OnClosed(EventArgs e)
        {
            await SaveState();

            base.OnClosed(e);
        }
    }
}