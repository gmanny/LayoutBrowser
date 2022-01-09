using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using LayoutBrowser.Layout;
using LayoutBrowser.RuntimeInstall;
using LayoutBrowser.Window;
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
            }, args, singleInstance: true, showConsole: Debugger.IsAttached || args.Any(a => a.ToLowerInvariant().Contains("console")));

            ep.OverrideStartupSequence(LayoutRestoreStartup);

            ep.Start();
        }

        private static void LayoutRestoreStartup(IKernel kernel, App app, WpfAppService<App, LayoutBrowserWindow> service)
        {
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // check if WebView2 runtime is available
            if (!RuntimeInstallWindowViewModel.IsRuntimeAvailable())
            {
                if (!InstallRuntime(kernel))
                {
                    // runtime install unsuccessful, exit the app
                    Environment.Exit(-2);
                }
            }

            LayoutManager manager = kernel.Get<LayoutManager>();

            manager.RestoreLayout();
        }

        private static bool InstallRuntime(IKernel kernel)
        {
            RuntimeInstallWindowViewModel vm = kernel.Get<RuntimeInstallWindowViewModel>();
            IRuntimeInstallWindowFactory wndFactory = kernel.Get<IRuntimeInstallWindowFactory>();
            RuntimeInstallWindow wnd = wndFactory.ForViewModel(vm);

            return wnd.ShowDialog() ?? false;
        }
    }
}