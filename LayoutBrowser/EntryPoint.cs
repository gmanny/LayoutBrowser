using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using LayoutBrowser;
using LayoutBrowser.Layout;
using LayoutBrowser.RuntimeInstall;
using LayoutBrowser.Window;
using Ninject;
using Ninject.Modules;
using WpfAppCommon;

Thread.CurrentThread.SetApartmentState(ApartmentState.Unknown);
Thread.CurrentThread.SetApartmentState(ApartmentState.STA);

WpfAppEntryPoint<App, LayoutBrowserWindow> entryPoint = new(new List<INinjectModule>
    {
        new LayoutBrowserAppModule()
    }, args, singleInstance: true,
    showConsole: Debugger.IsAttached || args.Any(a => a.ToLowerInvariant().Contains("console")));

entryPoint.OverrideStartupSequence(LayoutRestoreStartup);

entryPoint.Start();


void LayoutRestoreStartup(IKernel kernel, App app, WpfAppService<App, LayoutBrowserWindow> service)
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

bool InstallRuntime(IKernel kernel)
{
    RuntimeInstallWindowViewModel vm = kernel.Get<RuntimeInstallWindowViewModel>();
    IRuntimeInstallWindowFactory wndFactory = kernel.Get<IRuntimeInstallWindowFactory>();
    RuntimeInstallWindow wnd = wndFactory.ForViewModel(vm);

    return wnd.ShowDialog() ?? false;
}