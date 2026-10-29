using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;

namespace WeatherWidget
{
    // Настройки виджета. Хранятся в текстовом файле key=value:
    // %APPDATA%\WeatherWidget\settings.txt
    sealed class Settings
    {
        public City Home;                    // null — город ещё не выбран
        public Point? Position;              // положение окна на экране
        public bool TopMost;
        public bool Dynamic;                 // «Динамическая погода»
        public float? Scale;                 // масштаб карточки, 1 = 100%
        public int? Transparency;            // прозрачность фона карточки, %
        public string Lang;                  // "ru" или "en"; null — язык Windows
        public readonly CustomCities Custom = new CustomCities();

        public static string DefaultPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WeatherWidget", "settings.txt");
            }
        }

        // Никогда не бросает исключений: при ошибке возвращает то, что успело прочитаться
        public static Settings Load(string path)
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(path)) return s;
                string name = null, country = "";
                double lat = double.NaN, lon = double.NaN;
                int? x = null, y = null;
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string k = line.Substring(0, eq).Trim('﻿', ' '), v = line.Substring(eq + 1);
                    double d;
                    int n;
                    float f;
                    switch (k)
                    {
                        case "city": name = v; break;
                        case "country": country = v; break;
                        case "lat": if (TryDouble(v, out d)) lat = d; break;
                        case "lon": if (TryDouble(v, out d)) lon = d; break;
                        case "x": if (int.TryParse(v, out n)) x = n; break;
                        case "y": if (int.TryParse(v, out n)) y = n; break;
                        case "topmost": s.TopMost = v == "1"; break;
                        case "dynamic": s.Dynamic = v == "1"; break;
                        case "scale":
                            if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) s.Scale = f;
                            break;
                        case "transparency": if (int.TryParse(v, out n)) s.Transparency = n; break;
                        case "lang": if (v == "ru" || v == "en") s.Lang = v; break;
                        case "fav":
                            City c = ParseFav(v);
                            if (c != null) s.Custom.Remember(c);
                            break;
                    }
                }

                if (x.HasValue && y.HasValue) s.Position = new Point(x.Value, y.Value);
                // город без координат (самые старые настройки) не восстановить — тогда виджет попросит выбрать город
                if (name != null && !double.IsNaN(lat) && !double.IsNaN(lon))
                {
                    s.Home = new City(name, country, lat, lon);
                    s.Custom.AddIfMissing(s.Home);   // выбранный город всегда есть в списке
                }
            }
            catch { }
            return s;
        }

        public bool Save(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                List<string> lines = new List<string>();
                if (Home != null)
                {
                    lines.Add("city=" + Home.Name);
                    lines.Add("country=" + Home.Country);
                    lines.Add("lat=" + Home.Lat.ToString(CultureInfo.InvariantCulture));
                    lines.Add("lon=" + Home.Lon.ToString(CultureInfo.InvariantCulture));
                }
                if (Position.HasValue)
                {
                    lines.Add("x=" + Position.Value.X);
                    lines.Add("y=" + Position.Value.Y);
                }
                lines.Add("topmost=" + (TopMost ? "1" : "0"));
                lines.Add("dynamic=" + (Dynamic ? "1" : "0"));
                if (Scale.HasValue) lines.Add("scale=" + Scale.Value.ToString("0.###", CultureInfo.InvariantCulture));
                if (Transparency.HasValue) lines.Add("transparency=" + Transparency.Value);
                if (Lang != null) lines.Add("lang=" + Lang);
                foreach (City c in Custom.Items)
                    lines.Add("fav=" + Clean(c.Name) + "|" + Clean(c.Country) + "|" +
                        c.Lat.ToString(CultureInfo.InvariantCulture) + "|" + c.Lon.ToString(CultureInfo.InvariantCulture));
                SafeFile.WriteAllLines(path, lines.ToArray());   // сбой при записи не сотрёт город и свои города
                return true;
            }
            catch { return false; }
        }

        // свой город: Название|Страна|широта|долгота
        static City ParseFav(string v)
        {
            string[] p = v.Split('|');
            double lat, lon;
            if (p.Length != 4 || !TryDouble(p[2], out lat) || !TryDouble(p[3], out lon)) return null;
            return new City(p[0], p[1], lat, lon);
        }

        static bool TryDouble(string v, out double d)
        {
            return double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d);
        }

        static string Clean(string s) { return s.Replace("|", " ").Replace("\r", " ").Replace("\n", " "); }
    }
}
