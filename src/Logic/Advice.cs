using System;
using System.Globalization;

namespace WeatherWidget
{
    // Тексты подсказки: как ощущается погода и что надеть
    static class Advice
    {
        // Полный текст подсказки при наведении на температуру
        public static string TipText(WeatherData d)
        {
            return "Ощущается как " + Format.Deg(d.Feels) + "C\n" +
                "Ветер " + d.Wind.ToString("0.#", CultureInfo.InvariantCulture) + " м/с — " + WindFeel(d.Wind) + "\n" +
                "Влажность " + d.Humidity + "% — " + HumidityFeel(d.Humidity, d.Feels) + "\n\n" +
                Clothes(d);
        }

        // Приписка к подсказке: резервный источник или устаревшие данные (пусто, если всё обычно)
        public static string SourceNote(WeatherData d, DateTime nowUtc)
        {
            string s = "";
            if (d.Stale) s += "\n\nНет связи — показаны данные " + Format.Age(nowUtc - d.FetchedUtc) + ".";
            if (d.Source == MetNo.Name) s += (s == "" ? "\n\n" : "\n") + "Источник: MET Norway (резервный).";
            return s;
        }

        // Что надеть — по температуре «как ощущается», плюс осадки и ветер
        public static string Clothes(WeatherData d)
        {
            string s;
            if (d.Feels >= 25) s = "Жарко — футболка, шорты и головной убор.";
            else if (d.Feels >= 18) s = "Тепло — можно в футболке.";
            else if (d.Feels >= 12) s = "Прохладно — худи или лёгкая кофта.";
            else if (d.Feels >= 5) s = "Свежо — ветровка или лёгкая куртка.";
            else if (d.Feels >= -5) s = "Холодно — тёплая куртка и шапка.";
            else if (d.Feels >= -15) s = "Мороз — зимняя куртка, шапка, шарф, перчатки.";
            else s = "Сильный мороз — пуховик, термобельё, закройте лицо.";
            if (d.Sky == Sky.Rain) s += "\nВозьмите зонт или дождевик.";
            if (d.Sky == Sky.Snow) s += "\nНепромокаемая обувь не помешает.";
            if (d.Wind >= 8) s += "\nСильный ветер — одевайтесь теплее.";
            return s;
        }

        // Ветер простыми словами (по шкале Бофорта)
        public static string WindFeel(double w)
        {
            if (w < 0.5) return "штиль, ветра нет";
            if (w < 2) return "лёгкий ветерок, почти не чувствуется";
            if (w < 4) return "слабый ветер, шевелит листья";
            if (w < 6) return "умеренный, развевает волосы и одежду";
            if (w < 9) return "сильный, на улице заметно холоднее";
            if (w < 12) return "очень сильный, трудно идти против ветра";
            return "штормовой, лучше переждать дома";
        }

        // Влажность простыми словами: в тепле она ощущается как духота, в холоде — как промозглость
        public static string HumidityFeel(int h, double feels)
        {
            if (h < 30) return "сухой воздух, сушит кожу и губы";
            if (h < 60) return "комфортно";
            if (h < 80)
            {
                if (feels >= 22) return "влажно, немного душно";
                if (feels <= 5) return "сыровато, холод чувствуется сильнее";
                return "влажновато, но терпимо";
            }
            if (feels >= 22) return "очень влажно, душно и липко";
            if (feels <= 5) return "сыро и промозгло, холод пробирает";
            return "очень влажно, сыро";
        }
    }

    static class Format
    {
        // Температура для показа: «+6°», «0°», «-3°»
        public static string Deg(double v)
        {
            int r = (int)Math.Round(v, MidpointRounding.AwayFromZero);
            return (r > 0 ? "+" : "") + r + "°";
        }

        // Давность данных: «только что», «25 мин назад», «3 ч назад», «2 дн. назад»
        public static string Age(TimeSpan t)
        {
            if (t.TotalMinutes < 1) return "только что";
            if (t.TotalHours < 1) return (int)t.TotalMinutes + " мин назад";
            if (t.TotalDays < 1) return (int)t.TotalHours + " ч назад";
            return (int)t.TotalDays + " дн. назад";
        }
    }
}
