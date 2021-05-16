using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using LayoutBrowser.Layout;
using Microsoft.Extensions.Logging;
using WpfAppCommon;
using Size = System.Windows.Size;

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
        private static int windowIndex;

        private readonly LayoutBrowserWindowViewModel viewModel;
        private readonly LayoutManager layoutManager;
        private readonly ILogger logger;

        private IntPtr? cachedHandle;
        private bool cachedTopMost;

        private int myIndex = Interlocked.Increment(ref windowIndex);

        static LayoutBrowserWindow()
        {
            TopmostProperty.OverrideMetadata(typeof(LayoutBrowserWindow), new FrameworkPropertyMetadata(OnTopmostChanged));
        }

        public LayoutBrowserWindow(LayoutBrowserWindowViewModel viewModel, LayoutManager layoutManager, ILogger logger)
        {
            this.viewModel = viewModel;
            this.layoutManager = layoutManager;
            this.logger = logger;

            Dispatcher.BeginInvoke(FirstBackgroundDispatch, DispatcherPriority.Background);

            viewModel.WindowCloseRequested += Close;
            viewModel.NativeRect += NativeRect;

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

        private Rectangle NativeRect()
        {
            if (!GetWindowRect(CachedHandle, out RECT r))
            {
                return new Rectangle();
            }

            return new Rectangle(r.left, r.top, r.right - r.left, r.bottom - r.top);
        }

        private void FirstBackgroundDispatch()
        {
            if (!double.IsNaN(viewModel.LeftNativeInit))
            {
                SetWindowPos(CachedHandle, IntPtr.Zero, (int) viewModel.LeftNativeInit, (int) viewModel.TopNativeInit, (int) viewModel.WidthNativeInit, (int) viewModel.HeightNativeInit, SetWindowPosFlags.SWP_NOACTIVATE | SetWindowPosFlags.SWP_NOOWNERZORDER | SetWindowPosFlags.SWP_NOZORDER);

                return;
            }

            PresentationSource source = PresentationSource.FromVisual(this);
            CompositionTarget ct = source?.CompositionTarget;
            double dpiX = ct?.TransformToDevice.M11 ?? 1.0;
            double dpiY = ct?.TransformToDevice.M22 ?? 1.0;

            SetWindowPos(CachedHandle, IntPtr.Zero, (int) (viewModel.LeftInit*dpiX), (int) (viewModel.TopInit*dpiY), (int) (viewModel.WidthInit*dpiX), (int) (viewModel.HeightInit*dpiY), SetWindowPosFlags.SWP_NOACTIVATE | SetWindowPosFlags.SWP_NOOWNERZORDER | SetWindowPosFlags.SWP_NOZORDER);
        }

        private static void OnTopmostChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is LayoutBrowserWindow wnd))
            {
                return;
            }

            wnd.cachedTopMost = (bool) e.NewValue;
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);

            logger.LogDebug($"Window #{myIndex} DPI changed x:{oldDpi.DpiScaleX}/y:{oldDpi.DpiScaleY} -> x:{newDpi.DpiScaleX}/y:{newDpi.DpiScaleY}");

            WiggleBrowser();
        }

        private async void WiggleBrowser(int step = 1)
        {
            if (step > 10)
            {
                return;
            }

            if (step % 2 == 1)
            {
                inGrid.Margin = new Thickness(0, 0, 0, -1);
            }
            else
            {
                inGrid.Margin = new Thickness(0, 0, 0, 0);
            }

            await Task.Delay(TimeSpan.FromSeconds(0.1));

            await Dispatcher.BeginInvoke(() => WiggleBrowser(step + 1), DispatcherPriority.Background);
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

        private IntPtr CachedHandle => cachedHandle ?? IntPtr.Zero;

        protected override void OnSourceInitialized(EventArgs e)
        {
            logger.LogDebug($"Window #{myIndex} source initialized");

            base.OnSourceInitialized(e);

            cachedHandle = new WindowInteropHelper(this).Handle;

            Win32MaximizeHelper.FixMaximize(this, resizeBorderMrg.Margin, viewModel);
        }

        public void BringToFrontWithoutFocus()
        {
            if (CachedHandle == IntPtr.Zero)
            {
                return;
            }

            if (!cachedTopMost)
            {
                SetWindowPos(CachedHandle, (IntPtr)(HWND_TOPMOST | HWND_TOP), 0, 0, 0, 0, SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
                SetWindowPos(CachedHandle, (IntPtr)(HWND_NOTOPMOST | HWND_TOP), 0, 0, 0, 0, SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
            }
            else
            {
                SetWindowPos(CachedHandle, (IntPtr)(HWND_NOTOPMOST | HWND_TOP), 0, 0, 0, 0, SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
                SetWindowPos(CachedHandle, (IntPtr)(HWND_TOPMOST | HWND_TOP), 0, 0, 0, 0, SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
            }
        }

        public void BringToBack()
        {
            if (CachedHandle == IntPtr.Zero)
            {
                return;
            }

            SetWindowPos(CachedHandle, (IntPtr)HWND_BOTTOM, 0, 0, 0, 0, SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
        }
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, SetWindowPosFlags uFlags);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int GWL_EXSTYLE = -20;

        private const int HWND_TOP = 0;
        private const int HWND_BOTTOM = 1;
        private const int HWND_TOPMOST = -1;
        private const int HWND_NOTOPMOST = -2;

        private void OnMinimizeClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MinimizeBtn_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                WindowTabItem tab = viewModel.CurrentTab;
                if (tab != null)
                {
                    tab.ViewModel.Hidden = !tab.ViewModel.Hidden;
                }
            }
        }

        private void UiHideBtn_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                viewModel.NotInLayout = !viewModel.NotInLayout;
            }
        }
    }

    public static class Win32MaximizeHelper
    {
        // don't hide taskbar when maximized
        // taken from http://www.abhisheksur.com/2010/09/taskbar-with-window-maximized-and.html
        public static void FixMaximize(System.Windows.Window window, Thickness borderMargin,
            LayoutBrowserWindowViewModel viewModel)
        { 
            IntPtr handle = new WindowInteropHelper(window).Handle;
            HwndSource hSource = HwndSource.FromHwnd(handle);

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
    }

    [StructLayout(LayoutKind.Sequential)] 
    public struct RECT {
        public int left; 
        public int top; 
        public int right;
        public int bottom; 
    }

    [Flags]
    public enum SetWindowPosFlags : uint
    {
        // ReSharper disable InconsistentNaming

        /// <summary>
        ///     If the calling thread and the thread that owns the window are attached to different input queues, the system posts the request to the thread that owns the window. This prevents the calling thread from blocking its execution while other threads process the request.
        /// </summary>
        SWP_ASYNCWINDOWPOS = 0x4000,

        /// <summary>
        ///     Prevents generation of the WM_SYNCPAINT message.
        /// </summary>
        SWP_DEFERERASE = 0x2000,

        /// <summary>
        ///     Draws a frame (defined in the window's class description) around the window.
        /// </summary>
        SWP_DRAWFRAME = 0x0020,

        /// <summary>
        ///     Applies new frame styles set using the SetWindowLong function. Sends a WM_NCCALCSIZE message to the window, even if the window's size is not being changed. If this flag is not specified, WM_NCCALCSIZE is sent only when the window's size is being changed.
        /// </summary>
        SWP_FRAMECHANGED = 0x0020,

        /// <summary>
        ///     Hides the window.
        /// </summary>
        SWP_HIDEWINDOW = 0x0080,

        /// <summary>
        ///     Does not activate the window. If this flag is not set, the window is activated and moved to the top of either the topmost or non-topmost group (depending on the setting of the hWndInsertAfter parameter).
        /// </summary>
        SWP_NOACTIVATE = 0x0010,

        /// <summary>
        ///     Discards the entire contents of the client area. If this flag is not specified, the valid contents of the client area are saved and copied back into the client area after the window is sized or repositioned.
        /// </summary>
        SWP_NOCOPYBITS = 0x0100,

        /// <summary>
        ///     Retains the current position (ignores X and Y parameters).
        /// </summary>
        SWP_NOMOVE = 0x0002,

        /// <summary>
        ///     Does not change the owner window's position in the Z order.
        /// </summary>
        SWP_NOOWNERZORDER = 0x0200,

        /// <summary>
        ///     Does not redraw changes. If this flag is set, no repainting of any kind occurs. This applies to the client area, the nonclient area (including the title bar and scroll bars), and any part of the parent window uncovered as a result of the window being moved. When this flag is set, the application must explicitly invalidate or redraw any parts of the window and parent window that need redrawing.
        /// </summary>
        SWP_NOREDRAW = 0x0008,

        /// <summary>
        ///     Same as the SWP_NOOWNERZORDER flag.
        /// </summary>
        SWP_NOREPOSITION = 0x0200,

        /// <summary>
        ///     Prevents the window from receiving the WM_WINDOWPOSCHANGING message.
        /// </summary>
        SWP_NOSENDCHANGING = 0x0400,

        /// <summary>
        ///     Retains the current size (ignores the cx and cy parameters).
        /// </summary>
        SWP_NOSIZE = 0x0001,

        /// <summary>
        ///     Retains the current Z order (ignores the hWndInsertAfter parameter).
        /// </summary>
        SWP_NOZORDER = 0x0004,

        /// <summary>
        ///     Displays the window.
        /// </summary>
        SWP_SHOWWINDOW = 0x0040,

        // ReSharper restore InconsistentNaming
    }
}
