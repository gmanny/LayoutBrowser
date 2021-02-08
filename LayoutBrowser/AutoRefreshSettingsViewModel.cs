using System;
using System.Threading;
using MvvmHelpers;

namespace LayoutBrowser
{
    public interface IAutoRefreshSettingsViewModelFactory
    {
        public AutoRefreshSettingsViewModel ForSettings(bool autoRefreshEnabled, TimeSpan autoRefreshSpan);
    }

    public class AutoRefreshSettingsViewModel : ObservableObject, IDisposable
    {
        private readonly AutoRefreshGlobalOneSecondTimer timer;

        private bool autoRefreshEnabled;
        private TimeSpan autoRefreshSpan;

        private DateTime refreshSpanStart;

        public AutoRefreshSettingsViewModel(bool autoRefreshEnabled, TimeSpan autoRefreshSpan, AutoRefreshGlobalOneSecondTimer timer)
        {
            this.autoRefreshEnabled = autoRefreshEnabled;
            this.autoRefreshSpan = autoRefreshSpan;
            this.timer = timer;

            timer.Timer += OnTimer;
        }

        private void OnTimer()
        {
            throw new NotImplementedException();
        }

        public bool AutoRefreshEnabled
        {
            get => autoRefreshEnabled;
            set => SetProperty(ref autoRefreshEnabled, value);
        }

        public TimeSpan AutoRefreshSpan
        {
            get => autoRefreshSpan;
            set
            {
                SetProperty(ref autoRefreshSpan, value);
                OnPropertyChanged(nameof(ShowDateInSpanStart));
            }
        }

        public bool ShowDateInSpanStart => autoRefreshSpan.TotalHours >= 12;

        public DateTime RefreshSpanStart
        {
            get => refreshSpanStart;
            set => SetProperty(ref refreshSpanStart, value);
        }

        public void Dispose()
        {
            timer.Timer -= OnTimer;
        }
    }

    public class AutoRefreshGlobalOneSecondTimer
    {
        private readonly Timer timer;

        public AutoRefreshGlobalOneSecondTimer()
        {
            timer = new Timer(OnTimer, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        public event Action Timer;

        private void OnTimer(object state)
        {
            Timer?.Invoke();
        }
    }
}