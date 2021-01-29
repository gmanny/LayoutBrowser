using Ninject.Extensions.Factory;
using Ninject.Modules;

namespace LayoutBrowser
{
    public class LayoutBrowserAppModule : NinjectModule
    {
        public override void Load()
        {
            Bind<LayoutManager>().ToSelf().InSingletonScope();

            // UI
            Bind<App>().ToSelf().InSingletonScope();

            // -> windows
            Bind<BrowserTab>().ToSelf();
            Bind<IBrowserTabFactory>().ToFactory();
            Bind<BrowserTabViewModel>().ToSelf();
            Bind<IBrowserTabViewModelFactory>().ToFactory();

            Bind<LayoutBrowserWindow>().ToSelf();
            Bind<LayoutBrowserWindowViewModel>().ToSelf();
        }
    }
}