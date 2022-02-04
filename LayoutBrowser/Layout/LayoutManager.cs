using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using LanguageExt;
using LayoutBrowser.Window;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Monitor.ServiceCommon.Services;
using Monitor.ServiceCommon.Util;
using MonitorCommon;
using MonitorCommon.Tasks;
using Newtonsoft.Json;

namespace LayoutBrowser.Layout;

public class LayoutManager
{
    private readonly ILayoutBrowserWindowViewModelFactory viewModelFactory;
    private readonly ILayoutBrowserWindowFactory windowFactory;
    private readonly ILogger logger;
    private readonly App app;

    private readonly JsonSerializer ser;

    private readonly List<WindowItem> windows = new();
    private readonly ConcurrentDictionary<Guid, WindowItem> windowHash = new();

    private ClosedItemHistory closedItems = new();

    private bool layoutLocked;
    private bool minimizedAll;
    private bool layoutRestoreUsingToBack;
    private bool storeClosedHistory;
    private bool darkMode;

    private bool stopping;

    private bool inMinimizeAll;

    public LayoutManager(ILayoutBrowserWindowViewModelFactory viewModelFactory, ILayoutBrowserWindowFactory windowFactory, 
        JsonSerializerSvc serSvc, ProcessLifetimeSvc lifetimeSvc, ILogger logger, App app)
    {
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        this.viewModelFactory = viewModelFactory;
        this.windowFactory = windowFactory;
        this.logger = logger;
        this.app = app;

        ser = serSvc.Serializer;

        lifetimeSvc.ApplicationStop += OnAppStop;
    }

    public bool LayoutLocked
    {
        get => layoutLocked;
        set
        {
            layoutLocked = value;

            SaveLayout();
        }
    }

    public bool LayoutRestoreUsingToBack
    {
        get => layoutRestoreUsingToBack;
        set => layoutRestoreUsingToBack = value;
    }

    public bool StoreClosedHistory
    {
        get => storeClosedHistory;
        set => storeClosedHistory = value;
    }

    public bool DarkMode
    {
        get => darkMode;
        set => darkMode = value;
    }

    /// <summary>
    /// !Warning! Exposed for development purposes only!
    /// </summary>
    public IEnumerable<WindowItem> Windows => windows;

    private Task OnAppStop()
    {
        stopping = true;

        SaveLayout();

        return Task.CompletedTask;
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
            try
            {
                state = ser.Deserialize<LayoutState>(Settings.Default.Layout) ?? new LayoutState();
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Failed to deserialize layout");

                state = new LayoutState();
            }
        }

        if (state.windows.IsEmpty())
        {
            state.windows.Add(new LayoutWindow());
        }

        if (state.windows.Count == 1 && state.windows[0].tabs.IsEmpty())
        {
            state.windows[0].tabs.Add(
                new LayoutWindowTab()
            );
        }

        return state;
    }

    public Task UpdateNativeSizes()
    {
        List<Task> tasks = new();

        foreach (WindowItem wnd in windows)
        {
            TaskCompletionSource<Unit> tcs = new();

            wnd.Window.Dispatcher.BeginInvoke(() =>
            {
                wnd.ViewModel.UpdateNativeSize();

                tcs.SetResult(Unit.Default);
            }, DispatcherPriority.Background);

            tasks.Add(tcs.Task);
        }

        return Task.WhenAll(tasks);
    }

    public void SaveLayout()
    {
        LayoutState state = new()
        {
            windows = windows.Select(w => w.ViewModel.ToModel()).ToList(),
            locked = layoutLocked,
            minimizedAll = minimizedAll,
            restoreUsingToBack = layoutRestoreUsingToBack,
            storeClosedHistory = storeClosedHistory,
            useLightMode = !darkMode
        };

        Settings.Default.Layout = ser.Serialize(state);
        Settings.Default.ClosedHistory = storeClosedHistory ? ser.Serialize(closedItems) : null;

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
            closedItems = ser.Deserialize<ClosedItemHistory>(Settings.Default.ClosedHistory) ?? new ClosedItemHistory();
        }

        LayoutState state = FromSettings();

        LayoutRestoreUsingToBack = state.restoreUsingToBack;
        storeClosedHistory = state.storeClosedHistory;
        darkMode = !state.useLightMode;

        List<LayoutWindow> copy = state.windows.ToList();
        copy.Reverse();

        List<Task> layoutRestoreComplete = new(copy.Count);
        foreach (LayoutWindow window in copy)
        {
            (_, Task layoutComplete) = AddWindow(window);

            layoutRestoreComplete.Add(layoutComplete);

            logger.LogInformation($"Restored window {window.id}");
        }

        layoutLocked = state.locked;
        minimizedAll = state.minimizedAll;

        Task.WhenAll(layoutRestoreComplete).OnComplete(_ =>
        {
            logger.LogInformation("Layout fully restored");
                
            //SaveLayout();
        });
    }
        
    public (WindowItem wnd, Task layoutComplete) AddWindow(LayoutWindow window, bool noActivation = false)
    {
        LayoutBrowserWindowViewModel vm = viewModelFactory.ForModel(window);
        LayoutBrowserWindow w = windowFactory.ForViewModel(vm);
                
        WindowItem item = new(vm, w);

        windows.Add(item);
        if (!windowHash.TryAdd(item.ViewModel.Id, item))
        {
            logger.LogDebug($"Found window with duplicate ID {item.ViewModel.Id}");
        }

        TaskCompletionSource<Unit> layoutComplete = new();

        w.Activated += (_, _) => OnActivated(item);
        w.Closed += (_, _) => OnClosed(item);
        w.LayoutRestoreComplete += () => layoutComplete.SetResult(Unit.Default);
        vm.WindowBecameEmpty += _ => w.Close();
        vm.OpenNewWindow += OnOpenNewWindow;
        vm.PopoutRequested += (wn, t) => PopoutTab(t, wn);
        vm.TabClosed += OnWindowTabClosed;

        if (noActivation)
        {
            w.ShowActivated = false;
        }

        w.Show();

        return (item, layoutComplete.Task);
    }
        
    private void RestoreWindowOrder(WindowItem item)
    {
        if (inMinimizeAll)
        {
            // don't do window ordering inside a bulk operation
            return;
        }

        int index = windows.IndexOf(item);
        if (index < 0)
        {
            return;
        }

        void RestoreLayoutFront()
        {
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                WindowItem wnd = windows[i];
                if (wnd.ViewModel.NotInLayout ||
                    (wnd.ViewModel.OverrideLayoutMethod ? wnd.ViewModel.OverrideLayoutUsingToBack : layoutRestoreUsingToBack))
                {
                    continue;
                }

                wnd.Window.BringToFrontWithoutFocus();
            }
        }

        void RestoreLayoutBack()
        {
            for (int i = 0; i < windows.Count; i++)
            {
                WindowItem wnd = windows[i];
                if (wnd.ViewModel.NotInLayout ||
                    !(wnd.ViewModel.OverrideLayoutMethod ? wnd.ViewModel.OverrideLayoutUsingToBack : layoutRestoreUsingToBack))
                {
                    continue;
                }
                    
                wnd.Window.BringToBack();
            }
        }

        void RestoreLayoutGen()
        {
            RestoreLayoutBack();
            RestoreLayoutFront();
        }

        RestoreLayoutGen();

        // repeat after delay because it sometimes bugs out :\
        windows[index].Window.Dispatcher.BeginInvoke(RestoreLayoutGen, DispatcherPriority.Background);
    }

    public void MinimizeAll()
    {
        try
        {
            inMinimizeAll = true;

            foreach (WindowItem wnd in windows)
            {
                if (wnd.ViewModel.NotInLayout)
                {
                    continue;
                }

                wnd.ViewModel.Minimize();
            }
        }
        finally
        {
            inMinimizeAll = false;
        }

        minimizedAll = true;
    }

    public void RestoreAll()
    {
        if (!minimizedAll)
        {
            return;
        }

        try
        {
            inMinimizeAll = true;

            foreach (WindowItem wnd in Enumerable.Reverse(windows))
            {
                if (wnd.ViewModel.NotInLayout)
                {
                    continue;
                }

                wnd.ViewModel.Restore();
                wnd.Window.Activate();
            }
        }
        finally
        {
            inMinimizeAll = false;
        }

        minimizedAll = false;
    }

    private void OnWindowTabClosed(LayoutBrowserWindowViewModel wnd, WindowTabItem tab, int tabIndex)
    {
        LayoutWindowTab tabModel = tab.ViewModel.ToModel();
        if (tabModel.url.IsNullOrEmpty())
        {
            return;
        }

        closedItems.closedItems.Add(new ClosedLayoutTab(
            tab: tabModel,
            tabPosition: tabIndex,
            windowId: wnd.Id
        ));

        TrimClosedItems();
    }

    private void PopoutTab(WindowTabItem item, LayoutBrowserWindowViewModel parentWindow)
    {
        (WindowItem wnd, _) = AddWindow(new LayoutWindow
        {
            left = parentWindow.Left + 30,
            top = parentWindow.Top + 30,
            width = parentWindow.Width,
            height = parentWindow.Height,
            windowState = WindowState.Normal,
            notInLayout = layoutLocked || minimizedAll
        });

        wnd.ViewModel.AddForeignTab(item, true);
    }

    private async Task OnOpenNewWindow(WindowTabItem? item, LayoutBrowserWindowViewModel parentWindow, CoreWebView2NewWindowRequestedEventArgs? e, bool foreground)
    {
        (WindowItem wnd, _) = AddWindow(new LayoutWindow
        {
            tabs =
            {
                new LayoutWindowTab
                {
                    profile = item?.ViewModel.Profile.Name ?? ProfileManager.DefaultProfile,
                    url = null,
                    title = "New Tab"
                }
            },
            left = parentWindow.Left + 30,
            top = parentWindow.Top + 30,
            width = parentWindow.Width,
            height = parentWindow.Height,
            windowState = WindowState.Normal,
            notInLayout = layoutLocked || minimizedAll
        }, !foreground);

        WindowTabItem? ct = wnd.ViewModel.CurrentTab;
        if (ct != null) {
            if (e != null)
            {
                await ct.Control.webView.EnsureCoreWebView2Async();
                if (ct.Control.webView.CoreWebView2 != null)
                {
                    e.NewWindow = ct.Control.webView.CoreWebView2;
                    e.Handled = true;
                }
            }
            else
            {
                await wnd.Window.Dispatcher.BeginInvoke(() =>
                {
                    ct.Control.urlBar.Focus();
                }, DispatcherPriority.Background);
            }
        }
    }

    private void OnClosed(WindowItem item)
    {
        if (stopping)
        {
            return;
        }

        if (item.ViewModel.Tabs.NonEmpty())
        {
            closedItems.closedItems.Add(new ClosedLayoutWindow(item.ViewModel.ToModel()));

            TrimClosedItems();
        }

        int index = windows.IndexOf(item);

        if (index < 0)
        {
            return;
        }

        windows.RemoveAt(index);
        if (!windowHash.TryRemove(item.ViewModel.Id, out _))
        {
            logger.LogDebug($"Couldn't find window with id {item.ViewModel.Id} in window hash");
        }

        if (windows.IsEmpty())
        {
            app.Shutdown();
        }
    }

    private void TrimClosedItems()
    {
        if (closedItems.closedItems.Count > 3000)
        {
            closedItems.closedItems = closedItems.closedItems.Skip(1000).ToList();
        }
    }

    private void OnActivated(WindowItem item)
    {
        if (inMinimizeAll)
        {
            return;
        }

        if (!inMinimizeAll && minimizedAll && !item.ViewModel.NotInLayout)
        {
            item.Window.Dispatcher.BeginInvoke(() =>
            {
                RestoreAll();

                item.Window.Activate();
            }, DispatcherPriority.Background);
        }

        if (layoutLocked && !item.ViewModel.NotInLayout)
        {
            RestoreWindowOrder(item);
            return;
        }

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
                if (!windowHash.TryGetValue(tab.windowId, out WindowItem? wnd))
                {
                    if (windows.NonEmpty())
                    {
                        // last activated window (current window)
                        wnd = windows[0];
                    }
                    else
                    {
                        (wnd, _) = AddWindow(new LayoutWindow());
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

public record WindowItem(LayoutBrowserWindowViewModel ViewModel, LayoutBrowserWindow Window);