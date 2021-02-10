using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using LayoutBrowser.Layout;
using Microsoft.Extensions.Logging;
using WpfAppCommon;

namespace LayoutBrowser.Window
{
    public interface ILayoutBrowserWindowFactory
    {
        public LayoutBrowserWindow ForViewModel(LayoutBrowserWindowViewModel viewModel);
    }

    /// <summary>
    /// Interaction logic for LayoutBrowserWindow.xaml
    /// </summary>
    public partial class LayoutBrowserWindow
    {
        private readonly LayoutBrowserWindowViewModel viewModel;
        private readonly ILogger logger;

        public LayoutBrowserWindow(LayoutBrowserWindowViewModel viewModel, LayoutManager layoutManager, ILogger logger)
        {
            this.viewModel = viewModel;
            this.logger = logger;

            viewModel.WindowCloseRequested += Close;

            InitializeComponent();

            AddShortcut(Key.W, ModifierKeys.Control, viewModel.CloseCurrentTab);
            AddShortcut(Key.T, ModifierKeys.Control, () => viewModel.OpenNewTab());
            AddShortcut(Key.Tab, ModifierKeys.Control, viewModel.NextTab);
            AddShortcut(Key.Tab, ModifierKeys.Control | ModifierKeys.Shift, viewModel.PrevTab);
            AddShortcut(Key.Left, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt, viewModel.MovePrev);
            AddShortcut(Key.Right, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt, viewModel.MoveNext);
            AddShortcut(Key.Q, ModifierKeys.Alt, viewModel.Quit);
            AddShortcut(Key.Q, ModifierKeys.Control, viewModel.Quit);
            AddShortcut(Key.Escape, ModifierKeys.None, viewModel.StopLoading);
            AddShortcut(Key.F5, ModifierKeys.None, viewModel.Refresh);
            AddShortcut(Key.F6, ModifierKeys.None, viewModel.FocusAddressBar);
            AddShortcut(Key.P, ModifierKeys.Control | ModifierKeys.Shift, viewModel.RequestPopout);
            AddShortcut(Key.N, ModifierKeys.Control, viewModel.OpenNewEmptyWindow);
            AddShortcut(Key.N, ModifierKeys.Control | ModifierKeys.Shift, viewModel.OpenNewEmptyWindow);
            AddShortcut(Key.T, ModifierKeys.Control | ModifierKeys.Shift, layoutManager.ReopenLastClosedItem);
            AddShortcut(Key.U, ModifierKeys.Control | ModifierKeys.Shift, viewModel.ToggleUi);

            Dispatcher.BeginInvoke(() =>
            {
                tabBar.ScrollIntoView(viewModel.CurrentTab);
            }, DispatcherPriority.Background);
        }

        public LayoutBrowserWindowViewModel ViewModel => viewModel;

        protected void AddShortcut(Key key, ModifierKeys modifier, Action run)
        {
            InputBindings.Add(
                new KeyBinding(new WindowCommand(run), key, modifier)
            );
        }

        private void OnTabClicked(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle)
            {
                return;
            }

            e.Handled = true;

            ListBoxItem item = (ListBoxItem) sender;
            WindowTabItem tab = (WindowTabItem) item.DataContext;

            viewModel.CloseTab(tab);
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            Win32MaximizeHelper.FixMaximize(this, resizeBorderMrg.Margin);
        }
    }

    public static class Win32MaximizeHelper
    {
        // don't hide taskbar when maximized
        // taken from http://www.abhisheksur.com/2010/09/taskbar-with-window-maximized-and.html
        public static void FixMaximize(System.Windows.Window window, Thickness borderMargin)
        { 
            IntPtr handle = (new WindowInteropHelper(window)).Handle;
            var hSource = HwndSource.FromHwnd(handle);

            IntPtr WindowProc(
                IntPtr hwnd,
                int msg,
                IntPtr wParam,
                IntPtr lParam,
                ref bool handled)
            {
                switch (msg)
                {
                    case 0x0024:
                        WmGetMinMaxInfo(hwnd, lParam, borderMargin);
                        handled = true;
                        break;
                }

                return (IntPtr) 0;
            }
            
            hSource.AddHook(WindowProc);
        }

        private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam, Thickness borderThickness)
        {
            MINMAXINFO mmi = (MINMAXINFO) Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));

            // Adjust the maximized size and position to fit the work area of the correct monitor
            int MONITOR_DEFAULTTONEAREST =0x00000002;
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            Size topLeft = new Size(borderThickness.Left, borderThickness.Top),
                 bottomRight = new Size(borderThickness.Right, borderThickness.Bottom);
            /*using (*/var hSource = HwndSource.FromHwnd(hwnd);//)
            {
                Matrix transformToDevice = hSource.CompositionTarget.TransformToDevice;
                topLeft = (Size)transformToDevice.Transform((Vector) topLeft);
                bottomRight = (Size)transformToDevice.Transform((Vector) bottomRight);
            }

            if (monitor != IntPtr.Zero)
            {
                MONITORINFOEX monitorInfo = new MONITORINFOEX();
                GetMonitorInfo(monitor, monitorInfo);
                RECT rcWorkArea = monitorInfo.rcWork;
                RECT rcMonitorArea = monitorInfo.rcMonitor;
                mmi.ptMaxPosition.X = rcMonitorArea.left - rcWorkArea.left - (int) topLeft.Width;
                mmi.ptMaxPosition.Y = rcMonitorArea.top - rcWorkArea.top - (int) topLeft.Height;
                mmi.ptMaxSize.X = rcWorkArea.right - rcWorkArea.left + (int) topLeft.Width + (int) bottomRight.Width;
                mmi.ptMaxSize.Y = rcWorkArea.bottom - rcWorkArea.top + (int) topLeft.Height + (int) bottomRight.Height;
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }
        
        [DllImport("User32")]
        internal static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }

            public static implicit operator System.Drawing.Point(POINT p)
            {
                return new System.Drawing.Point(p.X, p.Y);
            }

            public static implicit operator POINT(System.Drawing.Point p)
            {
                return new POINT(p.X, p.Y);
            }
        }

        // size of a device name string
        private const int CCHDEVICENAME = 32;

        [DllImport("User32.dll", CharSet=CharSet.Auto)] 
        public static extern bool GetMonitorInfo(IntPtr hmonitor, [In, Out]MONITORINFOEX info);

        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Auto, Pack=4)]
        public class MONITORINFOEX { 
            public int     cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
            public RECT    rcMonitor = new RECT(); 
            public RECT    rcWork = new RECT(); 
            public int     dwFlags = 0;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst=32)] 
            public char[]  szDevice = new char[32];
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINTSTRUCT { 
            public int x;
            public int y;
            public POINTSTRUCT(int x, int y) {
                this.x = x; 
                this.y = y;
            } 
        } 

        [StructLayout(LayoutKind.Sequential)] 
        public struct RECT {
            public int left; 
            public int top; 
            public int right;
            public int bottom; 
        }
    }
}
