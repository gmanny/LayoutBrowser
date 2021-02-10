using System;
using System.Collections.Generic;
using System.Windows;

namespace LayoutBrowser.Layout
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

        public Guid id = Guid.NewGuid();
    }

    public class LayoutWindowTab
    {
        public string url;
        public string title = "New Tab";
        public string profile = ProfileManager.DefaultProfile;

        public double zoomFactor = 1;

        public bool autoRefreshEnabled;
        public TimeSpan autoRefreshTime = TimeSpan.Zero;

        public double scrollX, scrollY;
        public TimeSpan scrollDelay = TimeSpan.Zero;
    }

    public class ClosedItemHistory
    {
        // oldest -> newest
        public List<IClosedItem> closedItems = new List<IClosedItem>();
    }

    public interface IClosedItem { }

    public class ClosedLayoutWindow : IClosedItem
    {
        public LayoutWindow window;
    }

    public class ClosedLayoutTab : IClosedItem
    {
        public LayoutWindowTab tab;

        public Guid windowId;
        public int tabPosition;
    }
}