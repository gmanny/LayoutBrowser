using System;
using System.Collections.Generic;
using System.Windows;

namespace LayoutBrowser
{
    public class LayoutState
    {
        public List<LayoutWindow> windows = new List<LayoutWindow>();
    }

    public class LayoutWindow
    {
        public List<LayoutWindowTab> tabs = new List<LayoutWindowTab>();
        public int activeTabIndex;

        public WindowState windowState = WindowState.Normal;
        public double left = 100;
        public double top = 100;
        public double width = 800;
        public double height = 500;

        public bool uiHidden;
    }

    public class LayoutWindowTab
    {
        public string url;
        public string title = "New Tab";
        public string profile = "default";

        public double zoomFactor = 1;

        public TimeSpan? autoRefresh;
    }
}