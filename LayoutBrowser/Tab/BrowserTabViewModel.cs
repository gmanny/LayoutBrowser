using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Input;
using System.Windows.Threading;
using LanguageExt;
using LayoutBrowser.Layout;
using LayoutBrowser.Window;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MonitorCommon;
using MvvmHelpers;
using WpfAppCommon;

namespace LayoutBrowser.Tab
{
    public interface IBrowserTabViewModelFactory
    {
        public BrowserTabViewModel ForModel(LayoutWindowTab model, LayoutBrowserWindowViewModel parentWindow);
    }

    public class BrowserTabViewModel : ObservableObject, IDisposable
    {
        private readonly LayoutBrowserWindowViewModel parentWindow;
        private readonly IWebView2MessagingServiceFactory messengerFactory;
        private readonly ILogger logger;
        
        private readonly ProfileItem profile;
        private readonly ProfileListViewModel profileList;
        private readonly AutoRefreshSettingsViewModel autoRefresh;
        private readonly ScrollRestoreViewModel scrollRestore;
        
        private readonly CoreWebView2CreationProperties creationArgs;

        private readonly ICommand refreshBtnCommand;
        private readonly ICommand goBtnCommand;

        private WebView2 webView;
        private WebView2MessagingService messenger;

        private bool browserSourceExposed;
        private Uri browserSource;
        private string url;
        private bool isNavigating;
        private string refreshButtonText = "↻", refreshButtonHint = "Refresh (F5)";
        
        private string title;
        private double zoomFactor;

        private TaskCompletionSource<Unit> refreshComplete;

        public BrowserTabViewModel(LayoutWindowTab model, LayoutBrowserWindowViewModel parentWindow, ProfileManager profileManager, IProfileListViewModelFactory profileListFactory,
            IAutoRefreshSettingsViewModelFactory autoRefreshFactory, IWebView2MessagingServiceFactory messengerFactory, IScrollRestoreViewModelFactory scrollFactory, ILogger logger)
        {
            this.parentWindow = parentWindow;
            this.messengerFactory = messengerFactory;
            this.logger = logger;

            scrollRestore = scrollFactory.ForTab(model);

            Option<ProfileItem> pf = profileManager.Profiles.Find(p => p.Name == model.profile);
            if (pf.IsNone)
            {
                pf = profileManager.AddProfile(model.profile, model.profile.FirstLetterToUpper());
            }
            profile = pf.Get();

            profileList = profileListFactory.ForOwnerTab(this);

            autoRefresh = autoRefreshFactory.ForSettings(model.autoRefreshEnabled, model.autoRefreshTime, OnAutoRefresh);

            zoomFactor = model.zoomFactor;
            title = model.title;

            refreshBtnCommand = new WindowCommand(ExecuteRefresh);
            goBtnCommand = new WindowCommand(ExecuteGo);

            creationArgs = new CoreWebView2CreationProperties
            {
                UserDataFolder = Path.Combine("Profiles", profile.Name)
            };

            url = model.url;
            browserSource = model.url.IsNullOrEmpty() ? null : new Uri(model.url);
        }

        private async Task OnAutoRefresh()
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

        public ICommand RefreshBtnCommand => refreshBtnCommand;
        public ICommand GoBtnCommand => goBtnCommand;

        public ProfileItem Profile => profile;
        public ProfileListViewModel ProfileList => profileList;
        public AutoRefreshSettingsViewModel AutoRefresh => autoRefresh;
        public ScrollRestoreViewModel ScrollRestore => scrollRestore;

        public LayoutBrowserWindowViewModel ParentWindow => parentWindow;

        public LayoutWindowTab ToModel() => new LayoutWindowTab
        {
            url = browserSource.ToString(),
            title = title,
            profile = profile.Name,
            zoomFactor = zoomFactor,
            autoRefreshEnabled = autoRefresh.AutoRefreshEnabled,
            autoRefreshTime = autoRefresh.AutoRefreshSpan,
            scrollDelay = scrollRestore.ScrollDelay,
            scrollX = scrollRestore.LastScroll.X,
            scrollY = scrollRestore.LastScroll.Y
        };

        public async void Refresh()
        {
            scrollRestore.RememberScroll();

            await webView.EnsureCoreWebView2Async();

            webView.Reload();
        }

        private void ExecuteRefresh()
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

        private void ExposeBrowserSource()
        {
            browserSourceExposed = true;

            OnPropertyChanged(nameof(BrowserSource));
        }

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

            messenger = messengerFactory.ForWebView2(wv);

            OnControlInitialized();

            await scrollRestore.PlugIntoWebView(wv, messenger);

            Title = wv.CoreWebView2.DocumentTitle;
            wv.CoreWebView2.DocumentTitleChanged += OnTitleChanged;

            wv.CoreWebView2.WindowCloseRequested += OnCloseRequested;
            wv.CoreWebView2.NewWindowRequested += OnNewWindowRequested;

            ExposeBrowserSource();
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

        public void OnNavigationStarted(CoreWebView2NavigationStartingEventArgs e)
        {
            IsNavigating = true;
        }

        public event Action<BrowserTabViewModel> NavigationCompleted;

        public void OnNavigationCompleted(CoreWebView2NavigationCompletedEventArgs e)
        {
            IsNavigating = false;

            NavigationCompleted?.Invoke(this);
        }

        public event Action<ProfileItem> NewProfileSelected;

        public void OnNewProfileSelected(ProfileItem piModel)
        {
            NewProfileSelected?.Invoke(piModel);
        }

        public void Dispose()
        {
            autoRefresh.Dispose();
            messenger?.Dispose();
        }
    }
}