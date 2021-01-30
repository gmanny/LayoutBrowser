using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
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
        private readonly ObservableCollection<WindowTabItem> tabs = new ObservableCollection<WindowTabItem>();

        private double left, top, width, height;
        private WindowState state;
        private WindowTabItem currentTab;
        private bool showTabBar;

        public LayoutBrowserWindowViewModel(LayoutWindow model, IBrowserTabFactory tabFactory, IBrowserTabViewModelFactory tabVmFactory)
        {
            this.tabFactory = tabFactory;
            this.tabVmFactory = tabVmFactory;
            left = model.left;
            top = model.top;
            width = model.width;
            height = model.height;
            state = model.windowState;

            tabs.CollectionChanged += OnTabsChanged;

            foreach (LayoutWindowTab tabModel in model.tabs)
            {
                AddTab(tabModel);
            }

            if (tabs.NonEmpty())
            {
                currentTab = model.activeTabIndex >= 0 && model.activeTabIndex < tabs.Count ? tabs[model.activeTabIndex] : tabs[0];
            }
        }

        public bool ShowTabBar
        {
            get => showTabBar;
            set => SetProperty(ref showTabBar, value);
        }

        private void OnTabsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            ShowTabBar = tabs.Count > 1;
        }

        private WindowTabItem AddTab(LayoutWindowTab tabModel)
        {
            BrowserTabViewModel vm = tabVmFactory.ForModel(tabModel);
            BrowserTab t = tabFactory.ForViewModel(vm);

            WindowTabItem item = new WindowTabItem(vm, t);

            tabs.Add(item);

            return item;
        }

        public LayoutWindow ToModel() => new LayoutWindow
        {
            left = left,
            top = top,
            width = width,
            height = height,
            windowState = state,
            tabs = tabs.Select(t => t.ViewModel.ToModel()).ToList(),
            activeTabIndex = tabs.IndexOf(currentTab)
        };

        public ObservableCollection<WindowTabItem> Tabs => tabs;

        public WindowTabItem CurrentTab
        {
            get => currentTab;
            set
            {
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
            WindowTabItem ct = currentTab;
            if (ct == null)
            {
                return;
            }

            int tabIndex = Tabs.IndexOf(ct);
            if (tabIndex >= 0)
            {
                Tabs.RemoveAt(tabIndex);
            }
            else
            {
                tabIndex = 0;
            }

            ct.Dispose();

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
            }
        }

        public void OpenNewTab()
        {
            WindowTabItem tab = AddTab(new LayoutWindowTab
            {
                url = "https://duck.com"
            });

            CurrentTab = tab;
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
            WindowTabItem ct = currentTab;
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
            WindowTabItem ct = currentTab;
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