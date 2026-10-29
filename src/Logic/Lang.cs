using System;
using System.Globalization;

namespace WeatherWidget
{
    // Язык интерфейса: русский или английский. Тексты лежат прямо в коде парами
    // L.T("по-русски", "in English") — так перевод виден рядом с местом, где он нужен.
    static class L
    {
        static readonly CultureInfo Ru = new CultureInfo("ru-RU");
        static readonly CultureInfo En = new CultureInfo("en-GB");   // 24-часовое время, как в русской версии

        public static bool English { get; private set; }

        // Код языка: "ru" или "en" (он же уходит в поиск городов)
        public static string Code { get { return English ? "en" : "ru"; } }

        public static CultureInfo Culture { get { return English ? En : Ru; } }

        // Неизвестный код — английский
        public static void Use(string code) { English = code != "ru"; }

        // Язык Windows: для русского — русский, для всех остальных — английский
        public static string SystemDefault()
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en";
        }

        public static string T(string ru, string en) { return English ? en : ru; }

        // Подпись дня в прогнозе: «Пн, 26» / «Mon, 26»
        public static string DayLabel(DateTime date)
        {
            return Culture.DateTimeFormat.GetAbbreviatedDayName(date.DayOfWeek) + ", " + date.Day;
        }
    }
}
