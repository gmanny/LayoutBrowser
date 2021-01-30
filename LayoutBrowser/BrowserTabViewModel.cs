using System;
using System.IO;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MvvmHelpers;
using WpfAppCommon;

namespace LayoutBrowser
{
    public interface IBrowserTabViewModelFactory
    {
        public BrowserTabViewModel ForModel(LayoutWindowTab model);
    }

    public class BrowserTabViewModel : ObservableObject
    {
        private readonly LayoutWindowTab model;
        private readonly ILogger logger;

        private readonly string profile;

        private readonly CoreWebView2CreationProperties creationArgs;

        private readonly ICommand refreshBtnCommand;
        private readonly ICommand goBtnCommand;
        
        private Uri browserSource;
        private string url;
        private bool isNavigating;
        private string refreshButtonText = "↻";
        private WebView2 webView;
        private string title;
        private double zoomFactor;

        public BrowserTabViewModel(LayoutWindowTab model, ILogger logger)
        {
            this.model = model;
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
            browserSource = new Uri(model.url);
        }

        public ICommand RefreshBtnCommand => refreshBtnCommand;
        public ICommand GoBtnCommand => goBtnCommand;

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

            webView.CoreWebView2.Navigate(url);
        }

        public CoreWebView2CreationProperties CreationProperties => creationArgs;

        public Uri BrowserSource
        {
            get => browserSource;
            set
            {
                SetProperty(ref browserSource, value);

                logger.LogDebug($"Source changed to {value}");

                Url = value.ToString();
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

        public bool IsNavigating
        {
            get => isNavigating;
            set
            {
                SetProperty(ref isNavigating, value);

                RefreshButtonText = value ? "✕" : "↻";
            }
        }

        public string Title
        {
            get => title;
            set => SetProperty(ref title, value);
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

            Title = wv.CoreWebView2.DocumentTitle;
            wv.CoreWebView2.DocumentTitleChanged += OnTitleChanged;
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