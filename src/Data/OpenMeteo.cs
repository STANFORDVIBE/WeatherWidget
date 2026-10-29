using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace WeatherWidget
{
    // Результат поиска города
    sealed class GeoResult
    {
        public City City;
        public string Label;    // «Портленд, Орегон, США»
        public long Population; // 0 — неизвестно
        public int Typos;       // > 0 — подсказка при опечатке (CitySuggest), а не ответ сервера
    }

    // Источник данных — Open-Meteo (https://open-meteo.com), бесплатно и без ключа.
    // Данные распространяются по лицензии CC BY 4.0.
    static class OpenMeteo
    {
        public const string Name = "Open-Meteo";

        public static string ForecastUrl(City c)
        {
            return "https://api.open-meteo.com/v1/forecast?latitude=" + c.Lat.ToString(CultureInfo.InvariantCulture) +
                "&longitude=" + c.Lon.ToString(CultureInfo.InvariantCulture) +
                "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,is_day" +
                "&hourly=temperature_2m,weather_code,precipitation_probability,is_day" +
                "&daily=weather_code,temperature_2m_max,temperature_2m_min&forecast_days=4" +
                "&wind_speed_unit=ms&timezone=auto";
        }

        public static string SearchUrl(string query) { return SearchUrl(query, L.Code); }

        public static string SearchUrl(string query, string lang)
        {
            return "https://geocoding-api.open-meteo.com/v1/search?count=8&language=" + lang + "&format=json&name=" + Uri.EscapeDataString(query);
        }

        // Один город по номеру GeoNames — так берётся его название на другом языке
        public static string CityUrl(int id, string lang)
        {
            return "https://geocoding-api.open-meteo.com/v1/get?id=" + id + "&language=" + lang;
        }

        // Текущая погода, почасовой прогноз на сутки и прогноз на 3 дня (начиная с завтрашнего)
        public static WeatherData ParseForecast(City c, string json)
        {
            Dictionary<string, object> root = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
            Dictionary<string, object> cur = (Dictionary<string, object>)root["current"];
            WeatherData d = new WeatherData();
            d.City = c;
            d.Source = Name;
            d.Temp = Required(cur, "temperature_2m");
            d.Feels = Required(cur, "apparent_temperature");
            d.Wind = Required(cur, "wind_speed_10m");
            d.Humidity = (int)Required(cur, "relative_humidity_2m");
            d.Code = (int)Required(cur, "weather_code");
            d.IsDay = Required(cur, "is_day") != 0;
            d.Sky = WeatherCodes.ToSky(d.Code);
            d.UtcOffset = root.ContainsKey("utc_offset_seconds") ? (int)Num(root["utc_offset_seconds"]) : 0;

            try { ParseHours(d, root, cur); }
            catch { d.Hours.Clear(); }   // без почасового прогноза виджет всё равно работает

            try
            {
                Dictionary<string, object> daily = (Dictionary<string, object>)root["daily"];
                object[] time = (object[])daily["time"];
                object[] codes = (object[])daily["weather_code"];
                object[] max = (object[])daily["temperature_2m_max"];
                object[] min = (object[])daily["temperature_2m_min"];
                for (int i = 1; i < time.Length && d.Days.Count < 3; i++)   // с завтрашнего дня
                {
                    if (codes[i] == null || max[i] == null || min[i] == null) continue;
                    DateTime dt = DateTime.ParseExact((string)time[i], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    DayForecast f = new DayForecast();
                    f.Label = L.DayLabel(dt);
                    f.Date = dt;
                    f.Sky = WeatherCodes.ToSky((int)Num(codes[i]));
                    f.Max = Num(max[i]);
                    f.Min = Num(min[i]);
                    d.Days.Add(f);
                }
            }
            catch { }   // без прогноза виджет всё равно показывает текущую погоду
            return d;
        }

        public const int HoursAhead = 24;   // сколько часов прогноза хранить

        // Почасовой прогноз от текущего часа на сутки вперёд. Время в ответе — местное (timezone=auto).
        static void ParseHours(WeatherData d, Dictionary<string, object> root, Dictionary<string, object> cur)
        {
            if (!root.ContainsKey("hourly")) return;
            Dictionary<string, object> hourly = (Dictionary<string, object>)root["hourly"];
            object[] time = (object[])hourly["time"];
            object[] temp = (object[])hourly["temperature_2m"];
            object[] codes = (object[])hourly["weather_code"];
            object[] prob = (object[])hourly["precipitation_probability"];
            object[] isDay = (object[])hourly["is_day"];
            DateTime now = LocalTime((string)cur["time"]);
            DateTime from = now.Date.AddHours(now.Hour);
            for (int i = 0; i < time.Length; i++)
            {
                DateTime local = LocalTime((string)time[i]);
                if (local < from) continue;
                if (local >= from.AddHours(HoursAhead)) break;
                if (temp[i] == null || codes[i] == null) continue;
                HourForecast h = new HourForecast();
                h.Utc = DateTime.SpecifyKind(local.AddSeconds(-d.UtcOffset), DateTimeKind.Utc);
                h.Temp = Num(temp[i]);
                h.Code = (int)Num(codes[i]);
                h.Sky = WeatherCodes.ToSky(h.Code);
                h.IsDay = isDay[i] == null ? d.IsDay : Num(isDay[i]) != 0;
                if (prob[i] != null) h.Precip = (int)Num(prob[i]);
                d.Hours.Add(h);
            }
        }

        static DateTime LocalTime(string s)
        {
            return DateTime.ParseExact(s, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
        }

        // Обязательное поле текущей погоды: пустое значение — ошибка ответа, а не «0°»
        // (иначе виджет показал бы ноль вместо того, чтобы спросить запасной источник)
        static double Required(Dictionary<string, object> d, string key)
        {
            object v;
            if (!d.TryGetValue(key, out v) || v == null) throw new FormatException("Open-Meteo: нет поля " + key);
            return Num(v);
        }

        // Результаты поиска города
        public static List<GeoResult> ParseSearch(string json) { return ParseSearch(json, L.Code); }

        // lang — язык, на котором искали (на нём названия в ответе)
        public static List<GeoResult> ParseSearch(string json, string lang)
        {
            List<GeoResult> list = new List<GeoResult>();
            Dictionary<string, object> root = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
            if (!root.ContainsKey("results")) return list;
            foreach (object o in (object[])root["results"])
            {
                Dictionary<string, object> r = (Dictionary<string, object>)o;
                City c = ToCity(r, lang);
                if (c == null) continue;
                string region = Str(r, "admin1");
                GeoResult g = new GeoResult();
                g.City = c;
                g.Label = c.Name;
                long pop;
                if (long.TryParse(Str(r, "population"), out pop)) g.Population = pop;
                if (region != "" && region != c.Name) g.Label += ", " + region;
                if (c.Country != "") g.Label += ", " + c.Country;
                list.Add(g);
            }
            return list;
        }

        // Ответ на запрос CityUrl
        public static City ParseCity(string json, string lang)
        {
            City c = ToCity((Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json), lang);
            if (c == null) throw new FormatException("Open-Meteo: город не найден");
            return c;
        }

        static City ToCity(Dictionary<string, object> r, string lang)
        {
            string name = Str(r, "name");
            if (name == "" || !r.ContainsKey("latitude") || !r.ContainsKey("longitude")) return null;
            int id;
            int.TryParse(Str(r, "id"), out id);
            return new City(name, Str(r, "country"), Num(r["latitude"]), Num(r["longitude"]), lang, id);
        }

        static double Num(object o) { return Convert.ToDouble(o, CultureInfo.InvariantCulture); }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }
    }
}
