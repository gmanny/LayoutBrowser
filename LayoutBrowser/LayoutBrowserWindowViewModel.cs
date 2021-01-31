using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using MonitorCommon;
using MvvmHelpers;

namespace LayoutBrowser
{
    public interface ILayoutBrowserWindowViewModelFactory
    {
        public LayoutBrowserWindowViewModel ForModel(LayoutWindow model);
    }

    public class LayoutBrowserWindowViewModel : ObservableObject
    {
        private readonly IBrowserTabFactory tabFactory;
        private readonly IBrowserTabViewModelFactory tabVmFactory;
        private readonly ILogger logger;
        private readonly ObservableCollection<WindowTabItem> tabs = new ObservableCollection<WindowTabItem>();
        private readonly ObservableCollection<WindowTabItem> backgroundLoading = new ObservableCollection<WindowTabItem>();

        private double left, top, width, height;
        private WindowState state;
        private WindowTabItem currentTab;
        private bool showTabBar;
        private bool uiHidden;

        public LayoutBrowserWindowViewModel(LayoutWindow model, IBrowserTabFactory tabFactory, IBrowserTabViewModelFactory tabVmFactory, ILogger logger)
        {
            this.tabFactory = tabFactory;
            this.tabVmFactory = tabVmFactory;
            this.logger = logger;
            left = model.left;
            top = model.top;
            width = model.width;
            height = model.height;
            state = model.windowState;
            uiHidden = model.uiHidden;

            tabs.CollectionChanged += OnTabsChanged;

            foreach (LayoutWindowTab tabModel in model.tabs)
            {
                AddTab(tabModel);
            }

            if (tabs.NonEmpty())
            {
                CurrentTab = model.activeTabIndex >= 0 && model.activeTabIndex < tabs.Count ? tabs[model.activeTabIndex] : tabs[0];
            }
        }

        public bool ShowTabBar
        {
            get => showTabBar;
            set => SetProperty(ref showTabBar, value);
        }

        public bool UiHidden
        {
            get => uiHidden;
            set
            {
                SetProperty(ref uiHidden, value);
                OnPropertyChanged(nameof(UiVisible));
            }
        }

        public bool UiVisible => !uiHidden;

        private void OnTabsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            ShowTabBar = tabs.Count > 1;
        }

        private WindowTabItem AddTab(LayoutWindowTab tabModel)
        {
            WindowTabItem item = null;
            BrowserTabViewModel vm = tabVmFactory.ForModel(tabModel, this);
            vm.CloseRequested += _ =>
            {
                // ReSharper disable once AccessToModifiedClosure
                WindowTabItem itm = item;
                if (itm != null)
                {
                    logger.LogDebug($"Tab {itm.ViewModel.Url} requested to close itself");
                    CloseTab(itm);
                }
            };
            vm.NewWindowRequested += (_, e) =>
            {
                // ReSharper disable once AccessToModifiedClosure
                WindowTabItem itm = item;
                if (itm != null)
                {
                    OnNewWindowRequested(itm, e);
                }
            };
            vm.ControlInitialized += _ =>
            {
                // ReSharper disable once AccessToModifiedClosure
                WindowTabItem itm = item;
                if (itm != null)
                {
                    backgroundLoading.Remove(itm);
                }
            };

            BrowserTab t = tabFactory.ForViewModel(vm);

            item = new WindowTabItem(vm, t);

            backgroundLoading.Add(item);
            tabs.Add(item);

            return item;
        }

        private async void OnNewWindowRequested(WindowTabItem itm, CoreWebView2NewWindowRequestedEventArgs e)
        {
            CoreWebView2Deferral deferral = e.GetDeferral();
            try
            {
                bool newWindow = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
                bool foreground = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

                if (newWindow)
                {
                    await OnOpenNewWindow(itm, e, foreground);
                    return;
                }

                WindowTabItem newTab = AddTab(new LayoutWindowTab
                {
                    profile = itm.ViewModel.Profile,
                    url = null,
                    title = "New Tab"
                });

                if (foreground)
                {
                    CurrentTab = newTab;
                }

                await newTab.Control.webView.EnsureCoreWebView2Async();
                if (newTab.Control.webView.CoreWebView2 != null)
                {
                    e.NewWindow = newTab.Control.webView.CoreWebView2;
                    e.Handled = true;
                }
            }
            finally
            {
                deferral.Complete();
            }
        }

        private async Task OnOpenNewWindow(WindowTabItem itm, CoreWebView2NewWindowRequestedEventArgs e, bool foreground)
        {
            Task result = OpenNewWindow?.Invoke(itm, this, e, foreground);
            if (result != null)
            {
                await result;
            }
        }

        public event Func<WindowTabItem, LayoutBrowserWindowViewModel, CoreWebView2NewWindowRequestedEventArgs, bool, Task> OpenNewWindow;

        public LayoutWindow ToModel() => new LayoutWindow
        {
            left = left,
            top = top,
            width = width,
            height = height,
            windowState = state,
            uiHidden = uiHidden,
            tabs = tabs.Select(t => t.ViewModel.ToModel()).ToList(),
            activeTabIndex = tabs.IndexOf(CurrentTab)
        };

        public ObservableCollection<WindowTabItem> Tabs => tabs;
        public ObservableCollection<WindowTabItem> BackgroundLoading => backgroundLoading;

        public WindowTabItem CurrentTab
        {
            get => currentTab;
            set
            {
                backgroundLoading.Remove(value);

                SetProperty(ref currentTab, value);

                if (value == null)
                {
                    return;
                }

                value.Control.Dispatcher.BeginInvoke(() =>
                {
                    value.Control.urlBar.Focus();
                    value.Control.webView.Focus();
                }, DispatcherPriority.Background);
            }
        }

        public double Left
        {
            get => left;
            set => SetProperty(ref left, value);
        }

        public double Top
        {
            get => top;
            set => SetProperty(ref top, value);
        }

        public double Width
        {
            get => width;
            set => SetProperty(ref width, value);
        }

        public double Height
        {
            get => height;
            set => SetProperty(ref height, value);
        }

        public WindowState State
        {
            get => state;
            set => SetProperty(ref state, value);
        }

        public void CloseCurrentTab()
        {
            CloseTab(CurrentTab);
        }

        public void CloseTab(WindowTabItem tab)
        {
            if (tab == null)
            {
                return;
            }

            bool isCurrent = CurrentTab == tab;

            int tabIndex = Tabs.IndexOf(tab);
            if (tabIndex >= 0)
            {
                Tabs.RemoveAt(tabIndex);
            }
            else
            {
                tabIndex = 0;
            }

            tab.Dispose();

            if (isCurrent)
            {
                if (tabIndex < tabs.Count)
                {
                    CurrentTab = tabs[tabIndex];
                }
                else if (tabs.NonEmpty())
                {
                    CurrentTab = tabs[^1];
                }
                else
                {
                    CurrentTab = null;

                    OnWindowBecameEmpty();
                }
            }
        }

        public event Action<LayoutBrowserWindowViewModel> WindowBecameEmpty;

        protected virtual void OnWindowBecameEmpty()
        {
            WindowBecameEmpty?.Invoke(this);
        }

        public void OpenNewTab()
        {
            WindowTabItem tab = AddTab(new LayoutWindowTab
            {
                url = null
            });

            CurrentTab = tab;
            
            // focus url bar
            tab.Control.Dispatcher.BeginInvoke(() =>
            {
                tab.Control.urlBar.Focus();
            }, DispatcherPriority.Background);
        }

        public void NextTab()
        {
            ChangeTabOffs(+1);
        }

        public void PrevTab()
        {
            ChangeTabOffs(-1);
        }

        private void ChangeTabOffs(int offs)
        {
            WindowTabItem ct = CurrentTab;
            if (ct == null)
            {
                return;
            }

            int tabIndex = Tabs.IndexOf(ct);
            if (tabIndex < 0)
            {
                return;
            }

            int idx = tabIndex + offs;
            if (idx < 0)
            {
                idx = Tabs.Count - 1;
            }

            idx = idx % Tabs.Count;

            CurrentTab = Tabs[idx];
        }

        public void MoveNext()
        {
            MoveTabOffs(+1);
        }

        public void MovePrev()
        {
            MoveTabOffs(-1);
        }

        private void MoveTabOffs(int offs)
        {
            WindowTabItem ct = CurrentTab;
            if (ct == null)
            {
                return;
            }

            int tabIndex = Tabs.IndexOf(ct);
            if (tabIndex < 0)
            {
                return;
            }

            int idx = tabIndex + offs;
            if (idx < 0)
            {
                idx = Tabs.Count - 1;
            }

            idx = idx % Tabs.Count;

            Tabs.Move(tabIndex, idx);
        }

        public void Quit()
        {
            Environment.Exit(0);
        }

        public void StopLoading()
        {
            currentTab?.Control.webView.Stop();
        }

        public void Refresh()
        {
            currentTab?.Control.webView.Reload();
        }

        public void FocusAddressBar()
        {
            WindowTabItem ct = currentTab;
            if (ct == null)
            {
                return;
            }

            if (ct.Control.urlBar.IsKeyboardFocusWithin)
            {
                ct.Control.webView.Focus();
            }
            else
            {
                ct.Control.urlBar.Focus();
            }
        }
    }

    public class WindowTabItem : ObservableObject
    {
        private BrowserTabViewModel viewModel;
        private BrowserTab control;

        public WindowTabItem(BrowserTabViewModel viewModel, BrowserTab control)
        {
            this.viewModel = viewModel;
            this.control = control;
        }

        public BrowserTabViewModel ViewModel
        {
            get => viewModel;
            set => SetProperty(ref viewModel, value);
        }

        public BrowserTab Control
        {
            get => control;
            set => SetProperty(ref control, value);
        }

        public void Dispose()
        {
            Control.Dispose();
        }
    }
}