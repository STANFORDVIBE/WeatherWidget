namespace WeatherWidget
{
    // Коды погоды WMO (их возвращает Open-Meteo) → тип неба и описание на языке интерфейса
    static class WeatherCodes
    {
        public static Sky ToSky(int c)
        {
            if (c <= 1) return Sky.Sunny;
            if (c == 2) return Sky.PartlyCloudy;
            if (c == 3 || c == 45 || c == 48) return Sky.Cloudy;
            if ((c >= 71 && c <= 77) || c == 85 || c == 86) return Sky.Snow;
            return Sky.Rain;
        }

        public static string Describe(int c)
        {
            if (c == 0) return L.T("Ясно", "Clear");
            if (c == 1) return L.T("Преимущественно ясно", "Mostly clear");
            if (c == 2) return L.T("Переменная облачность", "Partly cloudy");
            if (c == 3) return L.T("Пасмурно", "Overcast");
            if (c == 45 || c == 48) return L.T("Туман", "Fog");
            if (c >= 51 && c <= 57) return L.T("Морось", "Drizzle");
            if (c >= 61 && c <= 67) return L.T("Дождь", "Rain");
            if (c >= 71 && c <= 77) return L.T("Снег", "Snow");
            if (c >= 80 && c <= 82) return L.T("Ливень", "Showers");
            if (c == 85 || c == 86) return L.T("Снегопад", "Snowfall");
            if (c >= 95) return L.T("Гроза", "Thunderstorm");
            return "";
        }
    }
}
