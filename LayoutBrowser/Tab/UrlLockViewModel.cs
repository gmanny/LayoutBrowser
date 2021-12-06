using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Threading;
using LanguageExt;
using LayoutBrowser.Layout;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MonitorCommon;
using MvvmHelpers;

namespace LayoutBrowser.Tab
{
    public interface IUrlLockViewModelFactory
    {
        public UrlLockViewModel ForTab(LayoutWindowTab model);
    }

    public class UrlLockViewModel : ObservableObject
    {
        private readonly ILogger logger;
        
        private Uri browserSource;
        private string url;
        private bool browserSourceExposed;
        private bool isNavigating;
        private string refreshButtonText = "↻", refreshButtonHint = "Refresh (F5)";

        private TaskCompletionSource<Unit> refreshComplete;

        private bool lockUrl;
        private string lockedUrl;
        
        private WebView2 webView;

        public UrlLockViewModel(LayoutWindowTab model, ILogger logger)
        {
            this.logger = logger;

            lockUrl = model.lockUrl;
            if (lockUrl)
            {
                lockedUrl = model.url;
            }
            url = model.url;
            browserSource = model.url.IsNullOrEmpty() ? null : new Uri(model.url);
        }

        public async Task PlugIntoWebView(WebView2 wv, WebView2MessagingService messenger)
        {
            webView = wv;

            wv.NavigationStarting += OnNavigationStarted;
            wv.NavigationCompleted += OnNavigationCompleted;
        }

        public void AfterInit()
        {
            ExposeBrowserSource();
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            IsNavigating = false;
        }

        private void OnNavigationStarted(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            IsNavigating = true;
        }

        public Uri InternalBrowserSource => browserSource;

        public Uri BrowserSource
        {
            get => browserSourceExposed ? browserSource : null;
            set
            {
                if (!browserSourceExposed)
                {
                    OnPropertyChanged();
                    return;
                }

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

        public bool LockUrl
        {
            get => lockUrl;
            set => SetProperty(ref lockUrl, value);
        }

        public string LockedUrl => lockUrl ? lockedUrl : null;

        public string Url
        {
            get => url;
            set => SetProperty(ref url, value);
        }

        private void ExposeBrowserSource()
        {
            browserSourceExposed = true;

            OnPropertyChanged(nameof(BrowserSource));
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
                bool stopped = isNavigating && !value;

                SetProperty(ref isNavigating, value);

                RefreshButtonText = value ? "✕" : "↻";
                RefreshButtonHint = value ? "Stop loading (Esc)" : "Refresh (F5)";

                if (stopped)
                {
                    refreshComplete?.TrySetResult(Unit.Default);
                }
            }
        }

        public async Task OnAutoRefresh()
        {
            if (webView == null)
            {
                return;
            }

            TaskCompletionSource<Unit> newComplete = new TaskCompletionSource<Unit>();
            TaskCompletionSource<Unit> oldComplete = Interlocked.Exchange(ref refreshComplete, newComplete);
            oldComplete?.TrySetResult(Unit.Default);

            await webView.Dispatcher.BeginInvoke(Refresh, DispatcherPriority.Background);
            
            await newComplete.Task;
        }

        public async void ExecuteGo()
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

        public void HandleRefreshStopButtonPress()
        {
            if (isNavigating)
            {
                webView.Stop();
            }
            else
            {
                Refresh();
            }
        }

        public event Action PreRefresh;

        public async void Refresh()
        {
            PreRefresh?.Invoke();

            await webView.EnsureCoreWebView2Async();

            webView.Reload();
        }
    }
}