using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace WeatherWidget
{
    // Резервный источник — MET Norway, Норвежский метеорологический институт (https://api.met.no).
    // Весь мир, бесплатно, без ключа; данные по лицензии CC BY 4.0.
    // В ответе нет ощущаемой температуры, часового пояса и дневных максимумов — считаем их сами.
    static class MetNo
    {
        public const string Name = "MET Norway";
        static readonly CultureInfo Ru = new CultureInfo("ru-RU");

        // MET Norway принимает не больше 4 знаков после запятой в координатах
        public static string ForecastUrl(City c)
        {
            return "https://api.met.no/weatherapi/locationforecast/2.0/compact?lat=" +
                c.Lat.ToString("0.####", CultureInfo.InvariantCulture) +
                "&lon=" + c.Lon.ToString("0.####", CultureInfo.InvariantCulture);
        }

        // utcOffset — смещение часового пояса города в секундах (MET его не сообщает)
        public static WeatherData ParseForecast(City c, string json, int utcOffset)
        {
            Dictionary<string, object> root = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
            Dictionary<string, object> props = (Dictionary<string, object>)root["properties"];
            object[] series = (object[])props["timeseries"];
            if (series.Length == 0) throw new FormatException("MET Norway: пустой прогноз");

            // текущая погода — первая точка ряда
            DateTime t0;
            Dictionary<string, object> first = (Dictionary<string, object>)series[0];
            Dictionary<string, object> now = Instant(first, out t0);
            string symbol = Symbol(first);
            WeatherData d = new WeatherData();
            d.City = c;
            d.Source = Name;
            d.Temp = Num(now["air_temperature"]);
            d.Humidity = (int)Math.Round(Num(now["relative_humidity"]));
            d.Wind = Num(now["wind_speed"]);
            d.Feels = ApparentTemp(d.Temp, d.Humidity, d.Wind);
            d.Code = SymbolToWmo(symbol);
            d.Sky = WeatherCodes.ToSky(d.Code);
            d.IsDay = IsDay(symbol, t0, c.Lon);
            d.UtcOffset = utcOffset;

            // почасовой прогноз на сутки: точки с часовым символом (дальше в ряду MET шаг 6 часов).
            // Вероятности осадков в компактном ответе нет — Precip остаётся -1.
            foreach (object o in series)
            {
                Dictionary<string, object> e = (Dictionary<string, object>)o;
                DateTime t;
                Dictionary<string, object> inst = Instant(e, out t);
                if (t >= t0.AddHours(OpenMeteo.HoursAhead)) break;
                string s = Symbol(e, "next_1_hours");
                if (s == null) continue;
                HourForecast h = new HourForecast();
                h.Utc = t;
                h.Temp = Num(inst["air_temperature"]);
                h.Code = SymbolToWmo(s);
                h.Sky = WeatherCodes.ToSky(h.Code);
                h.IsDay = IsDay(s, t, c.Lon);
                d.Hours.Add(h);
            }

            // прогноз на 3 дня: по каждому местному дню — max/min температуры и погода около полудня
            DateTime today = t0.AddSeconds(utcOffset).Date;
            for (int k = 1; k <= 3; k++)
            {
                DateTime day = today.AddDays(k);
                double max = double.MinValue, min = double.MaxValue;
                string daySymbol = null;
                double bestNoon = double.MaxValue;
                foreach (object o in series)
                {
                    Dictionary<string, object> e = (Dictionary<string, object>)o;
                    DateTime t;
                    Dictionary<string, object> inst = Instant(e, out t);
                    DateTime local = t.AddSeconds(utcOffset);
                    if (local.Date != day) continue;
                    double temp = Num(inst["air_temperature"]);
                    max = Math.Max(max, temp);
                    min = Math.Min(min, temp);
                    string s = Symbol(e);
                    double fromNoon = Math.Abs(local.TimeOfDay.TotalHours - 12);
                    if (s != null && fromNoon < bestNoon) { bestNoon = fromNoon; daySymbol = s; }
                }
                if (daySymbol == null) continue;   // нет данных на этот день
                DayForecast f = new DayForecast();
                f.Label = Ru.DateTimeFormat.GetAbbreviatedDayName(day.DayOfWeek) + ", " + day.Day;
                f.Date = day;
                f.Sky = WeatherCodes.ToSky(SymbolToWmo(daySymbol));
                f.Max = max;
                f.Min = min;
                d.Days.Add(f);
            }
            return d;
        }

        // Код погоды MET («partlycloudy_day», «lightrainshowers_night»…) → код WMO, как у Open-Meteo
        public static int SymbolToWmo(string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return 3;
            string s = symbol.Split('_')[0];
            bool light = s.StartsWith("light"), heavy = s.StartsWith("heavy"), showers = s.Contains("showers");
            if (s.Contains("thunder")) return 95;
            if (s.Contains("snow")) return showers ? 85 : heavy ? 75 : light ? 71 : 73;
            if (s.Contains("sleet")) return 66;
            if (s.Contains("rain")) return showers ? (heavy ? 82 : light ? 80 : 81) : (heavy ? 65 : light ? 61 : 63);
            if (s == "fog") return 45;
            if (s == "partlycloudy") return 2;
            if (s == "cloudy") return 3;
            if (s == "fair") return 1;
            if (s == "clearsky") return 0;
            return 3;
        }

        // Ощущаемая температура по формуле Стедмана (ею же пользуется Open-Meteo):
        // учитывает влажность и ветер, без солнечной радиации
        public static double ApparentTemp(double t, double humidity, double wind)
        {
            double e = humidity / 100 * 6.105 * Math.Exp(17.27 * t / (237.7 + t));   // давление водяного пара, гПа
            return t + 0.33 * e - 0.70 * wind - 4.00;
        }

        // Смещение часового пояса по долготе — грубая оценка, если настоящее неизвестно
        public static int EstimateUtcOffset(double lon)
        {
            return (int)Math.Round(lon / 15) * 3600;
        }

        // День или ночь: по суффиксу символа, а если его нет — по солнечному времени
        static bool IsDay(string symbol, DateTime utc, double lon)
        {
            if (symbol != null && symbol.EndsWith("_night")) return false;
            if (symbol != null && (symbol.EndsWith("_day") || symbol.EndsWith("_polartwilight"))) return true;
            double solarHour = (utc.TimeOfDay.TotalHours + lon / 15 + 24) % 24;
            return solarHour >= 6 && solarHour < 18;
        }

        static Dictionary<string, object> Instant(Dictionary<string, object> entry, out DateTime utc)
        {
            utc = DateTime.Parse((string)entry["time"], CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            Dictionary<string, object> data = (Dictionary<string, object>)entry["data"];
            Dictionary<string, object> instant = (Dictionary<string, object>)data["instant"];
            return (Dictionary<string, object>)instant["details"];
        }

        // Символ погоды на ближайший период: 1 час, иначе 6, иначе 12 часов
        static string Symbol(Dictionary<string, object> entry)
        {
            return Symbol(entry, "next_1_hours", "next_6_hours", "next_12_hours");
        }

        // Символ погоды за первый из указанных периодов, который есть в точке ряда
        static string Symbol(Dictionary<string, object> entry, params string[] periods)
        {
            Dictionary<string, object> data = (Dictionary<string, object>)entry["data"];
            foreach (string period in periods)
            {
                object p;
                if (!data.TryGetValue(period, out p)) continue;
                Dictionary<string, object> pd = p as Dictionary<string, object>;
                object summary;
                if (pd == null || !pd.TryGetValue("summary", out summary)) continue;
                Dictionary<string, object> sd = summary as Dictionary<string, object>;
                object code;
                if (sd != null && sd.TryGetValue("symbol_code", out code) && code != null) return code.ToString();
            }
            return null;
        }

        static double Num(object o) { return Convert.ToDouble(o, CultureInfo.InvariantCulture); }
    }
}
