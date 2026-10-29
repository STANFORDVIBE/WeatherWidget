using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace WeatherWidget
{
    // Крупный город из встроенного списка (assets\cities.txt): ~2800 городов мира
    // от 300 тыс. жителей и города СНГ и Балтии от 30 тыс. — с названиями на двух языках
    sealed class KnownCity
    {
        public int Id;                  // номер GeoNames
        public double Lat, Lon;
        public long Population;
        public string NameEn, CountryEn, NameRu, CountryRu;
        public string KeyEn, KeyRu;     // названия для сравнения (CitySuggest.Normalize)

        public string Name(string lang) { return lang == "en" ? NameEn : NameRu; }
        public string Country(string lang) { return lang == "en" ? CountryEn : CountryRu; }
    }

    static class KnownCities
    {
        const string Resource = "WeatherWidget.cities.txt";   // встраивается в exe при сборке (build.bat)
        static readonly object sync = new object();
        static List<KnownCity> all;

        // Читается один раз, при первом поиске
        public static List<KnownCity> All
        {
            get { lock (sync) { if (all == null) all = Load(); return all; } }
        }

        static List<KnownCity> Load()
        {
            try
            {
                using (Stream s = typeof(KnownCities).Assembly.GetManifestResourceStream(Resource))
                {
                    if (s == null) return new List<KnownCity>();
                    using (StreamReader r = new StreamReader(s, Encoding.UTF8)) return Parse(r.ReadToEnd());
                }
            }
            catch { return new List<KnownCity>(); }   // без списка поиск просто работает без подсказок
        }

        // номер|широта|долгота|население|название|страна (англ.)|название|страна (рус.); строки с # — комментарии
        public static List<KnownCity> Parse(string text)
        {
            List<KnownCity> list = new List<KnownCity>();
            foreach (string line in text.Split('\n'))
            {
                string l = line.TrimEnd('\r');
                if (l == "" || l.StartsWith("#")) continue;
                string[] p = l.Split('|');
                KnownCity c = new KnownCity();
                if (p.Length != 8 || !int.TryParse(p[0], out c.Id) ||
                    !double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out c.Lat) ||
                    !double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out c.Lon) ||
                    !long.TryParse(p[3], out c.Population)) continue;
                c.NameEn = p[4]; c.CountryEn = p[5]; c.NameRu = p[6]; c.CountryRu = p[7];
                c.KeyEn = CitySuggest.Normalize(c.NameEn);
                c.KeyRu = CitySuggest.Normalize(c.NameRu);
                list.Add(c);
            }
            return list;
        }
    }
}
