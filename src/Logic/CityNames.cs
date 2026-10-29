using System;
using System.Collections.Generic;

namespace WeatherWidget
{
    // Название города на другом языке — когда пользователь сменил язык интерфейса.
    // Искать «Омск» в английском поиске ненадёжно (находится «Omskiy»), поэтому:
    // находим город поиском на языке его названия, берём номер GeoNames
    // и по номеру спрашиваем название на нужном языке.
    static class CityNames
    {
        // Город с названием на языке lang (координаты — прежние, чтобы не терялся кэш погоды).
        // null — нет связи, попробовать позже. Если перевода нет, возвращается то же название
        // с пометкой нового языка, чтобы не спрашивать сервер снова и снова.
        public static City Translate(City c, string lang, Func<string, string> download)
        {
            if (c.Lang == lang) return c;
            City builtin = Cities.Builtin(c, lang);   // города «динамической погоды» — без сети
            if (builtin != null) return builtin;
            try
            {
                int id = c.Id;
                if (id == 0)
                {
                    List<GeoResult> found = OpenMeteo.ParseSearch(download(OpenMeteo.SearchUrl(c.Name, c.Lang)), c.Lang);
                    foreach (GeoResult g in found)
                        if (g.City.SamePlace(c)) { id = g.City.Id; break; }
                    if (id == 0) return new City(c.Name, c.Country, c.Lat, c.Lon, lang, 0);
                }
                City t = OpenMeteo.ParseCity(download(OpenMeteo.CityUrl(id, lang)), lang);
                return new City(t.Name, t.Country, c.Lat, c.Lon, lang, id);
            }
            catch { return null; }
        }
    }
}
