using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Monitor.ServiceCommon.Services;
using Monitor.ServiceCommon.Util;
using MonitorCommon;
using Newtonsoft.Json;

namespace LayoutBrowser
{
    public class LayoutManager
    {
        private readonly ILayoutBrowserWindowViewModelFactory viewModelFactory;
        private readonly ILayoutBrowserWindowFactory windowFactory;
        private readonly ILogger logger;

        private readonly JsonSerializer ser;

        private readonly List<WindowItem> windows = new List<WindowItem>();

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
            Settings.Default.Save();
        }

        public void RestoreLayout()
        {
            LayoutState state = FromSettings();

            List<LayoutWindow> copy = state.windows.ToList();
            copy.Reverse();

            foreach (LayoutWindow window in copy)
            {
                AddWindow(window);
            }
        }

        public WindowItem AddWindow(LayoutWindow window, bool noActivation = false)
        {
            LayoutBrowserWindowViewModel vm = viewModelFactory.ForModel(window);
            LayoutBrowserWindow w = windowFactory.ForViewModel(vm);
                
            WindowItem item = new WindowItem(vm, w);

            windows.Add(item);

            w.Activated += (s, e) => OnActivated(item, e);
            w.Closed += (s, e) => OnClosed(item, e);
            vm.WindowBecameEmpty += _ => w.Close();
            vm.OpenNewWindow += OnOpenNewWindow;

            if (noActivation)
            {
                w.ShowActivated = false;
            }

            w.Show();

            if (noActivation)
            {
                w.ShowActivated = true;
            }

            return item;
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

            await wnd.ViewModel.CurrentTab.Control.webView.EnsureCoreWebView2Async();
            if (wnd.ViewModel.CurrentTab.Control.webView.CoreWebView2 != null)
            {
                e.NewWindow = wnd.ViewModel.CurrentTab.Control.webView.CoreWebView2;
                e.Handled = true;
            }
        }

        private void OnClosed(WindowItem item, EventArgs e)
        {
            if (stopping)
            {
                return;
            }

            int index = windows.IndexOf(item);

            if (index <= 0)
            {
                return;
            }

            windows.RemoveAt(index);
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