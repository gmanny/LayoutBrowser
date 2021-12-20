using System;
using System.Collections.Generic;
using System.Windows;

namespace LayoutBrowser.Layout
{
    public class LayoutState
    {
        public List<LayoutWindow> windows = new List<LayoutWindow>();

        public bool locked;
        public bool minimizedAll;
        public bool restoreUsingToBack;
    }

    public class LayoutWindow
    {
        public List<LayoutWindowTab> tabs = new List<LayoutWindowTab>();
        public int activeTabIndex;

        public WindowState windowState = WindowState.Normal;
        public WindowState preMinimizedWindowState = WindowState.Normal;
        public double left = 100;
        public double top = 100;
        public double width = 800;
        public double height = 500;
        public double leftNative = Double.NaN, topNative = Double.NaN, widthNative = Double.NaN, heightNative = Double.NaN;

        public bool uiHidden;
        public bool notInLayout;

        public Guid id = Guid.NewGuid();

        public string iconPath;

        public bool? overrideToBack;
    }

    public class LayoutWindowTab
    {
        public string url;
        public bool lockUrl;

        public string title = "New Tab";
        public string overrideTitle;
        public string profile = ProfileManager.DefaultProfile;

        public double zoomFactor = 1;

        public bool hidden;

        public bool autoRefreshEnabled;
        public TimeSpan autoRefreshTime = TimeSpan.Zero;

        public double scrollX, scrollY;
        public bool lockScroll;
        public TimeSpan scrollDelay = TimeSpan.Zero;

        public TabNegativeMargin negativeMargin;

        public bool dontRefreshOnBrowserFail;
    }

    public class TabNegativeMargin
    {
        public bool enabled;
        public double left, top, right, bottom;
        public bool leftRightNativeMode;
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