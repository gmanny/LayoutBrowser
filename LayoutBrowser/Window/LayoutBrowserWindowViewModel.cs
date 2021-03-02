using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LanguageExt;
using LayoutBrowser.Layout;
using LayoutBrowser.Tab;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using MonitorCommon;
using MvvmHelpers;

namespace LayoutBrowser.Window
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

        private readonly Guid id;

        private double left, top, width, height;
        private readonly double leftInit, topInit, widthInit, heightInit;
        private WindowState state;
        private WindowTabItem currentTab;
        private bool showTabBar;
        private bool uiHidden;
        private bool backgroundLoadEnabled;

        public LayoutBrowserWindowViewModel(LayoutWindow model, IBrowserTabFactory tabFactory, IBrowserTabViewModelFactory tabVmFactory, ILogger logger)
        {
            this.tabFactory = tabFactory;
            this.tabVmFactory = tabVmFactory;
            this.logger = logger;

            id = model.id;

            leftInit = left = model.left;
            topInit = top = model.top;
            widthInit = width = model.width;
            heightInit = height = model.height;
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
                CurrentTab.ViewModel.NavigationCompleted += OnFirstNavComplete;
            }
            else
            {
                BackgroundLoadEnabled = true;
            }
        }

        public double LeftInit => leftInit;

        public double TopInit => topInit;

        public double WidthInit => widthInit;

        public double HeightInit => heightInit;

        private void OnFirstNavComplete(BrowserTabViewModel vm)
        {
            BackgroundLoadEnabled = true;

            vm.NavigationCompleted -= OnFirstNavComplete;
        }

        public Guid Id => id;

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

        public WindowTabItem AddTab(LayoutWindowTab tabModel, int position = -1)
        {
            BrowserTabViewModel vm = tabVmFactory.ForModel(tabModel, this);

            vm.CloseRequested += OnTabCloseRequested;
            vm.NewWindowRequested += OnTabNewWindowRequested;
            vm.ControlInitialized += OnTabControlInitialized;
            vm.NewProfileSelected += OnNewProfileSelected;

            BrowserTab t = tabFactory.ForViewModel(vm);

            WindowTabItem item = new WindowTabItem(vm, t);

            backgroundLoading.Add(item);
            if (position >= 0 && position < tabs.Count)
            {
                tabs.Insert(position, item);
            }
            else
            {
                tabs.Add(item);
            }

            return item;
        }

        private void OnNewProfileSelected(ProfileItem pf)
        {
            OpenNewTab(pf.Name);
        }

        public WindowTabItem AddForeignTab(WindowTabItem item, bool foreground)
        {
            item.ViewModel.CloseRequested += OnTabCloseRequested;
            item.ViewModel.NewWindowRequested += OnTabNewWindowRequested;
            item.ViewModel.NewProfileSelected += OnNewProfileSelected;

            tabs.Add(item);

            if (foreground)
            {
                CurrentTab = item;
            }

            return item;
        }

        private void OnTabControlInitialized(BrowserTabViewModel vm)
        {
            Option<WindowTabItem> itm = tabs.Find(t => t.ViewModel == vm);
            foreach (WindowTabItem tabItem in itm)
            {
                backgroundLoading.Remove(tabItem);
            }

            vm.ControlInitialized -= OnTabControlInitialized;
        }

        private void OnTabNewWindowRequested(BrowserTabViewModel vm, CoreWebView2NewWindowRequestedEventArgs e)
        {
            Option<WindowTabItem> itm = tabs.Find(t => t.ViewModel == vm);
            foreach (WindowTabItem tabItem in itm)
            {
                OnNewWindowRequested(tabItem, e);
            }
        }

        private void OnTabCloseRequested(BrowserTabViewModel vm)
        {
            Option<WindowTabItem> itm = tabs.Find(t => t.ViewModel == vm);
            foreach (WindowTabItem tabItem in itm)
            {
                logger.LogDebug($"Tab {tabItem.ViewModel.Url} requested to close itself");
                CloseTab(tabItem);
            }
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
                    profile = itm.ViewModel.Profile.Name,
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
            id = id,
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
        public ObservableCollection<WindowTabItem> BackgroundLoading => backgroundLoadEnabled ? backgroundLoading : null;

        public bool BackgroundLoadEnabled
        {
            get => backgroundLoadEnabled;
            set
            {
                SetProperty(ref backgroundLoadEnabled, value);
                OnPropertyChanged(nameof(BackgroundLoading));
            }
        }

        public WindowTabItem CurrentTab
        {
            get => currentTab;
            set
            {
                backgroundLoading.Remove(value);

                if (currentTab != null)
                {
                    BackgroundLoadEnabled = true;
                }

                SetProperty(ref currentTab, value);

                if (value == null)
                {
                    return;
                }

                value.Control.Dispatcher.BeginInvoke(() =>
                {
                    value.Control.urlBar.Focus();
                    try
                    {
                        value.Control.webView.Focus();
                    }
                    catch (COMException)
                    {
                        value.Control.Dispatcher.BeginInvoke(async () =>
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(100));

                            try
                            {
                                value.Control.webView.Focus();
                            }
                            // ReSharper disable once EmptyGeneralCatchClause
                            catch { }
                        }, DispatcherPriority.Background);
                    }
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
            if (tabs.Count <= 1)
            {
                CloseWindow();
            }
            else
            {
                CloseTab(CurrentTab);
            }
        }

        public event Action WindowCloseRequested;

        private void CloseWindow()
        {
            WindowCloseRequested?.Invoke();
        }

        public event Action<LayoutBrowserWindowViewModel, WindowTabItem, int> TabClosed;

        public void CloseTab(WindowTabItem tab, bool doDispose = true)
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

            tab.ViewModel.CloseRequested -= OnTabCloseRequested;
            tab.ViewModel.NewWindowRequested -= OnTabNewWindowRequested;
            tab.ViewModel.ControlInitialized -= OnTabControlInitialized;
            tab.ViewModel.NewProfileSelected -= OnNewProfileSelected;

            if (doDispose)
            {
                TabClosed?.Invoke(this, tab, tabIndex);

                tab.Dispose();
            }

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

        public void OpenNewTab(string profile = null)
        {
            WindowTabItem tab = AddTab(new LayoutWindowTab
            {
                url = null,
                profile = profile ?? currentTab.ViewModel.Profile.Name ?? ProfileManager.DefaultProfile
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
            currentTab?.ViewModel.Refresh();
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
        
        public event Action<LayoutBrowserWindowViewModel, WindowTabItem> PopoutRequested;

        public void RequestPopout()
        {
            if (tabs.Count <= 1)
            {
                logger.LogInformation("Preventing tab pop-out because window doesn't have enough tabs left");

                return;
            }

            WindowTabItem tab = CurrentTab;
            if (tab == null)
            {
                return;
            }

            CloseTab(tab, false);

            PopoutRequested?.Invoke(this, tab);
        }

        public async void OpenNewEmptyWindow()
        {
            await OnOpenNewWindow(currentTab, null, true);
        }

        public void ToggleUi()
        {
            UiHidden = !UiHidden;
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
            ViewModel.Dispose();
            Control.Dispose();
        }
    }
}