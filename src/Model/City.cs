using System;

namespace WeatherWidget
{
    // Город: название, страна и координаты
    sealed class City
    {
        public readonly string Name;
        public readonly string Country;
        public readonly double Lat;
        public readonly double Lon;
        public readonly string Lang;   // на каком языке название: "ru" или "en"
        public readonly int Id;        // номер в GeoNames (из поиска Open-Meteo); 0 — неизвестен

        // Язык названия не указан — угадываем по буквам: кириллица — русский
        public City(string name, string country, double lat, double lon)
            : this(name, country, lat, lon, null, 0) { }

        public City(string name, string country, double lat, double lon, string lang, int id)
        {
            Name = name ?? "";
            Country = country ?? "";
            Lat = lat;
            Lon = lon;
            Lang = lang == "ru" || lang == "en" ? lang : GuessLang(Name);
            Id = id;
        }

        public static string GuessLang(string s)
        {
            foreach (char ch in s)
                if (ch >= 'А' && ch <= 'я' || ch == 'Ё' || ch == 'ё') return "ru";
            return "en";
        }

        // Тот же город (координаты из разных источников могут чуть отличаться)
        public bool Same(City o)
        {
            return o != null && o.Name == Name && Math.Abs(o.Lat - Lat) < 0.01 && Math.Abs(o.Lon - Lon) < 0.01;
        }

        // То же место — без учёта названия (например, «Омск» и «Omsk»)
        public bool SamePlace(City o)
        {
            return o != null && Math.Abs(o.Lat - Lat) < 0.01 && Math.Abs(o.Lon - Lon) < 0.01;
        }

        // Подпись в меню: у зарубежных городов — со страной
        public string MenuLabel
        {
            get { return Country != "" && Country != "Россия" && Country != "Russia" ? Name + ", " + Country : Name; }
        }

        public override string ToString() { return MenuLabel; }
    }
}
