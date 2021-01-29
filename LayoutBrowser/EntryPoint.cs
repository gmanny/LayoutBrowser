using System;
using System.Collections.Generic;
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

            ep.Start();
        }
    }
}