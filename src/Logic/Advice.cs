using System;
using System.Collections.Generic;
using System.Globalization;

namespace WeatherWidget
{
    // Тексты подсказки: как ощущается погода и что надеть
    static class Advice
    {
        // Полный текст подсказки при наведении на температуру
        public static string TipText(WeatherData d)
        {
            return L.T("Ощущается как ", "Feels like ") + Format.Deg(d.Feels) + Units.TempLetter + "\n" +
                L.T("Ветер ", "Wind ") + Units.Wind(d.Wind) + " — " + WindFeel(d.Wind) + "\n" +
                L.T("Влажность ", "Humidity ") + d.Humidity + "% — " + HumidityFeel(d.Humidity, d.Feels) + "\n\n" +
                Clothes(d);
        }

        // Приписка к подсказке: резервный источник или устаревшие данные (пусто, если всё обычно)
        public static string SourceNote(WeatherData d, DateTime nowUtc)
        {
            string s = "";
            if (d.Stale) s += L.T("\n\nНет связи — показаны данные ", "\n\nOffline — showing data from ") + Format.Age(nowUtc - d.FetchedUtc) + ".";
            if (d.Source == MetNo.Name) s += (s == "" ? "\n\n" : "\n") + L.T("Источник: MET Norway (резервный).", "Source: MET Norway (backup).");
            return s;
        }

        // Что надеть — по температуре «как ощущается», плюс осадки и ветер
        public static string Clothes(WeatherData d)
        {
            return Clothes(d, DateTime.UtcNow);
        }

        public static string Clothes(WeatherData d, DateTime nowUtc)
        {
            string s;
            if (d.Feels >= 25) s = L.T("Жарко — футболка, шорты и головной убор.", "Hot — T-shirt, shorts and a hat.");
            else if (d.Feels >= 18) s = L.T("Тепло — можно в футболке.", "Warm — a T-shirt is fine.");
            else if (d.Feels >= 12) s = L.T("Прохладно — худи или лёгкая кофта.", "Cool — a hoodie or a light sweater.");
            else if (d.Feels >= 5) s = L.T("Свежо — ветровка или лёгкая куртка.", "Chilly — a windbreaker or a light jacket.");
            else if (d.Feels >= -5) s = L.T("Холодно — тёплая куртка и шапка.", "Cold — a warm jacket and a hat.");
            else if (d.Feels >= -15) s = L.T("Мороз — зимняя куртка, шапка, шарф, перчатки.", "Freezing — winter coat, hat, scarf, gloves.");
            else s = L.T("Сильный мороз — пуховик, термобельё, закройте лицо.", "Severe frost — down coat, thermals, cover your face.");
            string precip = PrecipNote(d, nowUtc);
            if (precip != "") s += "\n" + precip;
            if (d.Wind >= 8) s += L.T("\nСильный ветер — одевайтесь теплее.", "\nStrong wind — dress warmer.");
            return s;
        }

        const int AheadHours = 12;      // на сколько часов вперёд смотреть осадки
        const int LikelyPrecip = 40;    // с какой вероятности осадки считаются ожидаемыми, %

        // Осадки сейчас и в ближайшие часы — по почасовому прогнозу, а не только по текущему небу:
        // «С 17:00 дождь (80%) — возьмите зонт.», «Возьмите зонт — дождь до 16:00.». Пусто — если сухо.
        public static string PrecipNote(WeatherData d, DateTime nowUtc)
        {
            List<HourForecast> hours = new List<HourForecast>();
            foreach (HourForecast h in d.HoursFrom(nowUtc))
                if (h.Utc < nowUtc.AddHours(AheadHours)) hours.Add(h);

            if (d.Sky == Sky.Snow) return L.T("Непромокаемая обувь не помешает.", "Waterproof shoes won't hurt.");
            if (d.Sky == Sky.Rain)
            {
                // идёт дождь — когда закончится (первый сухой час после текущего)
                foreach (HourForecast h in hours)
                    if (h.Utc > nowUtc && !Wet(h))
                        return L.T("Возьмите зонт — дождь до ", "Take an umbrella — rain until ") + d.Local(h.Utc).ToString("HH:mm") + ".";
                return L.T("Возьмите зонт или дождевик.", "Take an umbrella or a raincoat.");
            }

            // сейчас сухо — будут ли осадки
            foreach (HourForecast h in hours)
            {
                if (!Wet(h)) continue;
                string what = WeatherCodes.Describe(h.Code), at = d.Local(h.Utc).ToString("HH:mm");
                string chance = h.Precip >= 0 ? " (" + h.Precip + "%)" : "";
                string when = L.English
                    ? what + (h.Utc <= nowUtc ? " soon" : " from " + at) + chance   // «Rain from 17:00 (80%)»
                    : (h.Utc <= nowUtc ? "Скоро " : "С " + at + " ") + what.ToLowerInvariant() + chance;
                return when + (h.Sky == Sky.Snow ? "." : L.T(" — возьмите зонт.", " — take an umbrella."));
            }
            return "";
        }

        // Осадки в этот час ожидаются: код погоды с осадками и вероятность не ниже порога (если она известна)
        static bool Wet(HourForecast h)
        {
            return (h.Sky == Sky.Rain || h.Sky == Sky.Snow) && (h.Precip < 0 || h.Precip >= LikelyPrecip);
        }

        // Ветер простыми словами (по шкале Бофорта)
        public static string WindFeel(double w)
        {
            if (w < 0.5) return L.T("штиль, ветра нет", "calm, no wind");
            if (w < 2) return L.T("лёгкий ветерок, почти не чувствуется", "light breeze, barely noticeable");
            if (w < 4) return L.T("слабый ветер, шевелит листья", "gentle breeze, rustles the leaves");
            if (w < 6) return L.T("умеренный, развевает волосы и одежду", "moderate, tousles hair and clothes");
            if (w < 9) return L.T("сильный, на улице заметно холоднее", "strong, feels noticeably colder");
            if (w < 12) return L.T("очень сильный, трудно идти против ветра", "very strong, hard to walk against");
            return L.T("штормовой, лучше переждать дома", "gale, better stay indoors");
        }

        // Влажность простыми словами: в тепле она ощущается как духота, в холоде — как промозглость
        public static string HumidityFeel(int h, double feels)
        {
            if (h < 30) return L.T("сухой воздух, сушит кожу и губы", "dry air, dries skin and lips");
            if (h < 60) return L.T("комфортно", "comfortable");
            if (h < 80)
            {
                if (feels >= 22) return L.T("влажно, немного душно", "humid, a bit stuffy");
                if (feels <= 5) return L.T("сыровато, холод чувствуется сильнее", "damp, the cold bites harder");
                return L.T("влажновато, но терпимо", "slightly humid, but fine");
            }
            if (feels >= 22) return L.T("очень влажно, душно и липко", "very humid, muggy and sticky");
            if (feels <= 5) return L.T("сыро и промозгло, холод пробирает", "raw and damp, chills to the bone");
            return L.T("очень влажно, сыро", "very humid, damp");
        }
    }

    static class Format
    {
        // Температура для показа (из °C в выбранных единицах): «+6°», «0°», «-3°»;
        // в °F без плюса — так принято в США («68°»), да и ниже нуля там бывает редко
        public static string Deg(double celsius)
        {
            int r = (int)Math.Round(Units.Temp(celsius), MidpointRounding.AwayFromZero);
            return (r > 0 && !Units.Imperial ? "+" : "") + r + "°";
        }

        // Давность данных: «только что», «25 мин назад», «3 ч назад», «2 дн. назад» (или по-английски)
        public static string Age(TimeSpan t)
        {
            if (t.TotalMinutes < 1) return L.T("только что", "just now");
            if (t.TotalHours < 1) return (int)t.TotalMinutes + L.T(" мин назад", " min ago");
            if (t.TotalDays < 1) return (int)t.TotalHours + L.T(" ч назад", " h ago");
            return (int)t.TotalDays + L.T(" дн. назад", " d ago");
        }
    }
}
