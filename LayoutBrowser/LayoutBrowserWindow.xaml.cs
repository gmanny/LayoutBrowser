using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using WpfAppCommon;

namespace LayoutBrowser
{
    public interface ILayoutBrowserWindowFactory
    {
        public LayoutBrowserWindow ForViewModel(LayoutBrowserWindowViewModel viewModel);
    }

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

            AddShortcut(Key.W, ModifierKeys.Control, viewModel.CloseCurrentTab);
            AddShortcut(Key.T, ModifierKeys.Control, () => viewModel.OpenNewTab());
            AddShortcut(Key.Tab, ModifierKeys.Control, viewModel.NextTab);
            AddShortcut(Key.Tab, ModifierKeys.Control | ModifierKeys.Shift, viewModel.PrevTab);
            AddShortcut(Key.Left, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt, viewModel.MovePrev);
            AddShortcut(Key.Right, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt, viewModel.MoveNext);
            AddShortcut(Key.Q, ModifierKeys.Alt, viewModel.Quit);
            AddShortcut(Key.Q, ModifierKeys.Control, viewModel.Quit);
            AddShortcut(Key.Escape, ModifierKeys.None, viewModel.StopLoading);
            AddShortcut(Key.F5, ModifierKeys.None, viewModel.Refresh);
            AddShortcut(Key.F6, ModifierKeys.None, viewModel.FocusAddressBar);
            AddShortcut(Key.P, ModifierKeys.Control | ModifierKeys.Shift, viewModel.RequestPopout);
            AddShortcut(Key.N, ModifierKeys.Control, viewModel.OpenNewEmptyWindow);
            AddShortcut(Key.N, ModifierKeys.Control | ModifierKeys.Shift, viewModel.OpenNewEmptyWindow);
        }

        public LayoutBrowserWindowViewModel ViewModel => viewModel;

        private void WebView_OnCoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            logger.LogInformation($"WV2 initialized, success = {e.IsSuccess}");
        }
        
        protected void AddShortcut(Key key, ModifierKeys modifier, Action run)
        {
            InputBindings.Add(
                new KeyBinding(new WindowCommand(run), key, modifier)
            );
        }

        private void OnTabClicked(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle)
            {
                return;
            }

            e.Handled = true;

            ListBoxItem item = (ListBoxItem) sender;
            WindowTabItem tab = (WindowTabItem) item.DataContext;

            viewModel.CloseTab(tab);
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
