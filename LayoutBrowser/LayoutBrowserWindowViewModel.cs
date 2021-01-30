using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using MonitorCommon;
using MvvmHelpers;

namespace LayoutBrowser
{
    public interface ILayoutBrowserWindowViewModelFactory
    {
        public LayoutBrowserWindowViewModel ForModel(LayoutWindow model);
    }

    public class LayoutBrowserWindowViewModel : ObservableObject
    {
        private readonly ObservableCollection<WindowTabItem> tabs = new ObservableCollection<WindowTabItem>();

        private double left, top, width, height;
        private WindowState state;
        private WindowTabItem currentTab;

        public LayoutBrowserWindowViewModel(LayoutWindow model, IBrowserTabFactory tabFactory, IBrowserTabViewModelFactory tabVmFactory)
        {
            left = model.left;
            top = model.top;
            width = model.width;
            height = model.height;
            state = model.windowState;

            foreach (LayoutWindowTab tabModel in model.tabs)
            {
                BrowserTabViewModel vm = tabVmFactory.ForModel(tabModel);
                BrowserTab t = tabFactory.ForViewModel(vm);

                WindowTabItem item = new WindowTabItem(vm, t);

                tabs.Add(item);
            }

            if (!tabs.IsEmpty())
            {
                currentTab = tabs[0];
            }
        }

        public LayoutWindow ToModel() => new LayoutWindow
        {
            left = left,
            top = top,
            width = width,
            height = height,
            windowState = state,
            tabs = tabs.Select(t => t.ViewModel.ToModel()).ToList()
        };

        public ObservableCollection<WindowTabItem> Tabs => tabs;

        public WindowTabItem CurrentTab
        {
            get => currentTab;
            set => SetProperty(ref currentTab, value);
        }

        public double Left
        {
            get => left;
            set => SetProperty(ref left, value);
        }

        public double Top
        {
            get => top;
            set => SetProperty(ref top, value);
        }

        public double Width
        {
            get => width;
            set => SetProperty(ref width, value);
        }

        public double Height
        {
            get => height;
            set => SetProperty(ref height, value);
        }

        public WindowState State
        {
            get => state;
            set => SetProperty(ref state, value);
        }
    }

    public class WindowTabItem : ObservableObject
    {
        private BrowserTabViewModel viewModel;
        private BrowserTab control;

        public WindowTabItem(BrowserTabViewModel viewModel, BrowserTab control)
        {
            this.viewModel = viewModel;
            this.control = control;
        }

        public BrowserTabViewModel ViewModel
        {
            get => viewModel;
            set => SetProperty(ref viewModel, value);
        }

        public BrowserTab Control
        {
            get => control;
            set => SetProperty(ref control, value);
        }
    }
}