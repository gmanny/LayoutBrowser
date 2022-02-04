using System;
using System.Threading;
using System.Threading.Tasks;
using MvvmHelpers;

namespace LayoutBrowser.Tab;

public interface IAutoRefreshSettingsViewModelFactory
{
    public AutoRefreshSettingsViewModel ForSettings(bool autoRefreshEnabled, TimeSpan autoRefreshSpan, Func<Task> refreshCallback);
}

public class AutoRefreshSettingsViewModel : ObservableObject, IDisposable
{
    private readonly AutoRefreshGlobalOneSecondTimer timer;

    private bool autoRefreshEnabled;
    private TimeSpan autoRefreshSpan;
    private readonly Func<Task> refreshCallback;

    private DateTime refreshSpanStart;

    private bool refreshTriggered;

    public AutoRefreshSettingsViewModel(bool autoRefreshEnabled, TimeSpan autoRefreshSpan, Func<Task> refreshCallback, AutoRefreshGlobalOneSecondTimer timer)
    {
        this.autoRefreshEnabled = autoRefreshEnabled;
        this.autoRefreshSpan = autoRefreshSpan <= TimeSpan.FromSeconds(1) ? TimeSpan.FromHours(1) : autoRefreshSpan;
        this.refreshCallback = refreshCallback;
        this.timer = timer;

        refreshSpanStart = DateTime.Now;

        timer.Timer += OnTimer;
    }

    private async void OnTimer()
    {
        if (!autoRefreshEnabled)
        {
            return;
        }

        if (TillNextRefresh > TimeSpan.Zero)
        {
            OnPropertyChanged(nameof(TillNextRefresh));
            return;
        }

        if (refreshTriggered)
        {
            return;
        }

        RefreshTriggered = true;
        try
        {
            await refreshCallback();
        }
        finally
        {
            RefreshSpanStart = DateTime.Now;

            RefreshTriggered = false;
        }
    }

    public bool RefreshTriggered
    {
        get => refreshTriggered;
        set => SetProperty(ref refreshTriggered, value);
    }

    public bool AutoRefreshEnabled
    {
        get => autoRefreshEnabled;
        set
        {
            if (value && !autoRefreshEnabled)
            {
                RefreshSpanStart = DateTime.Now;
            }

            SetProperty(ref autoRefreshEnabled, value);
        }
    }

    public TimeSpan AutoRefreshSpan
    {
        get => autoRefreshSpan;
        set
        {
            SetProperty(ref autoRefreshSpan, value);
            OnPropertyChanged(nameof(ShowDateInSpanStart));

            RefreshSpanStart = DateTime.Now;
        }
    }

    public bool ShowDateInSpanStart => autoRefreshSpan.TotalHours >= 12;

    public TimeSpan TillNextRefresh => AutoRefreshSpan - (DateTime.Now - RefreshSpanStart);

    public DateTime RefreshSpanStart
    {
        get => refreshSpanStart;
        set
        {
            SetProperty(ref refreshSpanStart, value);
            OnPropertyChanged(nameof(TillNextRefresh));
        }
    }

    public void Dispose()
    {
        timer.Timer -= OnTimer;

        GC.SuppressFinalize(this);
    }
}

public class AutoRefreshGlobalOneSecondTimer
{
    // this prevents timer from automatically disposing when its destructor is called
    // ReSharper disable once NotAccessedField.Local
#pragma warning disable IDE0052 // Remove unread private members
    private readonly Timer timer;
#pragma warning restore IDE0052 // Remove unread private members

    public AutoRefreshGlobalOneSecondTimer()
    {
        timer = new Timer(OnTimer, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public event Action? Timer;

    private void OnTimer(object? state)
    {
        Timer?.Invoke();
    }
}