using System;
using System.Collections.Generic;

namespace LayoutBrowser
{
    public class LayoutState
    {
        public List<LayoutWindow> windows;
    }

    public class LayoutWindow
    {
        public List<LayoutWindowTab> tabs;

        public double left;
        public double top;
        public double width;
        public double height;
    }

    public class LayoutWindowTab
    {
        public string url;
        public string profile;

        public double zoomFactor;

        public TimeSpan? autoRefresh;
    }
}