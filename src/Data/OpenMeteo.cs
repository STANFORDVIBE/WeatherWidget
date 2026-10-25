using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace WeatherWidget
{
    // Результат поиска города
    sealed class GeoResult
    {
        public City City;
        public string Label;    // «Портленд, Орегон, США»
    }

    // Источник данных — Open-Meteo (https://open-meteo.com), бесплатно и без ключа.
    // Данные распространяются по лицензии CC BY 4.0.
    static class OpenMeteo
    {
        const int TimeoutMs = 15000;
        static readonly CultureInfo Ru = new CultureInfo("ru-RU");

        public static string ForecastUrl(City c)
        {
            return "https://api.open-meteo.com/v1/forecast?latitude=" + c.Lat.ToString(CultureInfo.InvariantCulture) +
                "&longitude=" + c.Lon.ToString(CultureInfo.InvariantCulture) +
                "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,is_day" +
                "&daily=weather_code,temperature_2m_max,temperature_2m_min&forecast_days=4" +
                "&wind_speed_unit=ms&timezone=auto";
        }

        public static string SearchUrl(string query)
        {
            return "https://geocoding-api.open-meteo.com/v1/search?count=8&language=ru&format=json&name=" + Uri.EscapeDataString(query);
        }

        public static string Download(string url)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = TimeoutMs;
            req.ReadWriteTimeout = TimeoutMs;
            req.UserAgent = "WeatherWidget/" + Program.Version;
            using (WebResponse resp = req.GetResponse())
            using (StreamReader r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return r.ReadToEnd();
        }

        // Текущая погода и прогноз на 3 дня (начиная с завтрашнего)
        public static WeatherData ParseForecast(City c, string json)
        {
            Dictionary<string, object> root = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
            Dictionary<string, object> cur = (Dictionary<string, object>)root["current"];
            WeatherData d = new WeatherData();
            d.City = c;
            d.Temp = Num(cur["temperature_2m"]);
            d.Feels = Num(cur["apparent_temperature"]);
            d.Wind = Num(cur["wind_speed_10m"]);
            d.Humidity = (int)Num(cur["relative_humidity_2m"]);
            d.Code = (int)Num(cur["weather_code"]);
            d.IsDay = Num(cur["is_day"]) != 0;
            d.Sky = WeatherCodes.ToSky(d.Code);
            d.UtcOffset = root.ContainsKey("utc_offset_seconds") ? (int)Num(root["utc_offset_seconds"]) : 0;

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
                    f.Label = Ru.DateTimeFormat.GetAbbreviatedDayName(dt.DayOfWeek) + ", " + dt.Day;
                    f.Sky = WeatherCodes.ToSky((int)Num(codes[i]));
                    f.Max = Num(max[i]);
                    f.Min = Num(min[i]);
                    d.Days.Add(f);
                }
            }
            catch { }   // без прогноза виджет всё равно показывает текущую погоду
            return d;
        }

        // Результаты поиска города
        public static List<GeoResult> ParseSearch(string json)
        {
            List<GeoResult> list = new List<GeoResult>();
            Dictionary<string, object> root = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
            if (!root.ContainsKey("results")) return list;
            foreach (object o in (object[])root["results"])
            {
                Dictionary<string, object> r = (Dictionary<string, object>)o;
                string name = Str(r, "name"), country = Str(r, "country"), region = Str(r, "admin1");
                if (name == "" || !r.ContainsKey("latitude") || !r.ContainsKey("longitude")) continue;
                GeoResult g = new GeoResult();
                g.City = new City(name, country, Num(r["latitude"]), Num(r["longitude"]));
                g.Label = name;
                if (region != "" && region != name) g.Label += ", " + region;
                if (country != "") g.Label += ", " + country;
                list.Add(g);
            }
            return list;
        }

        static double Num(object o) { return Convert.ToDouble(o, CultureInfo.InvariantCulture); }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }
    }
}
