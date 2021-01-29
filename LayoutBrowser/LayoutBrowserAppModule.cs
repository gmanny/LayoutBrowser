using Ninject.Modules;

namespace LayoutBrowser
{
    public class LayoutBrowserAppModule : NinjectModule
    {
        public override void Load()
        {
            // UI
            Bind<App>().ToSelf().InSingletonScope();

            // -> windows
            Bind<LayoutBrowserWindow>().ToSelf();
            Bind<LayoutBrowserWindowViewModel>().ToSelf().InSingletonScope();
        }
    }
}