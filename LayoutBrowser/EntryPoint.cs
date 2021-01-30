using System;
using System.Collections.Generic;
using Ninject;
using Ninject.Modules;
using WpfAppCommon;

namespace LayoutBrowser
{
    public static class EntryPoint
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var ep = new WpfAppEntryPoint<App, LayoutBrowserWindow>(new List<INinjectModule>
            {
                new LayoutBrowserAppModule()
            }, args, singleInstance: true);

            ep.OverrideStartupSequence(LayoutRestoreStartup);

            ep.Start();
        }

        private static void LayoutRestoreStartup(IKernel kernel, App app, WpfAppService<App, LayoutBrowserWindow> service)
        {
            LayoutManager manager = kernel.Get<LayoutManager>();

            manager.RestoreLayout();

            // todo: set main window in service after restore
        }
    }
}