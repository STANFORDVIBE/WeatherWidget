using System;
using System.Globalization;

namespace WeatherWidget
{
    // Единицы измерения: метрические (°C, м/с) или американские (°F, mph).
    // Данные и вся логика советов — всегда в °C и м/с, переводятся только при показе.
    static class Units
    {
        public static bool Imperial { get; private set; }

        public static string Code { get { return Imperial ? "imperial" : "metric"; } }

        public static void Use(string code) { Imperial = code == "imperial"; }

        // По умолчанию — как принято в стране из настроек Windows: °F только в США, Либерии и Мьянме
        public static string SystemDefault()
        {
            try
            {
                string region = RegionInfo.CurrentRegion.TwoLetterISORegionName;
                return region == "US" || region == "LR" || region == "MM" ? "imperial" : "metric";
            }
            catch { return "metric"; }
        }

        // Температура в выбранных единицах (из °C)
        public static double Temp(double celsius) { return Imperial ? celsius * 9 / 5 + 32 : celsius; }

        public static string TempLetter { get { return Imperial ? "F" : "C"; } }

        // Скорость ветра с единицами (из м/с): «4.5 м/с», «4.5 m/s», «10 mph»
        public static string Wind(double ms)
        {
            if (Imperial) return (ms * 2.23694).ToString("0", CultureInfo.InvariantCulture) + " mph";
            return ms.ToString("0.#", CultureInfo.InvariantCulture) + L.T(" м/с", " m/s");
        }
    }
}
