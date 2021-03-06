using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LayoutBrowser.Layout;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MvvmHelpers;

namespace LayoutBrowser.Tab;

public interface IScrollRestoreViewModelFactory
{
    public ScrollRestoreViewModel ForTab(LayoutWindowTab model);
}

public class ScrollRestoreViewModel : ObservableObject, ITabFeatureViewModel
{
    public const string EventType = "scroll";

    private bool navStopped;
    private Point? scrollRestore;
    private TimeSpan scrollDelay;
    private CancellationTokenSource? currentScrollRestore;

    private WebView2? webView;

    private Point lastScroll;
    private Point lockedScroll;

    private bool lockScroll;

    public ScrollRestoreViewModel(LayoutWindowTab model)
    {
        lastScroll = new Point(model.scrollX, model.scrollY);
        lockedScroll = lastScroll;
        lockScroll = model.lockScroll;
        scrollRestore = lastScroll;
        scrollDelay = model.scrollDelay;
    }

    public async Task PlugIntoWebView(WebView2 wv, WebView2MessagingService messenger)
    {
        webView = wv;

        messenger.AddMessageHandler<ScrollCoords>(EventType, OnScrollChanged);

        await wv.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync($@"
                window.addEventListener(""scroll"", () => window.chrome.webview.postMessage({{type:'{EventType}', x:window.scrollX, y:window.scrollY}}))
            ");

        wv.NavigationStarting += OnNavigationStarted;
        wv.NavigationCompleted += OnNavigationCompleted;
    }

    public TimeSpan ScrollDelay
    {
        get => scrollDelay;
        set => SetProperty(ref scrollDelay, value);
    }

    public Point LastScroll => lockScroll ? lockedScroll : lastScroll;

    public bool LockScroll
    {
        get => lockScroll;
        set
        {
            if (value && !lockScroll)
            {
                lockedScroll = lastScroll;
            }

            SetProperty(ref lockScroll, value);
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        RestoreScroll();
    }

    private void OnNavigationStarted(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        OnNavStarted();
    }

    private void OnScrollChanged(ScrollCoords pt)
    {
        if (!navStopped)
        {
            return;
        }

        lastScroll = new Point(pt.x, pt.y);
    }

    public void RememberScroll() => scrollRestore = LastScroll;

    private async void RestoreScroll()
    {
        if (webView == null)
        {
            throw new Exception($"Got {nameof(RestoreScroll)} while webView is not initialized");
        }

        try
        {
            Point? restore = scrollRestore;
            if (restore == null)
            {
                return;
            }

            CancellationTokenSource cts = new();
            CancellationTokenSource? old = Interlocked.Exchange(ref currentScrollRestore, cts);
            old?.Cancel();

            try
            {
                await Task.Delay(scrollDelay, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (cts.IsCancellationRequested)
            {
                return;
            }

            Point coords = restore.Value;
            await webView.ExecuteScriptAsync($"window.scrollTo({coords.X:0.0}, {coords.Y:0.0})");
        }
        catch (ObjectDisposedException)
        {
            // webView was closed before the the scroll restore could happen
        }
        finally
        {
            navStopped = true;
        }
    }

    private void OnNavStarted()
    {
        navStopped = false;

        CancellationTokenSource? scrollRestoreOp = Interlocked.Exchange(ref currentScrollRestore, null);
        scrollRestoreOp?.Cancel();
    }
}

public class ScrollCoords
{
    public double x;
    public double y;
}