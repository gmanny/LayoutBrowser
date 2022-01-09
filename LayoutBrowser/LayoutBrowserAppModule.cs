using LayoutBrowser.Layout;
using LayoutBrowser.RuntimeInstall;
using LayoutBrowser.Tab;
using LayoutBrowser.Window;
using Monitor.ServiceCommon.Services.DiEager;
using Ninject.Extensions.Factory;
using Ninject.Modules;

namespace LayoutBrowser
{
    public class LayoutBrowserAppModule : NinjectModule
    {
        public override void Load()
        {
            Bind<LayoutManager>().ToSelf().InSingletonScope();
            Bind<ProfileManager>().ToSelf().InSingletonScope();
            Bind<ConsoleCommandProvider>().ToSelf().AsEagerSingleton();

            // UI
            Bind<App>().ToSelf().InSingletonScope();
            Bind<AppUnhandledExceptionSvc>().ToSelf().AsEagerSingleton();
            Bind<AutoRefreshGlobalOneSecondTimer>().ToSelf().InSingletonScope();

            Bind<WebView2MessagingService>().ToSelf();
            Bind<IWebView2MessagingServiceFactory>().ToFactory();

            Bind<LayoutManagerViewModel>().ToSelf().InSingletonScope();

            // -> windows
            Bind<BrowserTab>().ToSelf();
            Bind<IBrowserTabFactory>().ToFactory();
            Bind<ProfileListViewModel>().ToSelf();
            Bind<IProfileListViewModelFactory>().ToFactory();
            Bind<AutoRefreshSettingsViewModel>().ToSelf();
            Bind<IAutoRefreshSettingsViewModelFactory>().ToFactory();
            Bind<NegativeMarginViewModel>().ToSelf();
            Bind<INegativeMarginViewModelFactory>().ToFactory();
            Bind<ScrollRestoreViewModel>().ToSelf();
            Bind<IScrollRestoreViewModelFactory>().ToFactory();
            Bind<BrowserTabViewModel>().ToSelf();
            Bind<IBrowserTabViewModelFactory>().ToFactory();
            Bind<UrlLockViewModel>().ToSelf();
            Bind<IUrlLockViewModelFactory>().ToFactory();
            Bind<ElementBlockerViewModel>().ToSelf();
            Bind<IElementBlockerViewModelFactory>().ToFactory();

            Bind<LayoutBrowserWindow>().ToSelf();
            Bind<ILayoutBrowserWindowFactory>().ToFactory();
            Bind<LayoutBrowserWindowViewModel>().ToSelf();
            Bind<ILayoutBrowserWindowViewModelFactory>().ToFactory();

            Bind<RuntimeInstallWindowViewModel>().ToSelf();
            Bind<RuntimeInstallWindow>().ToSelf();
            Bind<IRuntimeInstallWindowFactory>().ToFactory();
        }
    }
}