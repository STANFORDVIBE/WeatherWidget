using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace WeatherWidget
{
    // Кэш последней полученной погоды по каждому городу — показывается, когда нет связи.
    // Файлы %APPDATA%\WeatherWidget\cache\<широта>_<долгота>.txt в формате key=value.
    sealed class WeatherCache
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        readonly string dir;

        public WeatherCache(string dir) { this.dir = dir; }

        public static string DefaultDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WeatherWidget", "cache"); }
        }

        string FileFor(City c)
        {
            return Path.Combine(dir, c.Lat.ToString("0.00", Inv) + "_" + c.Lon.ToString("0.00", Inv) + ".txt");
        }

        public void Save(WeatherData d)
        {
            try
            {
                List<string> lines = new List<string>();
                lines.Add("fetched=" + d.FetchedUtc.ToString("o", Inv));
                lines.Add("source=" + d.Source);
                lines.Add("temp=" + d.Temp.ToString(Inv));
                lines.Add("feels=" + d.Feels.ToString(Inv));
                lines.Add("wind=" + d.Wind.ToString(Inv));
                lines.Add("humidity=" + d.Humidity);
                lines.Add("code=" + d.Code);
                lines.Add("isday=" + (d.IsDay ? "1" : "0"));
                lines.Add("utcoffset=" + d.UtcOffset);
                foreach (DayForecast f in d.Days)
                    lines.Add("day=" + f.Label + "|" + f.Sky + "|" + f.Max.ToString(Inv) + "|" + f.Min.ToString(Inv) +
                        (f.Date != DateTime.MinValue ? "|" + f.Date.ToString("yyyy-MM-dd", Inv) : ""));
                foreach (SunDay s in d.Sun)
                    lines.Add("sun=" + s.Date.ToString("yyyy-MM-dd", Inv) + "|" + s.RiseUtc.ToString("o", Inv) + "|" +
                        s.SetUtc.ToString("o", Inv) + "|" + s.Uv.ToString(Inv));
                foreach (HourForecast h in d.Hours)
                    lines.Add("hour=" + h.Utc.ToString("o", Inv) + "|" + h.Temp.ToString(Inv) + "|" +
                        h.Code + "|" + (h.IsDay ? "1" : "0") + "|" + h.Precip);
                Directory.CreateDirectory(dir);
                SafeFile.WriteAllLines(FileFor(d.City), lines.ToArray());
            }
            catch { }
        }

        // Последние данные по городу не старше maxAge, или null
        public WeatherData Load(City c, TimeSpan maxAge)
        {
            try
            {
                string path = FileFor(c);
                if (!File.Exists(path)) return null;
                WeatherData d = new WeatherData();
                d.City = c;
                bool hasFetched = false, hasTemp = false;
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string k = line.Substring(0, eq), v = line.Substring(eq + 1);
                    switch (k)
                    {
                        case "fetched":
                            hasFetched = DateTime.TryParse(v, Inv, DateTimeStyles.RoundtripKind, out d.FetchedUtc);
                            d.FetchedUtc = d.FetchedUtc.ToUniversalTime();
                            break;
                        case "source": d.Source = v; break;
                        case "temp": hasTemp = double.TryParse(v, NumberStyles.Float, Inv, out d.Temp); break;
                        case "feels": double.TryParse(v, NumberStyles.Float, Inv, out d.Feels); break;
                        case "wind": double.TryParse(v, NumberStyles.Float, Inv, out d.Wind); break;
                        case "humidity": int.TryParse(v, out d.Humidity); break;
                        case "code": int.TryParse(v, out d.Code); break;
                        case "isday": d.IsDay = v == "1"; break;
                        case "utcoffset": int.TryParse(v, out d.UtcOffset); break;
                        case "day":
                            DayForecast f = ParseDay(v);
                            if (f != null) d.Days.Add(f);
                            break;
                        case "hour":
                            HourForecast h = ParseHour(v);
                            if (h != null) d.Hours.Add(h);
                            break;
                        case "sun":
                            SunDay s = ParseSun(v);
                            if (s != null) d.Sun.Add(s);
                            break;
                    }
                }
                if (!hasFetched || !hasTemp) return null;
                if (DateTime.UtcNow - d.FetchedUtc > maxAge) return null;
                d.Sky = WeatherCodes.ToSky(d.Code);
                return d;
            }
            catch { return null; }
        }

        // Подпись|небо|max|min[|дата] — дату кэш старых версий не хранил
        static DayForecast ParseDay(string v)
        {
            string[] p = v.Split('|');
            DayForecast f = new DayForecast();
            if ((p.Length != 4 && p.Length != 5) ||
                !double.TryParse(p[2], NumberStyles.Float, Inv, out f.Max) ||
                !double.TryParse(p[3], NumberStyles.Float, Inv, out f.Min)) return null;
            if (p.Length == 5 && !DateTime.TryParseExact(p[4], "yyyy-MM-dd", Inv, DateTimeStyles.None, out f.Date)) return null;
            try { f.Sky = (Sky)Enum.Parse(typeof(Sky), p[1]); } catch { return null; }
            f.Label = p[0];
            return f;
        }

        // дата|восход UTC|закат UTC|УФ-индекс
        static SunDay ParseSun(string v)
        {
            string[] p = v.Split('|');
            SunDay s = new SunDay();
            if (p.Length != 4 ||
                !DateTime.TryParseExact(p[0], "yyyy-MM-dd", Inv, DateTimeStyles.None, out s.Date) ||
                !DateTime.TryParse(p[1], Inv, DateTimeStyles.RoundtripKind, out s.RiseUtc) ||
                !DateTime.TryParse(p[2], Inv, DateTimeStyles.RoundtripKind, out s.SetUtc) ||
                !double.TryParse(p[3], NumberStyles.Float, Inv, out s.Uv)) return null;
            s.RiseUtc = s.RiseUtc.ToUniversalTime();
            s.SetUtc = s.SetUtc.ToUniversalTime();
            return s;
        }

        // UTC|температура|код|день|вероятность осадков
        static HourForecast ParseHour(string v)
        {
            string[] p = v.Split('|');
            HourForecast h = new HourForecast();
            if (p.Length != 5 ||
                !DateTime.TryParse(p[0], Inv, DateTimeStyles.RoundtripKind, out h.Utc) ||
                !double.TryParse(p[1], NumberStyles.Float, Inv, out h.Temp) ||
                !int.TryParse(p[2], out h.Code) ||
                !int.TryParse(p[4], out h.Precip)) return null;
            h.Utc = h.Utc.ToUniversalTime();
            h.IsDay = p[3] == "1";
            h.Sky = WeatherCodes.ToSky(h.Code);
            return h;
        }
    }
}
