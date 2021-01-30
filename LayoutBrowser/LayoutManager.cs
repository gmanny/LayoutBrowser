using System.Collections.Generic;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace LayoutBrowser
{
    public class LayoutManager
    {
        private readonly ILayoutBrowserWindowViewModelFactory viewModelFactory;
        private readonly ILayoutBrowserWindowFactory windowFactory;
        private readonly ILogger logger;

        public LayoutManager(ILayoutBrowserWindowViewModelFactory viewModelFactory, ILayoutBrowserWindowFactory windowFactory, ILogger logger)
        {
            this.viewModelFactory = viewModelFactory;
            this.windowFactory = windowFactory;
            this.logger = logger;
        }

        public void RestoreLayout()
        {
            var windows = new List<LayoutWindow>
            {
                new LayoutWindow
                {
                    left = 200, top = 100, width = 1000, height = 600,
                    windowState = WindowState.Normal,
                    tabs = new List<LayoutWindowTab>
                    {
                        new LayoutWindowTab
                        {
                            url = "https://bing.com",
                            profile = "FirstProfile",
                            zoomFactor = 1
                        },
                        new LayoutWindowTab
                        {
                            url = "https://duck.com",
                            profile = "SecondProfile",
                            zoomFactor = 1
                        }
                    }
                }
            };

            foreach (LayoutWindow window in windows)
            {
                LayoutBrowserWindowViewModel vm = viewModelFactory.ForModel(window);
                LayoutBrowserWindow w = windowFactory.ForViewModel(vm);

                w.Show();
            }
        }
    }
}