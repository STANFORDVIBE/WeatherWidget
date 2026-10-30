using System;
using System.Collections.Generic;
using System.Threading;

namespace WeatherWidget
{
    // Откуда брать погоду: Open-Meteo и MET Norway «наперегонки», а без связи — последние данные из кэша.
    // Сначала спрашиваем Open-Meteo; если он не ответил за metDelayMs (или сразу упал) — параллельно
    // спрашиваем MET Norway и берём первый успешный ответ. Дольше totalTimeoutMs не ждём — тогда кэш.
    // Так медленный или недоступный сервис не задерживает погоду на десятки секунд.
    // Методы вызываются из фоновых потоков.
    sealed class WeatherService
    {
        public static readonly TimeSpan MaxCacheAge = TimeSpan.FromDays(3);

        readonly Func<string, string> download;
        readonly WeatherCache cache;
        readonly int retryDelayMs, metDelayMs, totalTimeoutMs;
        readonly Dictionary<string, int> offsets = new Dictionary<string, int>();   // известные часовые пояса городов

        // Источник не ответил ни с одной попытки: «Open-Meteo, Омск: …» (виджет пишет это в error.log).
        // Та же ошибка того же источника — не чаще раза в FailRepeat, чтобы журнал не разрастался без связи.
        // Вызывается из фонового потока.
        public Action<string> Failed;
        public static readonly TimeSpan FailRepeat = TimeSpan.FromMinutes(10);
        readonly Dictionary<string, DateTime> reported = new Dictionary<string, DateTime>();   // ошибка → когда сообщили

        public WeatherService(Func<string, string> download, WeatherCache cache, int retryDelayMs)
            : this(download, cache, retryDelayMs, 6000, 15000) { }

        public WeatherService(Func<string, string> download, WeatherCache cache, int retryDelayMs, int metDelayMs, int totalTimeoutMs)
        {
            this.download = download;
            this.cache = cache;
            this.retryDelayMs = retryDelayMs;
            this.metDelayMs = metDelayMs;
            this.totalTimeoutMs = totalTimeoutMs;
        }

        // Свежая погода, а если её не получить — кэш с пометкой Stale. null — нет ни того ни другого.
        public WeatherData Get(City c)
        {
            SourceRace race = new SourceRace();
            race.Start(delegate { return TryOpenMeteo(c); });
            if (!race.Wait(metDelayMs))
            {
                race.Start(delegate { return TryMetNo(c); });
                race.Wait(totalTimeoutMs - metDelayMs);
            }
            WeatherData d = race.Result;
            if (d != null)
            {
                d.FetchedUtc = DateTime.UtcNow;
                if (d.Source == OpenMeteo.Name) lock (offsets) offsets[Key(c)] = d.UtcOffset;
                cache.Save(d);
                return d;
            }
            return Cached(c);
        }

        // Только кэш (например, чтобы сразу показать погоду при запуске, пока идёт загрузка)
        public WeatherData Cached(City c)
        {
            WeatherData old = cache.Load(c, MaxCacheAge);
            if (old != null) old.Stale = true;
            return old;
        }

        WeatherData TryOpenMeteo(City c)
        {
            string url = OpenMeteo.ForecastUrl(c);
            return Retry(OpenMeteo.Name, c, 2, delegate { return OpenMeteo.ParseForecast(c, download(url)); });
        }

        WeatherData TryMetNo(City c)
        {
            string url = MetNo.ForecastUrl(c);
            int offset = KnownOffset(c);
            return Retry(MetNo.Name, c, 2, delegate { return MetNo.ParseForecast(c, download(url), offset); });
        }

        // Часовой пояс для MET Norway: последний известный от Open-Meteo, из кэша, иначе оценка по долготе
        int KnownOffset(City c)
        {
            int o;
            lock (offsets) if (offsets.TryGetValue(Key(c), out o)) return o;
            WeatherData old = cache.Load(c, TimeSpan.MaxValue);
            if (old != null && old.Source == OpenMeteo.Name) return old.UtcOffset;
            return MetNo.EstimateUtcOffset(c.Lon);
        }

        WeatherData Retry(string source, City c, int attempts, Func<WeatherData> f)
        {
            Exception last = null;
            for (int i = 0; i < attempts; i++)
            {
                try { return f(); }
                catch (Exception e)
                {
                    last = e;
                    if (i + 1 < attempts && retryDelayMs > 0) Thread.Sleep(retryDelayMs);
                }
            }
            Report(source, c, last);
            return null;
        }

        void Report(string source, City c, Exception e)
        {
            Action<string> failed = Failed;
            if (failed == null || e == null) return;
            string why = e.GetType().Name + ": " + e.Message;
            string key = source + "|" + why;   // без города: в «динамической погоде» города меняются каждые 45 с
            DateTime now = DateTime.UtcNow;
            lock (reported)
            {
                DateTime when;
                if (reported.TryGetValue(key, out when) && now - when < FailRepeat) return;
                reported[key] = now;
            }
            try { failed(source + ", " + c.Name + ": " + why); } catch { }
        }

        static string Key(City c)
        {
            return Math.Round(c.Lat, 2) + "_" + Math.Round(c.Lon, 2);
        }

        // Несколько источников в фоне; побеждает первый успешный ответ
        sealed class SourceRace
        {
            readonly object gate = new object();
            WeatherData result;
            int running;

            public WeatherData Result { get { lock (gate) return result; } }

            public void Start(Func<WeatherData> source)
            {
                lock (gate) running++;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    WeatherData d = null;
                    try { d = source(); } catch { }
                    lock (gate)
                    {
                        running--;
                        if (result == null && d != null) result = d;
                        Monitor.PulseAll(gate);
                    }
                });
            }

            // true — есть результат; false — все запущенные источники упали или вышло время
            public bool Wait(int ms)
            {
                DateTime end = DateTime.UtcNow.AddMilliseconds(Math.Max(0, ms));
                lock (gate)
                {
                    while (result == null && running > 0)
                    {
                        int left = (int)(end - DateTime.UtcNow).TotalMilliseconds;
                        if (left <= 0) break;
                        Monitor.Wait(gate, left);
                    }
                    return result != null;
                }
            }
        }
    }
}
