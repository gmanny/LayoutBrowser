﻿using Monitor.ServiceCommon.Services.DiEager;
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

            // UI
            Bind<App>().ToSelf().InSingletonScope();
            Bind<AppUnhandledExceptionSvc>().ToSelf().AsEagerSingleton();
            Bind<AutoRefreshGlobalOneSecondTimer>().ToSelf().InSingletonScope();

            // -> windows
            Bind<BrowserTab>().ToSelf();
            Bind<IBrowserTabFactory>().ToFactory();
            Bind<ProfileListViewModel>().ToSelf();
            Bind<IProfileListViewModelFactory>().ToFactory();
            Bind<AutoRefreshSettingsViewModel>().ToSelf();
            Bind<IAutoRefreshSettingsViewModelFactory>().ToFactory();
            Bind<BrowserTabViewModel>().ToSelf();
            Bind<IBrowserTabViewModelFactory>().ToFactory();

            Bind<LayoutBrowserWindow>().ToSelf();
            Bind<ILayoutBrowserWindowFactory>().ToFactory();
            Bind<LayoutBrowserWindowViewModel>().ToSelf();
            Bind<ILayoutBrowserWindowViewModelFactory>().ToFactory();
        }
    }
}