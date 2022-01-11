using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LayoutBrowser.Layout;
using LayoutBrowser.Window;
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
        private readonly LayoutManager layoutManager;

        public BrowserTab(BrowserTabViewModel viewModel, LayoutManager layoutManager)
        {
            this.viewModel = viewModel;
            this.layoutManager = layoutManager;

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

        private void TabHiddenBg_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                viewModel.Hidden = !viewModel.Hidden;

                e.Handled = true;
                return;
            }
        }

        private void DuplicateTabClick(object sender, RoutedEventArgs e)
        {
            WindowTabItem tab = viewModel.ParentWindow.AddTab(viewModel.ToModel().Copy());

            viewModel.ParentWindow.CurrentTab = tab;
        }

        private void DuplicateWindowClick(object sender, RoutedEventArgs e)
        {
            LayoutWindow model = viewModel.ParentWindow.ToModel().Copy();
            model.top += 50;
            model.left += 50;

            model.topNative = Double.NaN;
            model.leftNative = Double.NaN;
            model.widthNative = Double.NaN;
            model.heightNative = Double.NaN;

            layoutManager.AddWindow(model);
        }

        private void OnElementBlockerFeatureButtonPressed(object sender, MouseButtonEventArgs e)
        {
            viewModel.ElementBlocker.Enabled = !viewModel.ElementBlocker.Enabled;
        }
    }
}
