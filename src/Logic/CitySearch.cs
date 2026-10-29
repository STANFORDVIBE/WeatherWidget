using System;
using System.Collections.Generic;

namespace WeatherWidget
{
    // Поиск города для панели выбора: ответ Open-Meteo плюс подсказки при опечатках
    // из встроенного списка (они работают и без связи).
    static class CitySearch
    {
        public const int MaxResults = 8, MaxGuesses = 5;

        // Open-Meteo ищет названия на языке запроса: «Сидней» с language=en не находит ничего.
        // Набрали кириллицей — ищем по-русски (выбранный город потом переведётся сам).
        public static string QueryLang(string query, string uiLang)
        {
            return City.GuessLang(query) == "ru" ? "ru" : uiLang;
        }

        // Результаты для показа; null — нет связи и подсказать нечего
        public static List<GeoResult> Find(string query, string uiLang, Func<string, string> download)
        {
            string q = query.Trim();
            string lang = QueryLang(q, uiLang);
            List<GeoResult> server = null;
            try { server = OpenMeteo.ParseSearch(download(OpenMeteo.SearchUrl(q, lang)), lang); }
            catch { }
            return CitySuggest.Merge(server, CitySuggest.Find(q, uiLang, MaxGuesses), MaxResults);
        }
    }
}
