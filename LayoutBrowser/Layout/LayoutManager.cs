using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using LayoutBrowser.Window;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Monitor.ServiceCommon.Services;
using Monitor.ServiceCommon.Util;
using MonitorCommon;
using Newtonsoft.Json;

namespace LayoutBrowser.Layout
{
    public class LayoutManager
    {
        private readonly ILayoutBrowserWindowViewModelFactory viewModelFactory;
        private readonly ILayoutBrowserWindowFactory windowFactory;
        private readonly ILogger logger;

        private readonly JsonSerializer ser;

        private readonly List<WindowItem> windows = new List<WindowItem>();
        private readonly ConcurrentDictionary<Guid, WindowItem> windowHash = new ConcurrentDictionary<Guid, WindowItem>();

        private ClosedItemHistory closedItems;

        private bool stopping;

        public LayoutManager(ILayoutBrowserWindowViewModelFactory viewModelFactory, ILayoutBrowserWindowFactory windowFactory, JsonSerializerSvc serSvc, ProcessLifetimeSvc lifetimeSvc, ILogger logger)
        {
            this.viewModelFactory = viewModelFactory;
            this.windowFactory = windowFactory;
            this.logger = logger;

            ser = serSvc.Serializer;

            lifetimeSvc.ApplicationStop += OnAppStop;
        }

        private async Task OnAppStop()
        {
            stopping = true;

            SaveLayout();
        }

        public LayoutState FromSettings()
        {
            LayoutState state;

            if (Settings.Default.Layout.IsNullOrEmpty())
            {
                state = new LayoutState();
            }
            else
            {
                state = ser.Deserialize<LayoutState>(Settings.Default.Layout);
            }

            if (state.windows.IsEmpty())
            {
                state.windows.Add(new LayoutWindow());
            }

            if (state.windows.Count == 1 && state.windows[0].tabs.IsEmpty())
            {
                state.windows[0].tabs.Add(
                    new LayoutWindowTab { url = "https://duck.com" }
                );
            }

            return state;
        }

        public void SaveLayout()
        {
            LayoutState state = new LayoutState
            {
                windows = windows.Select(w => w.ViewModel.ToModel()).ToList()
            };

            Settings.Default.Layout = ser.Serialize(state);
            Settings.Default.ClosedHistory = ser.Serialize(closedItems);
            Settings.Default.Save();
        }

        public void RestoreLayout()
        {
            if (Settings.Default.ClosedHistory.IsNullOrEmpty())
            {
                closedItems = new ClosedItemHistory();
            }
            else
            {
                closedItems = ser.Deserialize<ClosedItemHistory>(Settings.Default.ClosedHistory);
            }

            LayoutState state = FromSettings();

            List<LayoutWindow> copy = state.windows.ToList();
            copy.Reverse();

            foreach (LayoutWindow window in copy)
            {
                AddWindow(window);

                logger.LogInformation($"Restored window {window.id}");
            }
        }

        public WindowItem AddWindow(LayoutWindow window, bool noActivation = false)
        {
            LayoutBrowserWindowViewModel vm = viewModelFactory.ForModel(window);
            LayoutBrowserWindow w = windowFactory.ForViewModel(vm);
                
            WindowItem item = new WindowItem(vm, w);

            windows.Add(item);
            if (!windowHash.TryAdd(item.ViewModel.Id, item))
            {
                logger.LogDebug($"Found window with duplicate ID {item.ViewModel.Id}");
            }

            w.Activated += (s, e) => OnActivated(item, e);
            w.Closed += (s, e) => OnClosed(item, e);
            vm.WindowBecameEmpty += _ => w.Close();
            vm.OpenNewWindow += OnOpenNewWindow;
            vm.PopoutRequested += (w, t) => PopoutTab(t, w);
            vm.TabClosed += OnWindowTabClosed;

            if (noActivation)
            {
                w.ShowActivated = false;
            }

            w.Show();

            return item;
        }

        private void OnWindowTabClosed(LayoutBrowserWindowViewModel wnd, WindowTabItem tab, int tabIndex)
        {
            closedItems.closedItems.Add(new ClosedLayoutTab
            {
                tab = tab.ViewModel.ToModel(),
                tabPosition = tabIndex,
                windowId = wnd.Id
            });

            TrimClosedItems();
        }

        private void PopoutTab(WindowTabItem item, LayoutBrowserWindowViewModel parentWindow)
        {
            WindowItem wnd = AddWindow(new LayoutWindow
            {
                left = parentWindow.Left + 30,
                top = parentWindow.Top + 30,
                width = parentWindow.Width,
                height = parentWindow.Height,
                windowState = WindowState.Normal
            });

            wnd.ViewModel.AddForeignTab(item, true);
        }

        private async Task OnOpenNewWindow(WindowTabItem item, LayoutBrowserWindowViewModel parentWindow, CoreWebView2NewWindowRequestedEventArgs e, bool foreground)
        {
            WindowItem wnd = AddWindow(new LayoutWindow
            {
                tabs =
                {
                    new LayoutWindowTab
                    {
                        profile = item.ViewModel.Profile.Name,
                        url = null,
                        title = "New Tab"
                    }
                },
                left = parentWindow.Left + 30,
                top = parentWindow.Top + 30,
                width = parentWindow.Width,
                height = parentWindow.Height,
                windowState = WindowState.Normal
            }, !foreground);

            if (e != null)
            {
                await wnd.ViewModel.CurrentTab.Control.webView.EnsureCoreWebView2Async();
                if (wnd.ViewModel.CurrentTab.Control.webView.CoreWebView2 != null)
                {
                    e.NewWindow = wnd.ViewModel.CurrentTab.Control.webView.CoreWebView2;
                    e.Handled = true;
                }
            }
            else
            {
                await wnd.Window.Dispatcher.BeginInvoke(() =>
                {
                    wnd.ViewModel.CurrentTab.Control.urlBar.Focus();
                }, DispatcherPriority.Background);
            }
        }

        private void OnClosed(WindowItem item, EventArgs e)
        {
            if (stopping)
            {
                return;
            }

            if (item.ViewModel.Tabs.NonEmpty())
            {
                closedItems.closedItems.Add(new ClosedLayoutWindow
                {
                    window = item.ViewModel.ToModel()
                });

                TrimClosedItems();
            }

            int index = windows.IndexOf(item);

            if (index <= 0)
            {
                return;
            }

            windows.RemoveAt(index);
            if (!windowHash.TryRemove(item.ViewModel.Id, out _))
            {
                logger.LogDebug($"Couldn't find window with id {item.ViewModel.Id} in window hash");
            }
        }

        private void TrimClosedItems()
        {
            if (closedItems.closedItems.Count > 3000)
            {
                closedItems.closedItems = closedItems.closedItems.Skip(1000).ToList();
            }
        }

        private void OnActivated(WindowItem item, EventArgs e)
        {
            int index = windows.IndexOf(item);
            if (index == 0)
            {
                return;
            }

            if (index <= 0)
            {
                // window is orphan, probably a closed one
                return;
            }

            // push window item to top
            windows.RemoveAt(index);
            windows.Insert(0, item);
        }

        public void ReopenLastClosedItem()
        {
            if (closedItems.closedItems.IsEmpty())
            {
                return;
            }

            IClosedItem lastItem = closedItems.closedItems[^1];
            closedItems.closedItems.RemoveAt(closedItems.closedItems.Count - 1);

            switch (lastItem)
            {
                case ClosedLayoutWindow wnd:
                {
                    AddWindow(wnd.window);
                } break;

                case ClosedLayoutTab tab:
                {
                    if (!windowHash.TryGetValue(tab.windowId, out WindowItem wnd))
                    {
                        if (windows.NonEmpty())
                        {
                            // last activated window (current window)
                            wnd = windows[0];
                        }
                        else
                        {
                            wnd = AddWindow(new LayoutWindow());
                        }
                    }

                    WindowTabItem tabItem = wnd.ViewModel.AddTab(tab.tab, tab.tabPosition);
                    wnd.ViewModel.CurrentTab = tabItem;

                    wnd.Window.Activate();
                } break;

                default:
                    logger.LogWarning($"Unknown type of closed item encountered: {lastItem.GetType().FullName} / {lastItem}");
                    break;
            }
        }
    }

    public class WindowItem
    {
        public WindowItem(LayoutBrowserWindowViewModel viewModel, LayoutBrowserWindow window)
        {
            ViewModel = viewModel;
            Window = window;
        }

        public LayoutBrowserWindowViewModel ViewModel { get; set; }
        public LayoutBrowserWindow Window { get; set; }
    }
}