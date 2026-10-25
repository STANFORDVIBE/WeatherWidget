namespace WeatherWidget
{
    // Коды погоды WMO (их возвращает Open-Meteo) → тип неба и описание по-русски
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
            if (c == 0) return "Ясно";
            if (c == 1) return "Преимущественно ясно";
            if (c == 2) return "Переменная облачность";
            if (c == 3) return "Пасмурно";
            if (c == 45 || c == 48) return "Туман";
            if (c >= 51 && c <= 57) return "Морось";
            if (c >= 61 && c <= 67) return "Дождь";
            if (c >= 71 && c <= 77) return "Снег";
            if (c >= 80 && c <= 82) return "Ливень";
            if (c == 85 || c == 86) return "Снегопад";
            if (c >= 95) return "Гроза";
            return "";
        }
    }
}
