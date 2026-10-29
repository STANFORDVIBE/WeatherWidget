using System;
using System.Net.NetworkInformation;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WeatherWidget
{
    // Когда обновлять погоду: раз в RefreshMinutes; следующий город «динамической погоды» — каждые
    // DynamicSeconds; без связи — повтор через OfflineRetryMinutes; сразу — когда вернулась сеть
    // или компьютер проснулся. Что именно загружать, решает окно.
    sealed class RefreshSchedule : IDisposable
    {
        public const int RefreshMinutes = 15, DynamicSeconds = 45, OfflineRetryMinutes = 2;

        readonly Action<MethodInvoker> invoke;   // выполнить в потоке окна (события сети и сна приходят из других потоков)
        readonly Action refresh, networkBack;
        readonly System.Windows.Forms.Timer refreshTimer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer dynTimer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer retryTimer = new System.Windows.Forms.Timer();   // повтор, пока нет связи

        // refresh — обновить текущий город, nextCity — следующий город «динамической погоды»,
        // networkBack — сеть вернулась
        public RefreshSchedule(Action<MethodInvoker> invoke, Action refresh, Action nextCity, Action networkBack)
            : this(invoke, refresh, nextCity, networkBack, RefreshMinutes * 60 * 1000, DynamicSeconds * 1000, OfflineRetryMinutes * 60 * 1000) { }

        // Интервалы в миллисекундах — тесты задают короткие
        public RefreshSchedule(Action<MethodInvoker> invoke, Action refresh, Action nextCity, Action networkBack,
            int refreshMs, int dynamicMs, int retryMs)
        {
            this.invoke = invoke;
            this.refresh = refresh;
            this.networkBack = networkBack;

            refreshTimer.Interval = refreshMs;
            refreshTimer.Tick += delegate { refresh(); };
            refreshTimer.Start();

            dynTimer.Interval = dynamicMs;
            dynTimer.Tick += delegate { nextCity(); };

            retryTimer.Interval = retryMs;
            retryTimer.Tick += delegate { retryTimer.Stop(); refresh(); };

            NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }

        // «Динамическая погода»: отсчёт до следующего города — заново
        public void StartDynamic() { dynTimer.Stop(); dynTimer.Start(); }

        public void StopDynamic() { dynTimer.Stop(); }

        // Пришли (или не пришли) данные: нет свежих — повторить через пару минут
        public void RetryLater(bool needed)
        {
            retryTimer.Stop();
            if (needed) retryTimer.Start();
        }

        public bool Disposed { get; private set; }

        void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume) Resumed();
        }

        void OnNetworkChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            NetworkChanged(e.IsAvailable);
        }

        // Компьютер проснулся — погода могла устареть на время сна. Отсчёт до планового обновления — заново.
        // Сеть после сна поднимается не сразу: не вышло — повторит retryTimer или NetworkChanged.
        internal void Resumed()
        {
            invoke(delegate { if (Disposed) return; refreshTimer.Stop(); refreshTimer.Start(); refresh(); });
        }

        // Сеть вернулась — обновить сразу, не дожидаясь таймера
        internal void NetworkChanged(bool available)
        {
            if (available) invoke(delegate { if (!Disposed) networkBack(); });
        }

        public void Dispose()
        {
            Disposed = true;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            refreshTimer.Dispose();
            dynTimer.Dispose();
            retryTimer.Dispose();
        }
    }
}
