using System;
using System.Collections.Generic;
using System.Threading;

namespace WeatherWidget
{
    // Откуда брать погоду: сначала Open-Meteo, при сбое — MET Norway, а без связи — последние данные из кэша.
    // Методы вызываются из фоновых потоков.
    sealed class WeatherService
    {
        public static readonly TimeSpan MaxCacheAge = TimeSpan.FromDays(3);

        readonly Func<string, string> download;
        readonly WeatherCache cache;
        readonly int retryDelayMs;
        readonly Dictionary<string, int> offsets = new Dictionary<string, int>();   // известные часовые пояса городов

        public WeatherService(Func<string, string> download, WeatherCache cache, int retryDelayMs)
        {
            this.download = download;
            this.cache = cache;
            this.retryDelayMs = retryDelayMs;
        }

        // Свежая погода, а если её не получить — кэш с пометкой Stale. null — нет ни того ни другого.
        public WeatherData Get(City c)
        {
            WeatherData d = TryOpenMeteo(c) ?? TryMetNo(c);
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
            return Retry(2, delegate { return OpenMeteo.ParseForecast(c, download(url)); });
        }

        WeatherData TryMetNo(City c)
        {
            string url = MetNo.ForecastUrl(c);
            int offset = KnownOffset(c);
            return Retry(2, delegate { return MetNo.ParseForecast(c, download(url), offset); });
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

        WeatherData Retry(int attempts, Func<WeatherData> f)
        {
            for (int i = 0; i < attempts; i++)
            {
                try { return f(); }
                catch { if (i + 1 < attempts && retryDelayMs > 0) Thread.Sleep(retryDelayMs); }
            }
            return null;
        }

        static string Key(City c)
        {
            return Math.Round(c.Lat, 2) + "_" + Math.Round(c.Lon, 2);
        }
    }
}
