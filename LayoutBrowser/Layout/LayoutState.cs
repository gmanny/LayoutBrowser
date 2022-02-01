using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace LayoutBrowser.Layout
{
    public class LayoutState
    {
        public List<LayoutWindow> windows = new();

        public bool locked;
        public bool minimizedAll;
        public bool restoreUsingToBack;
        public bool storeClosedHistory;
        public bool useLightMode;
    }

    public class LayoutWindow
    {
        public List<LayoutWindowTab> tabs = new();
        public int activeTabIndex;

        [DefaultValue(WindowState.Normal)]
        public WindowState windowState = WindowState.Normal;

        [DefaultValue(WindowState.Normal)]
        public WindowState preMinimizedWindowState = WindowState.Normal;

        [DefaultValue(100d)]
        public double left = 100;
        
        [DefaultValue(100d)]
        public double top = 100;
        
        [DefaultValue(800d)]
        public double width = 800;
        
        [DefaultValue(500d)]
        public double height = 500;
        
        [DefaultValue(Double.NaN)]
        public double leftNative = Double.NaN, topNative = Double.NaN, widthNative = Double.NaN, heightNative = Double.NaN;

        public bool uiHidden;
        public bool notInLayout;

        public Guid id = Guid.NewGuid();

        public string iconPath;

        public bool? overrideToBack;

        public LayoutWindow Copy() => new()
        {
            tabs = tabs.Select(t => t.Copy()).ToList(),
            activeTabIndex = activeTabIndex,
            windowState = windowState,
            preMinimizedWindowState = preMinimizedWindowState,
            left = left,
            top = top,
            width = width,
            height = height,
            leftNative = leftNative,
            topNative = topNative,
            widthNative = widthNative,
            heightNative = heightNative,
            uiHidden = uiHidden,
            notInLayout = notInLayout,
            // skipping id copy
            iconPath = iconPath,
            overrideToBack = overrideToBack
        };
    }

    public class LayoutWindowTab
    {
        public string url;
        public bool lockUrl;

        public string title = "New Tab";
        public string overrideTitle;
        
        [DefaultValue(ProfileManager.DefaultProfile)]
        public string profile = ProfileManager.DefaultProfile;

        [DefaultValue(1d)]
        public double zoomFactor = 1;

        public bool hidden;

        public bool autoRefreshEnabled;
        public TimeSpan autoRefreshTime = TimeSpan.Zero;

        public double scrollX, scrollY;
        public bool lockScroll;
        public TimeSpan scrollDelay = TimeSpan.Zero;

        public TabNegativeMargin negativeMargin;

        public bool dontRefreshOnBrowserFail;

        public ElementBlockingSettings elementBlocking;

        public LayoutWindowTab Copy() => new()
        {
            url = url,
            lockUrl = lockUrl,
            title = title,
            overrideTitle = overrideTitle,
            profile = profile,
            zoomFactor = zoomFactor,
            hidden = hidden,
            autoRefreshEnabled = autoRefreshEnabled,
            autoRefreshTime = autoRefreshTime,
            scrollX = scrollX,
            scrollY = scrollY,
            scrollDelay = scrollDelay,
            lockScroll = lockScroll,
            negativeMargin = negativeMargin.Copy(),
            dontRefreshOnBrowserFail = dontRefreshOnBrowserFail,
            elementBlocking = elementBlocking.Copy()
        };
    }

    public class TabNegativeMargin
    {
        public bool enabled;
        public double left, top, right, bottom;
        public bool leftRightNativeMode;

        public TabNegativeMargin Copy() => new()
        {
            enabled = enabled,
            left = left,
            top = top,
            right = right,
            bottom = bottom
        };
    }

    public class ElementBlockingSettings
    {
        public bool enabled;
        public List<ElementBlockingRule> rules;

        public ElementBlockingSettings Copy() => new()
        {
            enabled = enabled,
            rules = rules.Select(r => r.Copy()).ToList()
        };
    }

    public class ElementBlockingRule
    {
        public bool enabled;
        public string selector;

        public ElementBlockingRule Copy() => new()
        {
            enabled = enabled,
            selector = selector
        };
    }

    public class ClosedItemHistory
    {
        // oldest -> newest
        public List<IClosedItem> closedItems = new();
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