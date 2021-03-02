using System.Threading.Tasks;
using System.Windows;
using LayoutBrowser.Layout;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MvvmHelpers;

namespace LayoutBrowser.Tab
{
    public interface INegativeMarginViewModelFactory
    {
        public NegativeMarginViewModel ForModel(TabNegativeMargin model);
    }

	// document.body.style.margin = "-100px -100px -100px -100px"
    public class NegativeMarginViewModel : ObservableObject
    {
        private double marginLeft, marginTop, marginRight, marginBottom;
        private bool leftRightNativeMode;

        private WebView2 webView;

        public NegativeMarginViewModel(TabNegativeMargin model)
        {
            marginLeft = model.left;
            marginTop = model.top;
            marginRight = model.right;
            marginBottom = model.bottom;
            leftRightNativeMode = model.leftRightNativeMode;
        }

        public TabNegativeMargin ToModel() => new TabNegativeMargin
        {
            left = marginLeft,
            top = marginTop,
            right = marginRight,
            bottom = marginBottom,
            leftRightNativeMode = leftRightNativeMode
        };

        public double MarginLeft
        {
            get => marginLeft;
            set
            {
                SetProperty(ref marginLeft, value);

                UpdateMargin();
            }
        }

        public double MarginTop
        {
            get => marginTop;
            set
            {
                SetProperty(ref marginTop, value);

                UpdateMargin();
            }
        }

        public double MarginRight
        {
            get => marginRight;
            set
            {
                SetProperty(ref marginRight, value);

                UpdateMargin();
            }
        }

        public double MarginBottom
        {
            get => marginBottom;
            set
            {
                SetProperty(ref marginBottom, value);

                UpdateMargin();
            }
        }

        public bool LeftRightNativeMode
        {
            get => leftRightNativeMode;
            set
            {
                SetProperty(ref leftRightNativeMode, value);

                UpdateMargin();
            }
        }

        public Thickness NativeMargin => leftRightNativeMode
            ? new Thickness(-marginLeft, 0, -marginRight, -marginBottom)
            : new Thickness(0, 0, 0, -marginBottom);

        private void UpdateMargin()
        {
            OnPropertyChanged(nameof(NativeMargin));

            SetNegativeMargin();
        }

        public async Task PlugIntoWebView(WebView2 wv, WebView2MessagingService messenger)
        {
            webView = wv;
            
            wv.NavigationCompleted += OnNavigationCompleted;
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            SetNegativeMargin();
        }

        private async void SetNegativeMargin()
        {
            if (webView == null)
            {
                return;
            }

            // bottom margin doesn't work and is implemented differently
            await webView.ExecuteScriptAsync($"document.body.style.margin = \"{-marginTop}px {(leftRightNativeMode ? 0 : -marginRight)}px 0px {(leftRightNativeMode ? 0 : -marginLeft)}px\"");
        }
    }
}