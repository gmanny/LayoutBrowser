using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace LayoutBrowser
{
    public interface IBrowserTabFactory
    {
        public BrowserTab ForViewModel(BrowserTabViewModel viewModel);
    }

    public partial class BrowserTab
    {
        private readonly BrowserTabViewModel viewModel;

        public BrowserTab(BrowserTabViewModel viewModel)
        {
            this.viewModel = viewModel;

            InitializeComponent();

            viewModel.WebView = webView;
        }

        public BrowserTabViewModel ViewModel => viewModel;

        private void OnNavigationStarted(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            viewModel.OnNavigationStarted(e);
        }

        private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            viewModel.OnNavigationCompleted(e);
        }

        public void Dispose()
        {
            webView.Visibility = Visibility.Collapsed;
            webView.Dispose();
        }
    }
}
