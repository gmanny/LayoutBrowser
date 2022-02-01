using System;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace LayoutBrowser;

public class AppUnhandledExceptionSvc
{
    private readonly ILogger logger;

    public AppUnhandledExceptionSvc(App app, ILogger logger)
    {
        this.logger = logger;

        app.DispatcherUnhandledException += (_, e) => OnDispatcherUnhandledException(e);
    }

    private void OnDispatcherUnhandledException(DispatcherUnhandledExceptionEventArgs e)
    {
        if (e.Exception is not InvalidOperationException io)
        {
            return;
        }

        if (io.Message.Contains("RoutedEvent"))
        {
            // webView2 seems (understandably) quirky when changing its parent window
            e.Handled = true;

            logger.LogDebug($"Ignoring exception `{e.Exception.Message}`");
        }
    }
}