using System.Collections.Generic;
using MvvmHelpers;

namespace LayoutBrowser
{
    public class LayoutBrowserWindowViewModel : ObservableObject
    {
        private BrowserTab tab;

        public List<BrowserTab> Tabs { get; } = new List<BrowserTab>();

        public LayoutBrowserWindowViewModel(IBrowserTabFactory tabFactory, IBrowserTabViewModelFactory tabVmFactory)
        {
            var models = new List<LayoutWindowTab>
            {
                new LayoutWindowTab
                {
                    url = "https://bing.com",
                    profile = "FirstProfile"
                },
                new LayoutWindowTab
                {
                    url = "https://duck.com",
                    profile = "SecondProfile"
                }
            };

            foreach (LayoutWindowTab tabModel in models)
            {
                BrowserTabViewModel vm = tabVmFactory.ForModel(tabModel);
                BrowserTab t = tabFactory.ForViewModel(vm);

                Tabs.Add(t);
            }

            tab = Tabs[0];
        }

        public BrowserTab Tab
        {
            get => tab;
            set => SetProperty(ref tab, value);
        }
    }
}