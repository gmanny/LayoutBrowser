using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace LayoutBrowser.Tab
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

        private void OnNegativeMarginToggle(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && sender is MenuItem itm)
            {
                itm.IsChecked = !itm.IsChecked;
            }
        }

        private void OnNegativeMarginFeatureButtonPressed(object sender, MouseButtonEventArgs e)
        {
            viewModel.NegativeMargin.Enabled = !viewModel.NegativeMargin.Enabled;
        }

        private void OnLockScrollFeatureButtonPressed(object sender, MouseButtonEventArgs e)
        {
            viewModel.ScrollRestore.LockScroll = !viewModel.ScrollRestore.LockScroll;
        }

        private void OnUrlLockFeatureButtonPressed(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ChangedButton == MouseButton.Middle && !viewModel.UrlVm.LockUrl)
                {
                    viewModel.UrlVm.LockUrlEx();
                    return;
                }

                viewModel.UrlVm.LockUrl = !viewModel.UrlVm.LockUrl;
            }
            finally
            {
                e.Handled = true;
            }
        }
    }
}
