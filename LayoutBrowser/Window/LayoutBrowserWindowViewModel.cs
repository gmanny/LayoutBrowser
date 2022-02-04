using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LanguageExt;
using LayoutBrowser.Layout;
using LayoutBrowser.Tab;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using MonitorCommon;
using MvvmHelpers;
using WpfAppCommon;

namespace LayoutBrowser.Window;

public interface ILayoutBrowserWindowViewModelFactory
{
    public LayoutBrowserWindowViewModel ForModel(LayoutWindow model);
}

public class LayoutBrowserWindowViewModel : ObservableObject
{
    private static int windowIndex;

    private readonly IBrowserTabFactory tabFactory;
    private readonly IBrowserTabViewModelFactory tabVmFactory;
    private readonly ILogger logger;
    private readonly IWpfAppEntryPoint entryPoint;
    private readonly LayoutManagerViewModel layoutMgr;
    private readonly ObservableCollection<WindowTabItem> tabs = new();
    private readonly ObservableCollection<WindowTabItem> backgroundLoading = new();

    private readonly Guid id;
    private readonly int myIndex = Interlocked.Increment(ref windowIndex);

    private readonly ICommand changeIconCommand;
    private readonly ICommand quitCommand;

    private double left, top, width, height;
    private readonly double leftInit, topInit, widthInit, heightInit;
    private double leftNative, topNative, widthNative, heightNative;
    private readonly double leftNativeInit, topNativeInit, widthNativeInit, heightNativeInit;
    private WindowState state;
    private WindowState preMinState;
    private WindowTabItem? currentTab;
    private bool showTabBar;
    private bool uiHidden;
    private bool notInLayout;
    private bool backgroundLoadEnabled;
    private string? iconPath;
    private bool overrideLayoutMethod;
    private bool overrideLayoutUsingToBack;

    public LayoutBrowserWindowViewModel(LayoutWindow model, IBrowserTabFactory tabFactory, IBrowserTabViewModelFactory tabVmFactory, ILogger logger, IWpfAppEntryPoint entryPoint,
        LayoutManagerViewModel layoutMgr)
    {
        this.tabFactory = tabFactory;
        this.tabVmFactory = tabVmFactory;
        this.logger = logger;
        this.entryPoint = entryPoint;
        this.layoutMgr = layoutMgr;

        id = model.id;

        leftInit = left = model.left;
        topInit = top = model.top;
        widthInit = width = model.width;
        heightInit = height = model.height;
        leftNativeInit = leftNative = model.leftNative;
        topNativeInit = topNative = model.topNative;
        widthNativeInit = widthNative = model.widthNative;
        heightNativeInit = heightNative = model.heightNative;
        state = model.windowState;
        preMinState = model.preMinimizedWindowState;
        uiHidden = model.uiHidden;
        notInLayout = model.notInLayout;
        iconPath = model.iconPath;
        overrideLayoutMethod = model.overrideToBack != null;
        if (model.overrideToBack != null)
        {
            overrideLayoutUsingToBack = model.overrideToBack.Value;
        }

        tabs.CollectionChanged += OnTabsChanged;

        changeIconCommand = new WindowCommand(PickNewIcon);
        quitCommand = new WindowCommand(Quit);

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

    public ICommand ChangeIconCommand => changeIconCommand;
    public ICommand QuitCommand => quitCommand;

    public event Func<Rectangle>? NativeRect;
        
    public double LeftInit => leftInit;
    public double TopInit => topInit;
    public double WidthInit => widthInit;
    public double HeightInit => heightInit;

    public double LeftNativeInit => leftNativeInit;
    public double TopNativeInit => topNativeInit;
    public double WidthNativeInit => widthNativeInit;
    public double HeightNativeInit => heightNativeInit;

    public double LeftNative => leftNative;
    public double TopNative => topNative;
    public double WidthNative => widthNative;
    public double HeightNative => heightNative;

    public LayoutManagerViewModel LayoutMgr => layoutMgr;

    // debug prop
    public string UrlList => tabs.Select(t => t.ViewModel.UrlVm.Url).CommaString();

    private void OnFirstNavComplete(BrowserTabViewModel vm)
    {
        BackgroundLoadEnabled = true;

        vm.NavigationCompleted -= OnFirstNavComplete;
    }

    public Guid Id => id;
    public int Index => myIndex;

    public bool ShowDebugInfo => entryPoint.ShowConsole;

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

    public bool NotInLayout
    {
        get => notInLayout;
        set => SetProperty(ref notInLayout, value);
    }

    public string? IconPath
    {
        get => iconPath;
        set => SetProperty(ref iconPath, value);
    }

    public bool UiVisible => !uiHidden;

    public bool OverrideLayoutMethod
    {
        get => overrideLayoutMethod;
        set => SetProperty(ref overrideLayoutMethod, value);
    }

    public bool OverrideLayoutUsingToBack
    {
        get => overrideLayoutUsingToBack;
        set => SetProperty(ref overrideLayoutUsingToBack, value);
    }

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
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

        WindowTabItem item = new(vm, t);

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

        item.ViewModel.ChangeParent(this);

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
            logger.LogDebug($"Tab {tabItem.ViewModel.UrlVm.Url} requested to close itself");
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

    public void Minimize()
    {
        if (state == WindowState.Minimized)
        {
            return;
        }

        preMinState = state;

        State = WindowState.Minimized;
    }

    public void Restore()
    {
        if (state != WindowState.Minimized)
        {
            return;
        }

        State = preMinState;
    }

    private async Task OnOpenNewWindow(WindowTabItem? itm, CoreWebView2NewWindowRequestedEventArgs? e, bool foreground)
    {
        Task? result = OpenNewWindow?.Invoke(itm, this, e, foreground);
        if (result != null)
        {
            await result;
        }
    }

    public event Func<WindowTabItem?, LayoutBrowserWindowViewModel, CoreWebView2NewWindowRequestedEventArgs?, bool, Task>? OpenNewWindow;

    public LayoutWindow ToModel() => new()
    {
        id = id,
        left = left,
        top = top,
        width = width,
        height = height,
        leftNative = leftNative,
        topNative = topNative,
        widthNative = widthNative,
        heightNative = heightNative,
        windowState = state,
        preMinimizedWindowState = preMinState,
        uiHidden = uiHidden,
        notInLayout = notInLayout,
        tabs = tabs.Select(t => t.ViewModel.ToModel()).ToList(),
        activeTabIndex = currentTab == null ? 0 : tabs.IndexOf(currentTab),
        iconPath = iconPath,
        overrideToBack = overrideLayoutMethod ? overrideLayoutUsingToBack : null
    };

    public ObservableCollection<WindowTabItem> Tabs => tabs;
    public ObservableCollection<WindowTabItem>? BackgroundLoading => backgroundLoadEnabled ? backgroundLoading : null;

    public bool BackgroundLoadEnabled
    {
        get => backgroundLoadEnabled;
        set
        {
            SetProperty(ref backgroundLoadEnabled, value);
            OnPropertyChanged(nameof(BackgroundLoading));
        }
    }

    public WindowTabItem? CurrentTab
    {
        get => currentTab;
        set
        {
            if (value != null)
            {
                backgroundLoading.Remove(value);
            }

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
        set
        {
            SetProperty(ref left, value);

            UpdateNativeSize();
        }
    }

    public double Top
    {
        get => top;
        set
        {
            SetProperty(ref top, value);

            UpdateNativeSize();
        }
    }

    public double Width
    {
        get => width;
        set
        {
            SetProperty(ref width, value);

            UpdateNativeSize();
        }
    }

    public double Height
    {
        get => height;
        set
        {
            SetProperty(ref height, value);

            UpdateNativeSize();
        }
    }

    public void UpdateNativeSize()
    {
        Rectangle? nativeRect = NativeRect?.Invoke();

        if (nativeRect == null || nativeRect == new Rectangle())
        {
            return;
        }

        Rectangle nr = nativeRect.Value;

        leftNative = nr.Left;
        topNative = nr.Top;
        widthNative = nr.Width;
        heightNative = nr.Height;
    }

    public WindowState State
    {
        get => state;
        set
        {
            if (state == WindowState.Minimized && value == WindowState.Normal && preMinState == WindowState.Maximized)
            {
                value = WindowState.Maximized;
            }

            SetProperty(ref state, value);
        }
    }

    public void CloseCurrentTab()
    {
        if (tabs.Count <= 1)
        {
            CloseWindow();
        }
        else if (currentTab != null)
        {
            CloseTab(currentTab);
        }
    }

    public event Action? WindowCloseRequested;

    private void CloseWindow()
    {
        WindowCloseRequested?.Invoke();
    }

    public event Action<LayoutBrowserWindowViewModel, WindowTabItem, int>? TabClosed;

    public void CloseTab(WindowTabItem tab, bool doDispose = true)
    {
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

    public event Action<LayoutBrowserWindowViewModel>? WindowBecameEmpty;

    protected virtual void OnWindowBecameEmpty()
    {
        WindowBecameEmpty?.Invoke(this);
    }

    public void OpenNewTab(string? profile = null)
    {
        WindowTabItem tab = AddTab(new LayoutWindowTab
        {
            url = null,
            profile = profile ?? currentTab?.ViewModel.Profile.Name ?? ProfileManager.DefaultProfile
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
        WindowTabItem? ct = CurrentTab;
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

        idx %= Tabs.Count;

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
        WindowTabItem? ct = CurrentTab;
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

        idx %= Tabs.Count;

        Tabs.Move(tabIndex, idx);
    }

    public static void Quit()
    {
        Environment.Exit(0);
    }

    public void StopLoading()
    {
        currentTab?.Control.webView.Stop();
    }

    public void Refresh()
    {
        currentTab?.ViewModel.UrlVm.Refresh();
    }

    public void FocusAddressBar()
    {
        WindowTabItem? ct = currentTab;
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
        
    public event Action<LayoutBrowserWindowViewModel, WindowTabItem>? PopoutRequested;

    public void RequestPopout()
    {
        if (tabs.Count <= 1)
        {
            logger.LogInformation("Preventing tab pop-out because window doesn't have enough tabs left");

            return;
        }

        WindowTabItem? tab = CurrentTab;
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

    public void PickNewIcon()
    {
        OpenFileDialog ofd = new()
        {
            CheckFileExists = true,
            DefaultExt = ".ico",
            Filter = "Icon Files (*.ico)|*.ico",
            FilterIndex = 1,
            Multiselect = false,
            Title = "Please select a new window icon"
        };

        if (iconPath.IsNullOrEmpty())
        {
            string? entryLocation = Assembly.GetEntryAssembly()?.Location;
            if (!entryLocation.IsNullOrEmpty())
            {
                string? entryDir = Path.GetDirectoryName(entryLocation);
                if (entryDir != null)
                {
                    string wholeDir = Path.Combine(entryDir, "Resources", "Icons", "crystal-clear-icons-by-everaldo", "ico");
                    ofd.InitialDirectory = wholeDir;
                }
            }
        }
        else
        {
            ofd.FileName = iconPath;
        }

        if (ofd.ShowDialog() != true || !File.Exists(ofd.FileName))
        {
            return;
        }

        IconPath = ofd.FileName;
    }
}

public record WindowTabItem(BrowserTabViewModel ViewModel, BrowserTab Control) : IDisposable
{
    public void Dispose()
    {
        ViewModel.Dispose();
        Control.Dispose();

        GC.SuppressFinalize(this);
    }
}