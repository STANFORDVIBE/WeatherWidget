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

        public City(string name, string country, double lat, double lon)
        {
            Name = name ?? "";
            Country = country ?? "";
            Lat = lat;
            Lon = lon;
        }

        // Тот же город (координаты из разных источников могут чуть отличаться)
        public bool Same(City o)
        {
            return o != null && o.Name == Name && Math.Abs(o.Lat - Lat) < 0.01 && Math.Abs(o.Lon - Lon) < 0.01;
        }

        // Подпись в меню: у зарубежных городов — со страной
        public string MenuLabel
        {
            get { return Country != "" && Country != "Россия" ? Name + ", " + Country : Name; }
        }

        public override string ToString() { return MenuLabel; }
    }
}
