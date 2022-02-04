using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Threading;
using LanguageExt;
using LayoutBrowser.Layout;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MonitorCommon;
using MvvmHelpers;

namespace LayoutBrowser.Tab;

public interface IUrlLockViewModelFactory
{
    public UrlLockViewModel ForTab(LayoutWindowTab model);
}

public class UrlLockViewModel : ObservableObject, ITabFeatureViewModel
{
    private readonly ILogger logger;
        
    private Uri? browserSource;
    private string? url;
    private bool browserSourceExposed;
    private bool isNavigating;
    private string refreshButtonText = "↻", refreshButtonHint = "Refresh (F5)";

    private TaskCompletionSource<Unit>? refreshComplete;

    private bool lockUrl;
    private string? lockedUrl;
    private int lockUrlRetries;
    private bool lockUrlSettled = true;
    private bool dontRefreshOnBrowserFail;
    private DateTime? lastFail;
    private bool isFailNavigation;
        
    private WebView2? webView;

    public UrlLockViewModel(LayoutWindowTab model, ILogger logger)
    {
        this.logger = logger;

        lockUrl = model.lockUrl;
        if (lockUrl)
        {
            lockedUrl = model.url;
        }
        url = model.url;
        browserSource = model.url.IsNullOrEmpty() ? null : new Uri(model.url);
        dontRefreshOnBrowserFail = model.dontRefreshOnBrowserFail;
    }

    public Task PlugIntoWebView(WebView2 wv, WebView2MessagingService messenger)
    {
        webView = wv;

        wv.NavigationStarting += OnNavigationStarted;
        wv.NavigationCompleted += OnNavigationCompleted;
        wv.CoreWebView2.ProcessFailed += OnProcessFailed;

        return Task.CompletedTask;
    }

    public DateTime? LastFail
    {
        get => lastFail;
        set
        {
            SetProperty(ref lastFail, value);

            OnPropertyChanged(nameof(HasLastFailDate));
        }
    }

    public bool HasLastFailDate => lastFail.HasValue;

    [MemberNotNull(nameof(webView))]
    private void CheckWebView([CallerMemberName] string memberName = "n/a")
    {
        if (webView == null)
        {
            throw new Exception($"WebView should not be null when {memberName} is called");
        }
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        CheckWebView();

        LastFail = DateTime.Now;

        if (dontRefreshOnBrowserFail)
        {
            return;
        }

        webView.Dispatcher.BeginInvoke(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3));

            isFailNavigation = true;
            webView.Reload();
        }, DispatcherPriority.Background);
    }

    public void AfterInit()
    {
        ExposeBrowserSource();
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        CheckWebView();

        IsNavigating = false;

        webView.Dispatcher.BeginInvoke(async () =>
        {
            if (!isFailNavigation)
            {
                LastFail = null;
            }
            else
            {
                isFailNavigation = false;
            }

            await Task.Delay(TimeSpan.FromSeconds(0.25));

            if (lockUrl)
            {
                if (url != lockedUrl)
                {
                    if (lockUrlRetries < 4)
                    {
                        LockUrlRetries++;
                        lockUrlSettled = false;

                        webView.CoreWebView2.Navigate(lockedUrl);
                    }
                    else
                    {
                        lockUrlSettled = true;
                    }
                }
                else
                {
                    LockUrlRetries = Math.Min(2, LockUrlRetries);
                    lockUrlSettled = true;
                }
            }
        }, DispatcherPriority.Background);
    }

    private void OnNavigationStarted(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (lockUrl && lockUrlSettled)
        {
            LockUrlRetries = 0;
        }

        IsNavigating = true;
    }

    public Uri? InternalBrowserSource => browserSource;

    public Uri? BrowserSource
    {
        get => browserSourceExposed ? browserSource : null;
        set
        {
            if (!browserSourceExposed)
            {
                OnPropertyChanged();
                return;
            }

            SetProperty(ref browserSource, value);

            logger.LogDebug($"Source changed to {value}");

            if (value == null || value.ToString() == "about:blank")
            {
                Url = "";
            }
            else
            {
                Url = value.ToString();
            }
        }
    }

    public bool LockUrl
    {
        get => lockUrl;
        set
        {
            if (value && !lockUrl)
            {
                lockedUrl = url;
            }

            SetProperty(ref lockUrl, value);
        }
    }

    public bool DontRefreshOnBrowserFail
    {
        get => dontRefreshOnBrowserFail;
        set => SetProperty(ref dontRefreshOnBrowserFail, value);
    }

    public void LockUrlEx()
    {
        CheckWebView();

        if (lockUrl)
        {
            return;
        }

        SetProperty(ref lockUrl, true, nameof(LockUrl));

        webView.CoreWebView2.Navigate(lockedUrl);
    }

    public string? LockedUrl => lockUrl ? lockedUrl : null;

    public int LockUrlRetries
    {
        get => lockUrlRetries;
        private set => SetProperty(ref lockUrlRetries, value);
    }

    public string? Url
    {
        get => url;
        set => SetProperty(ref url, value);
    }

    private void ExposeBrowserSource()
    {
        browserSourceExposed = true;

        OnPropertyChanged(nameof(BrowserSource));
    }

    public string RefreshButtonText
    {
        get => refreshButtonText;
        set => SetProperty(ref refreshButtonText, value);
    }

    public string RefreshButtonHint
    {
        get => refreshButtonHint;
        set => SetProperty(ref refreshButtonHint, value);
    }

    public bool IsNavigating
    {
        get => isNavigating;
        set
        {
            bool stopped = isNavigating && !value;

            SetProperty(ref isNavigating, value);

            RefreshButtonText = value ? "✕" : "↻";
            RefreshButtonHint = value ? "Stop loading (Esc)" : "Refresh (F5)";

            if (stopped)
            {
                refreshComplete?.TrySetResult(Unit.Default);
            }
        }
    }

    public async Task OnAutoRefresh()
    {
        if (webView == null)
        {
            return;
        }

        TaskCompletionSource<Unit> newComplete = new();
        TaskCompletionSource<Unit>? oldComplete = Interlocked.Exchange(ref refreshComplete, newComplete);
        oldComplete?.TrySetResult(Unit.Default);

        await webView.Dispatcher.BeginInvoke(Refresh, DispatcherPriority.Background);
            
        await newComplete.Task;
    }

    public async Task ExecuteGo()
    {
        if (url.IsNullOrEmpty())
        {
            return;
        }

        CheckWebView();

        await webView.EnsureCoreWebView2Async();

        logger.LogDebug($"Navigating to {url}");

        bool trySearch = false;
        try
        {
            BrowserSource = new Uri(url);
            webView.CoreWebView2.Navigate(url);
        }
        catch (ArgumentException) 
        {
            trySearch = true;
        }
        catch (UriFormatException) 
        {
            trySearch = true;
        }

        if (trySearch)
        {
            string searchUrl = "https://duckduckgo.com/?q=" + HttpUtility.UrlEncode(url);

            webView.CoreWebView2.Navigate(searchUrl);
        }

        webView.Focus();
    }

    public void HandleRefreshStopButtonPress()
    {
        CheckWebView();

        if (isNavigating)
        {
            webView.Stop();
        }
        else
        {
            Refresh();
        }
    }

    public event Action? PreRefresh;

    public async void Refresh()
    {
        CheckWebView();

        PreRefresh?.Invoke();

        await webView.EnsureCoreWebView2Async();

        webView.Reload();
    }
}