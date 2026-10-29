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
        {
            this.invoke = invoke;
            this.refresh = refresh;
            this.networkBack = networkBack;

            refreshTimer.Interval = RefreshMinutes * 60 * 1000;
            refreshTimer.Tick += delegate { refresh(); };
            refreshTimer.Start();

            dynTimer.Interval = DynamicSeconds * 1000;
            dynTimer.Tick += delegate { nextCity(); };

            retryTimer.Interval = OfflineRetryMinutes * 60 * 1000;
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

        // компьютер проснулся — погода могла устареть на время сна
        void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode != PowerModes.Resume) return;
            // сеть после сна поднимается не сразу: не вышло — повторит retryTimer или OnNetworkChanged
            invoke(delegate { refreshTimer.Stop(); refreshTimer.Start(); refresh(); });
        }

        // сеть вернулась — обновить сразу, не дожидаясь таймера
        void OnNetworkChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            if (e.IsAvailable) invoke(delegate { networkBack(); });
        }

        public void Dispose()
        {
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            refreshTimer.Dispose();
            dynTimer.Dispose();
            retryTimer.Dispose();
        }
    }
}
