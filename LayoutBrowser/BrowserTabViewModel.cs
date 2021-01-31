using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Web;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MonitorCommon;
using MvvmHelpers;
using WpfAppCommon;

namespace LayoutBrowser
{
    public interface IBrowserTabViewModelFactory
    {
        public BrowserTabViewModel ForModel(LayoutWindowTab model, LayoutBrowserWindowViewModel parentWindow);
    }

    public class BrowserTabViewModel : ObservableObject
    {
        private readonly LayoutWindowTab model;
        private readonly LayoutBrowserWindowViewModel parentWindow;
        private readonly ILogger logger;

        private readonly string profile;

        private readonly CoreWebView2CreationProperties creationArgs;

        private readonly ICommand refreshBtnCommand;
        private readonly ICommand goBtnCommand;
        
        private Uri browserSource;
        private string url;
        private bool isNavigating;
        private string refreshButtonText = "↻", refreshButtonHint = "Refresh (F5)";
        private WebView2 webView;
        private string title;
        private double zoomFactor;

        public BrowserTabViewModel(LayoutWindowTab model, LayoutBrowserWindowViewModel parentWindow, ILogger logger)
        {
            this.model = model;
            this.parentWindow = parentWindow;
            this.logger = logger;

            profile = model.profile;
            zoomFactor = model.zoomFactor;
            title = model.title;

            refreshBtnCommand = new WindowCommand(ExecuteRefresh);
            goBtnCommand = new WindowCommand(ExecuteGo);

            creationArgs = new CoreWebView2CreationProperties
            {
                UserDataFolder = Path.Combine("Profiles", profile)
            };

            url = model.url;
            browserSource = model.url.IsNullOrEmpty() ? null : new Uri(model.url);
        }

        public ICommand RefreshBtnCommand => refreshBtnCommand;
        public ICommand GoBtnCommand => goBtnCommand;

        public string Profile => profile;

        public LayoutBrowserWindowViewModel ParentWindow => parentWindow;

        public LayoutWindowTab ToModel() => new LayoutWindowTab
        {
            url = browserSource.ToString(),
            title = title,
            profile = profile,
            zoomFactor = zoomFactor
        };

        private void ExecuteRefresh()
        {
            if (isNavigating)
            {
                webView.Stop();
            }
            else
            {
                webView.Reload();
            }
        }

        private async void ExecuteGo()
        {
            await webView.EnsureCoreWebView2Async();

            logger.LogDebug($"Navigating to {url}");

            try
            {
                webView.CoreWebView2.Navigate(url);
            }
            catch (ArgumentException)
            {
                string searchUrl = "https://duckduckgo.com/?q=" + HttpUtility.UrlEncode(url);

                webView.CoreWebView2.Navigate(searchUrl);
            }

            webView.Focus();
        }

        public CoreWebView2CreationProperties CreationProperties => creationArgs;

        public Uri BrowserSource
        {
            get => browserSource;
            set
            {
                SetProperty(ref browserSource, value);

                logger.LogDebug($"Source changed to {value}");

                if (value.ToString() == "about:blank")
                {
                    Url = "";
                }
                else
                {
                    Url = value.ToString();
                }
            }
        }

        public string Url
        {
            get => url;
            set => SetProperty(ref url, value);
        }

        public string RefreshButtonText
        {
            get => refreshButtonText;
            set => SetProperty(ref refreshButtonText, value);
        }

        public string RefreshButtonHint
        {
            get => refreshButtonHint;
            set => SetProperty(ref refreshButtonHint, value);
        }

        public bool IsNavigating
        {
            get => isNavigating;
            set
            {
                SetProperty(ref isNavigating, value);

                RefreshButtonText = value ? "✕" : "↻";
                RefreshButtonHint = value ? "Stop loading (Esc)" : "Refresh (F5)";
            }
        }

        public string Title
        {
            get => title;
            set
            {
                if (value.IsNullOrEmpty())
                {
                    return;
                }

                SetProperty(ref title, value);
            }
        }

        public double ZoomFactor
        {
            get => zoomFactor;
            set => SetProperty(ref zoomFactor, value);
        }

        public WebView2 WebView
        {
            get => webView;
            set
            {
                if (webView != null)
                {
                    throw new InvalidOperationException("Web view is intended to be set only once at init");
                }

                SetProperty(ref webView, value);

                PlugIntoWebView(value);
            }
        }

        private async void PlugIntoWebView(WebView2 wv)
        {
            await wv.EnsureCoreWebView2Async();

            logger.LogDebug($"WebView2 core initialized");

            OnControlInitialized();

            Title = wv.CoreWebView2.DocumentTitle;
            wv.CoreWebView2.DocumentTitleChanged += OnTitleChanged;

            wv.CoreWebView2.WindowCloseRequested += OnCloseRequested;
            wv.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
        }

        public event Action<BrowserTabViewModel, CoreWebView2NewWindowRequestedEventArgs> NewWindowRequested;

        private void OnNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            NewWindowRequested?.Invoke(this, e);
        }

        public event Action<BrowserTabViewModel> CloseRequested;

        private void OnCloseRequested(object sender, object e)
        {
            CloseRequested?.Invoke(this);
        }

        public event Action<BrowserTabViewModel> ControlInitialized;

        private void OnControlInitialized()
        {
            ControlInitialized?.Invoke(this);
        }

        private void OnTitleChanged(object sender, object e)
        {
            Title = webView.CoreWebView2.DocumentTitle;
        }

        public void NavigationStarted(CoreWebView2NavigationStartingEventArgs e)
        {
            IsNavigating = true;
        }

        public void NavigationCompleted(CoreWebView2NavigationCompletedEventArgs e)
        {
            IsNavigating = false;
        }
    }
}