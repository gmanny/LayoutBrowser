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
        private readonly IWebView2MessagingServiceFactory messengerFactory;
        private readonly LayoutManagerViewModel layoutManagerVm;
        private readonly ILogger logger;

        private LayoutBrowserWindowViewModel parentWindow;
        
        private readonly ProfileItem profile;
        private readonly ProfileListViewModel profileList;
        private readonly AutoRefreshSettingsViewModel autoRefresh;
        private readonly ScrollRestoreViewModel scrollRestore;
        private readonly NegativeMarginViewModel negativeMargin;
        private readonly UrlLockViewModel urlVm;
        
        private readonly CoreWebView2CreationProperties creationArgs;

        private readonly ICommand refreshBtnCommand;
        private readonly ICommand goBtnCommand;

        private WebView2 webView;
        private WebView2MessagingService messenger;

        private string browserTitle;
        private string overrideTitle;
        private double zoomFactor;
        private double storedZoomFactor;
        private bool hidden;

        public BrowserTabViewModel(LayoutWindowTab model, LayoutBrowserWindowViewModel parentWindow, ProfileManager profileManager, IProfileListViewModelFactory profileListFactory,
            IAutoRefreshSettingsViewModelFactory autoRefreshFactory, IWebView2MessagingServiceFactory messengerFactory, IScrollRestoreViewModelFactory scrollFactory,
            LayoutManagerViewModel layoutManagerVm, INegativeMarginViewModelFactory negativeMarginFactory, IUrlLockViewModelFactory urlFactory, ILogger logger)
        {
            this.parentWindow = parentWindow;
            this.messengerFactory = messengerFactory;
            this.layoutManagerVm = layoutManagerVm;
            this.logger = logger;

            urlVm = urlFactory.ForTab(model);

            scrollRestore = scrollFactory.ForTab(model);
            urlVm.PreRefresh += () => scrollRestore.RememberScroll();
            
            negativeMargin = negativeMarginFactory.ForModel(model.negativeMargin ?? new TabNegativeMargin());

            Option<ProfileItem> pf = profileManager.Profiles.Find(p => p.Name == model.profile);
            if (pf.IsNone)
            {
                pf = profileManager.AddProfile(model.profile, model.profile.FirstLetterToUpper());
            }
            profile = pf.Get();

            profileList = profileListFactory.ForOwnerTab(this);

            autoRefresh = autoRefreshFactory.ForSettings(model.autoRefreshEnabled, model.autoRefreshTime, urlVm.OnAutoRefresh);
            
            storedZoomFactor = zoomFactor = model.zoomFactor;
            browserTitle = model.title;
            overrideTitle = model.overrideTitle;
            hidden = model.hidden;

            refreshBtnCommand = new WindowCommand(urlVm.HandleRefreshStopButtonPress);
            goBtnCommand = new WindowCommand(async () => await urlVm.ExecuteGo());

            creationArgs = new CoreWebView2CreationProperties
            {
                UserDataFolder = Path.Combine("Profiles", profile.Name)
            };
        }

        public ICommand RefreshBtnCommand => refreshBtnCommand;
        public ICommand GoBtnCommand => goBtnCommand;

        public ProfileItem Profile => profile;
        public ProfileListViewModel ProfileList => profileList;
        public AutoRefreshSettingsViewModel AutoRefresh => autoRefresh;
        public ScrollRestoreViewModel ScrollRestore => scrollRestore;
        public NegativeMarginViewModel NegativeMargin => negativeMargin;
        public UrlLockViewModel UrlVm => urlVm;

        public LayoutManagerViewModel LayoutMgr => layoutManagerVm;

        public void ChangeParent(LayoutBrowserWindowViewModel newParent)
        {
            parentWindow = newParent;

            OnPropertyChanged(nameof(ParentWindow));
        }

        public LayoutBrowserWindowViewModel ParentWindow => parentWindow;

        public LayoutWindowTab ToModel() => new LayoutWindowTab
        {
            url = urlVm.LockedUrl ?? urlVm.InternalBrowserSource?.ToString(),
            lockUrl = urlVm.LockUrl,
            title = browserTitle,
            overrideTitle = overrideTitle,
            profile = profile.Name,
            zoomFactor = zoomFactor,
            hidden = hidden,
            autoRefreshEnabled = autoRefresh.AutoRefreshEnabled,
            autoRefreshTime = autoRefresh.AutoRefreshSpan,
            scrollDelay = scrollRestore.ScrollDelay,
            scrollX = scrollRestore.LastScroll.X,
            scrollY = scrollRestore.LastScroll.Y,
            negativeMargin = negativeMargin.ToModel()
        };

        public bool Hidden
        {
            get => hidden;
            set => SetProperty(ref hidden, value);
        }

        public CoreWebView2CreationProperties CreationProperties => creationArgs;

        public string Title => overrideTitle.IsNullOrEmpty() ? browserTitle : overrideTitle.Replace("[$$$]", browserTitle);

        public string BrowserTitle
        {
            get => browserTitle;
            set
            {
                if (value.IsNullOrEmpty())
                {
                    return;
                }

                SetProperty(ref browserTitle, value);
                OnPropertyChanged(nameof(Title));
            }
        }

        public string OverrideTitle
        {
            get => overrideTitle;
            set
            {
                SetProperty(ref overrideTitle, value);
                OnPropertyChanged(nameof(Title));
            }
        }

        public double ZoomFactor
        {
            get => zoomFactor;
            set => SetProperty(ref zoomFactor, value);
        }

        private void ShakeZoomFactor()
        {
            double zf = zoomFactor;
            ZoomFactor = zf + 0.001;
            ZoomFactor = zf;
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

            await urlVm.PlugIntoWebView(wv, messenger);
            await scrollRestore.PlugIntoWebView(wv, messenger);
            await negativeMargin.PlugIntoWebView(wv, messenger);

            BrowserTitle = wv.CoreWebView2.DocumentTitle;
            wv.CoreWebView2.DocumentTitleChanged += OnTitleChanged;

            wv.CoreWebView2.WindowCloseRequested += OnCloseRequested;
            wv.CoreWebView2.NewWindowRequested += OnNewWindowRequested;

            urlVm.AfterInit();
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
            BrowserTitle = webView.CoreWebView2.DocumentTitle;
        }

        public void OnNavigationStarted(CoreWebView2NavigationStartingEventArgs e)
        {
            storedZoomFactor = zoomFactor;
        }

        public event Action<BrowserTabViewModel> NavigationCompleted;

        public void OnNavigationCompleted(CoreWebView2NavigationCompletedEventArgs e)
        {
            NavigationCompleted?.Invoke(this);

            if (Math.Abs(storedZoomFactor - zoomFactor) < 1e-7)
            {
                ShakeZoomFactor();
            }
            else
            {
                ZoomFactor = storedZoomFactor;
            }
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