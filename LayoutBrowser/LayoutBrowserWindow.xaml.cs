using System;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace LayoutBrowser
{
    /// <summary>
    /// Interaction logic for LayoutBrowserWindow.xaml
    /// </summary>
    public partial class LayoutBrowserWindow
    {
        private readonly LayoutBrowserWindowViewModel viewModel;
        private readonly ILogger logger;

        public LayoutBrowserWindow(LayoutBrowserWindowViewModel viewModel, ILogger logger)
        {
            this.viewModel = viewModel;
            this.logger = logger;

            InitializeComponent();
        }

        public LayoutBrowserWindowViewModel ViewModel => viewModel;

        private void WebView_OnCoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            logger.LogInformation($"WV2 initialized, success = {e.IsSuccess}");
        }
    }
}
