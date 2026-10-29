using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace WeatherWidget.Tests
{
    // Автотесты логики виджета. Без внешних библиотек: каждый тест — метод,
    // проверки через Check/Eq, итог и код возврата — в Main (0 — всё прошло).
    static class Program
    {
        static int passed, failed;
        static string fixtures;

        // тестовые города (предустановленных городов в виджете нет)
        static readonly City Moscow = new City("Москва", "Россия", 55.7558, 37.6173);
        static readonly City Spb = new City("Санкт-Петербург", "Россия", 59.9343, 30.3351);
        static readonly City Omsk = new City("Омск", "Россия", 54.9885, 73.3242);

        [STAThread]
        static int Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            L.Use("ru");   // тесты пишутся по-русски, английский проверяется отдельно
            fixtures = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\tests\fixtures"));

            Run("Коды погоды", WeatherCodesMap);
            Run("Формат температуры", DegFormat);
            Run("Ветер и влажность простыми словами", Feelings);
            Run("Совет по одежде", ClothesAdvice);
            Run("Разбор прогноза Open-Meteo", ParseForecast);
            Run("Прогноз без блока daily", ParseForecastWithoutDaily);
            Run("Пустое поле в ответе Open-Meteo", ParseForecastNullField);
            Run("Прошедшие дни прогноза не показываются", UpcomingDays);
            Run("Осадки по почасовому прогнозу", PrecipAdvice);
            Run("Разбор поиска города", ParseSearch);
            Run("Списки городов", CityLists);
            Run("Свои города", CustomCitiesList);
            Run("Порядок «динамической погоды»", Rotation);
            Run("Настройки: сохранение и загрузка", SettingsRoundTrip);
            Run("Настройки: старый формат файла", SettingsLegacy);
            Run("Настройки: повреждённый и отсутствующий файл", SettingsBroken);
            Run("MET Norway: коды погоды", MetNoSymbols);
            Run("MET Norway: ощущаемая температура и часовой пояс", MetNoFormulas);
            Run("MET Norway: разбор прогноза", MetNoParse);
            Run("Кэш погоды", Cache);
            Run("Цепочка источников: Open-Meteo → MET Norway → кэш", ServiceFallback);
            Run("Медленная сеть: источники наперегонки", SlowNetwork);
            Run("Давность данных и приписка к подсказке", AgeAndNotes);
            Run("Окно: размер при запуске с разным масштабом", WindowSizeOnStart);
            Run("Первый запуск: город не выбран", FirstStart);
            Run("Панель выбора города", Picker);
            Run("Подсказка в стиле виджета", Tooltip);
            Run("Прозрачность фона", Transparency);
            Run("Английский язык", English);
            Run("Названия своих городов при смене языка", CityNamesTranslate);

            Console.WriteLine();
            Console.WriteLine(failed == 0 ? "ВСЕ ТЕСТЫ ПРОШЛИ: " + passed + " проверок" : "ОШИБКИ: " + failed + " из " + (passed + failed));
            return failed == 0 ? 0 : 1;
        }

        // ---------- мини-фреймворк ----------

        static void Run(string name, Action test)
        {
            int before = failed;
            try { test(); }
            catch (Exception e) { Fail("исключение: " + e.GetType().Name + ": " + e.Message); }
            Console.WriteLine((failed == before ? "  OK    " : "  FAIL  ") + name);
        }

        static void Check(bool cond, string what)
        {
            if (cond) passed++; else Fail(what);
        }

        static void Eq<T>(T expected, T actual, string what)
        {
            if (Equals(expected, actual)) passed++;
            else Fail(what + ": ожидалось «" + expected + "», получено «" + actual + "»");
        }

        static void Fail(string what)
        {
            failed++;
            Console.WriteLine("        ✗ " + what);
        }

        static string Fixture(string name) { return File.ReadAllText(Path.Combine(fixtures, name), Encoding.UTF8); }

        static string TempFile()
        {
            return Path.Combine(Path.GetTempPath(), "WeatherWidget.Tests", Guid.NewGuid().ToString("N"), "settings.txt");
        }

        // ---------- тесты ----------

        static void WeatherCodesMap()
        {
            Eq(Sky.Sunny, WeatherCodes.ToSky(0), "0 — ясно");
            Eq(Sky.Sunny, WeatherCodes.ToSky(1), "1 — преимущественно ясно");
            Eq(Sky.PartlyCloudy, WeatherCodes.ToSky(2), "2 — переменная облачность");
            Eq(Sky.Cloudy, WeatherCodes.ToSky(3), "3 — пасмурно");
            Eq(Sky.Cloudy, WeatherCodes.ToSky(45), "45 — туман");
            Eq(Sky.Rain, WeatherCodes.ToSky(53), "53 — морось");
            Eq(Sky.Rain, WeatherCodes.ToSky(63), "63 — дождь");
            Eq(Sky.Snow, WeatherCodes.ToSky(73), "73 — снег");
            Eq(Sky.Snow, WeatherCodes.ToSky(86), "86 — снегопад");
            Eq(Sky.Rain, WeatherCodes.ToSky(95), "95 — гроза");
            Eq("Пасмурно", WeatherCodes.Describe(3), "описание 3");
            Eq("Снегопад", WeatherCodes.Describe(85), "описание 85");
            Eq("Гроза", WeatherCodes.Describe(99), "описание 99");
        }

        static void DegFormat()
        {
            Eq("0°", Format.Deg(0.4), "0.4");
            Eq("0°", Format.Deg(-0.4), "-0.4 — без «-0»");
            Eq("+1°", Format.Deg(0.5), "0.5 округляется вверх");
            Eq("-3°", Format.Deg(-2.5), "-2.5 округляется от нуля");
            Eq("+12°", Format.Deg(12.49), "12.49");
        }

        static void Feelings()
        {
            Eq("штиль, ветра нет", Advice.WindFeel(0), "ветер 0");
            Eq("лёгкий ветерок, почти не чувствуется", Advice.WindFeel(1.9), "ветер 1.9");
            Eq("слабый ветер, шевелит листья", Advice.WindFeel(2), "ветер 2 — граница");
            Eq("штормовой, лучше переждать дома", Advice.WindFeel(15), "ветер 15");
            Eq("сухой воздух, сушит кожу и губы", Advice.HumidityFeel(25, 0), "влажность 25%");
            Eq("комфортно", Advice.HumidityFeel(58, 18), "влажность 58%");
            Eq("сыро и промозгло, холод пробирает", Advice.HumidityFeel(85, 2), "85% в холод");
            Eq("очень влажно, душно и липко", Advice.HumidityFeel(82, 27), "82% в жару");
        }

        static void ClothesAdvice()
        {
            WeatherData d = new WeatherData();
            d.Feels = 19; d.Sky = Sky.Sunny; d.Wind = 2;
            Check(Advice.Clothes(d).StartsWith("Тепло — можно в футболке"), "+19 — футболка");
            d.Feels = 14;
            Check(Advice.Clothes(d).Contains("худи"), "+14 — худи");
            d.Feels = 2; d.Sky = Sky.Rain;
            string s = Advice.Clothes(d);
            Check(s.Contains("тёплая куртка") && s.Contains("зонт"), "+2 и дождь — куртка и зонт");
            d.Feels = -20; d.Sky = Sky.Snow; d.Wind = 10;
            s = Advice.Clothes(d);
            Check(s.Contains("пуховик") && s.Contains("обувь") && s.Contains("Сильный ветер"), "-20, снег, ветер");
            d.Humidity = 85;
            Check(Advice.TipText(d).StartsWith("Ощущается как -20°C\nВетер 10 м/с"), "текст подсказки");
        }

        static void ParseForecast()
        {
            City moscow = Moscow;
            WeatherData d = OpenMeteo.ParseForecast(moscow, Fixture("forecast.json"));
            Check(ReferenceEquals(moscow, d.City), "город сохраняется");
            Eq(7.4, d.Temp, "температура");
            Eq(3.9, d.Feels, "ощущается");
            Eq(4.2, d.Wind, "ветер");
            Eq(81, d.Humidity, "влажность");
            Eq(61, d.Code, "код погоды");
            Eq(Sky.Rain, d.Sky, "тип неба");
            Eq(false, d.IsDay, "ночь");
            Eq(10800, d.UtcOffset, "часовой пояс");
            // сегодня пропускается, день с null — тоже: остаются 26 (Пн) и 28 (Ср)
            Eq(2, d.Days.Count, "дней в прогнозе");
            if (d.Days.Count == 2)
            {
                Eq("Пн, 26", d.Days[0].Label, "подпись дня 1");
                Eq(Sky.Cloudy, d.Days[0].Sky, "небо дня 1");
                Eq(8.0, d.Days[0].Max, "максимум дня 1");
                Eq("Ср, 28", d.Days[1].Label, "подпись дня 2");
                Eq(Sky.Snow, d.Days[1].Sky, "небо дня 2");
                Eq(-2.4, d.Days[1].Min, "минимум дня 2");
                Eq(new DateTime(2026, 10, 26), d.Days[0].Date, "дата дня 1");
            }

            // по часам: с текущего часа (18:00 местного) на сутки, час с null пропущен
            Eq(23, d.Hours.Count, "часов в прогнозе");
            if (d.Hours.Count == 23)
            {
                HourForecast h = d.Hours[0];
                Eq(new DateTime(2026, 10, 25, 15, 0, 0, DateTimeKind.Utc), h.Utc, "первый час — 18:00 МСК в UTC");
                Eq(DateTimeKind.Utc, h.Utc.Kind, "время часа — UTC");
                Eq(61, h.Code, "код часа");
                Eq(Sky.Rain, h.Sky, "небо часа");
                Eq(70, h.Precip, "вероятность осадков");
                Eq(false, h.IsDay, "ночь");
                Eq(3.4, h.Temp, "температура часа");
                Check(!d.Hours.Exists(x => x.Utc == new DateTime(2026, 10, 26, 3, 0, 0)), "час без температуры пропущен");
                HourForecast noProb = d.Hours.Find(x => x.Utc == new DateTime(2026, 10, 26, 6, 0, 0));
                Eq(-1, noProb != null ? noProb.Precip : -99, "вероятность null — неизвестна");
            }
        }

        static void ParseForecastNullField()
        {
            string json = Fixture("forecast.json").Replace("\"temperature_2m\":7.4", "\"temperature_2m\":null");
            bool threw = false;
            try { OpenMeteo.ParseForecast(Moscow, json); }
            catch (FormatException) { threw = true; }
            Check(threw, "пустая температура — ошибка ответа (дальше запасной источник), а не «0°»");
        }

        static void UpcomingDays()
        {
            WeatherData d = OpenMeteo.ParseForecast(Moscow, Fixture("forecast.json"));   // дни 26 и 28 октября, МСК
            Eq(2, d.UpcomingDays(new DateTime(2026, 10, 25, 15, 30, 0, DateTimeKind.Utc), 3).Count, "25-го видны оба дня");
            List<DayForecast> later = d.UpcomingDays(new DateTime(2026, 10, 26, 22, 0, 0, DateTimeKind.Utc), 3);
            Eq(1, later.Count, "в 01:00 27-го по Москве — только 28-е");
            if (later.Count == 1) Eq("Ср, 28", later[0].Label, "оставшийся день");
            Eq(0, d.UpcomingDays(new DateTime(2026, 10, 28, 21, 30, 0, DateTimeKind.Utc), 3).Count, "все дни прошли");
            Eq(1, d.UpcomingDays(new DateTime(2026, 10, 25, 15, 30, 0, DateTimeKind.Utc), 1).Count, "не больше max");
            d.Days.Add(new DayForecast { Label = "Чт, 29" });
            Eq(1, d.UpcomingDays(new DateTime(2026, 10, 28, 21, 30, 0, DateTimeKind.Utc), 3).Count, "день без даты (старый кэш) показывается");
        }

        // Погода с почасовым прогнозом: МСК, сейчас 18:30 (15:30 UTC), часы с 18:00 местного
        static readonly DateTime Now1830 = new DateTime(2026, 10, 25, 15, 30, 0, DateTimeKind.Utc);

        static WeatherData WithHours(Sky now, int hours, params int[] wet)   // wet: номер часа, код, вероятность, …
        {
            WeatherData d = new WeatherData();
            d.Feels = 10;
            d.Sky = now;
            d.UtcOffset = 10800;
            for (int i = 0; i < hours; i++)
            {
                HourForecast h = new HourForecast();
                h.Utc = new DateTime(2026, 10, 25, 15, 0, 0, DateTimeKind.Utc).AddHours(i);
                h.Code = 2;
                h.Precip = 10;
                for (int k = 0; k + 2 < wet.Length; k += 3)
                    if (wet[k] == i) { h.Code = wet[k + 1]; h.Precip = wet[k + 2]; }
                h.Sky = WeatherCodes.ToSky(h.Code);
                d.Hours.Add(h);
            }
            return d;
        }

        static void PrecipAdvice()
        {
            Eq("С 21:00 дождь (80%) — возьмите зонт.", Advice.PrecipNote(WithHours(Sky.Cloudy, 16, 3, 61, 80), Now1830), "сухо, дождь через 3 часа");
            WeatherData unlikely = WithHours(Sky.Cloudy, 16, 3, 61, 30);
            Eq("", Advice.PrecipNote(unlikely, Now1830), "дождь с вероятностью 30% — не пугаем");
            Check(!Advice.Clothes(unlikely, Now1830).Contains("зонт"), "и в совете по одежде зонта нет");
            Eq("С 21:00 снег (60%).", Advice.PrecipNote(WithHours(Sky.Cloudy, 16, 3, 73, 60), Now1830), "снег — без зонта");
            Eq("С 21:00 ливень — возьмите зонт.", Advice.PrecipNote(WithHours(Sky.Cloudy, 16, 3, 80, -1), Now1830), "вероятность неизвестна (MET Norway)");
            Eq("Скоро дождь (70%) — возьмите зонт.", Advice.PrecipNote(WithHours(Sky.Cloudy, 16, 0, 61, 70), Now1830), "дождь уже в текущем часе");
            Eq("", Advice.PrecipNote(WithHours(Sky.Cloudy, 16, 13, 61, 90), Now1830), "дальше 12 часов не смотрим");
            Eq("Возьмите зонт — дождь до 20:00.", Advice.PrecipNote(WithHours(Sky.Rain, 16, 0, 61, 90, 1, 61, 90), Now1830), "идёт дождь — до какого часа");
            Eq("Возьмите зонт или дождевик.", Advice.PrecipNote(WithHours(Sky.Rain, 16, 0, 61, 90, 1, 61, 90, 2, 61, 90, 3, 61, 90, 4, 61, 90,
                5, 61, 90, 6, 61, 90, 7, 61, 90, 8, 61, 90, 9, 61, 90, 10, 61, 90, 11, 61, 90, 12, 61, 90), Now1830), "дождь не кончится");
            Eq("Непромокаемая обувь не помешает.", Advice.PrecipNote(WithHours(Sky.Snow, 16), Now1830), "идёт снег");

            // прогноз устарел (кэш двухдневной давности) — только по текущему небу
            DateTime later = Now1830.AddDays(2);
            Eq("Возьмите зонт или дождевик.", Advice.PrecipNote(WithHours(Sky.Rain, 16, 0, 61, 90), later), "старый прогноз, идёт дождь");
            Eq("", Advice.PrecipNote(WithHours(Sky.Cloudy, 16, 3, 61, 90), later), "старый прогноз не пугает дождём");

            WeatherData real = OpenMeteo.ParseForecast(Moscow, Fixture("forecast.json"));
            Eq("Возьмите зонт — дождь до 20:00.", Advice.PrecipNote(real, Now1830), "по ответу Open-Meteo");
        }

        static void ParseForecastWithoutDaily()
        {
            WeatherData d = OpenMeteo.ParseForecast(Cities.World[0], Fixture("forecast-no-daily.json"));
            Eq(18.6, d.Temp, "температура");
            Eq(true, d.IsDay, "день");
            Eq(-25200, d.UtcOffset, "часовой пояс −7");
            Eq(0, d.Days.Count, "без прогноза — пустой список, а не ошибка");
        }

        static void ParseSearch()
        {
            List<GeoResult> r = OpenMeteo.ParseSearch(Fixture("search.json"));
            Eq(2, r.Count, "результат без координат пропущен");
            if (r.Count == 2)
            {
                Eq("Берлин, Германия", r[0].Label, "регион совпадает с названием — не повторяется");
                Eq("Берлин, Нью-Гэмпшир, США", r[1].Label, "одноимённый город — с регионом");
                Eq("США", r[1].City.Country, "страна");
                Check(Math.Abs(r[1].City.Lon + 71.18508) < 1e-9, "долгота");
            }
            Eq(0, OpenMeteo.ParseSearch(Fixture("search-empty.json")).Count, "ничего не найдено");
            Check(OpenMeteo.SearchUrl("Нью-Йорк").EndsWith("name=%D0%9D%D1%8C%D1%8E-%D0%99%D0%BE%D1%80%D0%BA"), "запрос кодируется в URL");
            Check(OpenMeteo.ForecastUrl(Cities.World[0]).Contains("latitude=34.0522&longitude=-118.2437"), "координаты в URL — с точкой");
        }

        static void CityLists()
        {
            Eq(35, Cities.World.Length, "городов «динамической погоды»");
            HashSet<string> names = new HashSet<string>();
            foreach (City c in Cities.World)
            {
                Check(names.Add(c.Name), "без повторов: " + c.Name);
                Check(c.Country != "", "страна указана: " + c.Name);
                Check(Math.Abs(c.Lat) <= 90 && Math.Abs(c.Lon) <= 180, "координаты в допустимых пределах: " + c.Name);
            }
            Eq("Лондон, Великобритания", Cities.World[2].MenuLabel, "подпись зарубежного города");
            Eq("Омск", Omsk.MenuLabel, "подпись российского города — без страны");
            Check(Omsk.Same(new City("Омск", "Россия", 54.99, 73.32)), "тот же город при чуть других координатах");
        }

        static void CustomCitiesList()
        {
            CustomCities list = new CustomCities();
            Eq(0, list.Count, "сначала своих городов нет — предустановленных тоже");
            list.Remember(null);
            Eq(0, list.Count, "пустой город не добавляется");

            City london = new City("Лондон", "Великобритания", 51.5085, -0.12574);
            City kazan = new City("Казань", "Россия", 55.7887, 49.1221);
            list.Remember(london);
            list.Remember(kazan);
            list.Remember(new City("Лондон", "Великобритания", 51.5074, -0.1278));   // тот же город, другие координаты
            Eq(2, list.Count, "повторный выбор не создаёт дубль");
            Eq("Лондон", list.Items[1].Name, "повторно выбранный — в конце списка");

            list.AddIfMissing(kazan);
            Eq("Казань", list.Items[0].Name, "AddIfMissing не меняет порядок");

            for (int i = 1; i <= 12; i++) list.Remember(new City("Город" + i, "Тест", i, i));
            Eq(CustomCities.Max, list.Count, "не больше " + CustomCities.Max);
            Eq("Город3", list.Items[0].Name, "вытесняются самые старые");

            list.Remove(new City("Город5", "Тест", 5, 5));
            Check(!list.Contains(new City("Город5", "Тест", 5, 5)), "удаление");
            Eq(CustomCities.Max - 1, list.Count, "после удаления");
        }

        static void Rotation()
        {
            CityRotation rot = new CityRotation(Cities.World, new Random(42));
            int n = Cities.World.Length, rounds = 500;
            string prevCountry = null;
            int sameCountry = 0, badRounds = 0;
            HashSet<string> firsts = new HashSet<string>();
            for (int r = 0; r < rounds; r++)
            {
                HashSet<string> seen = new HashSet<string>();
                for (int i = 0; i < n; i++)
                {
                    City c = rot.Next();
                    if (i == 0) firsts.Add(c.Name);
                    seen.Add(c.Name);
                    if (c.Country == prevCountry) sameCountry++;
                    prevCountry = c.Country;
                }
                if (seen.Count != n) badRounds++;
            }
            Eq(0, badRounds, "каждый круг — все города ровно по разу");
            Eq(0, sameCountry, "два города одной страны подряд");
            Check(firsts.Count > n / 2, "круги начинаются с разных городов (" + firsts.Count + ")");

            City before = rot.Next();
            rot.Restart();
            Check(rot.Next().Country != before.Country, "Restart: новый круг без повтора страны на стыке");
        }

        static void SettingsRoundTrip()
        {
            string path = TempFile();
            Settings s = new Settings();
            s.Home = new City("Владивосток", "Россия", 43.10562, 131.87354);
            s.Position = new Point(8, -1069);
            s.TopMost = true;
            s.Dynamic = true;
            s.Scale = 0.875f;
            s.Custom.Remember(new City("Лондон", "Великобритания", 51.5085, -0.12574));
            s.Custom.Remember(new City("Город|с чертой", "Тест", 1, 2));
            Check(s.Save(path), "сохранение");

            Settings l = Settings.Load(path);
            Check(l.Home != null && l.Home.Same(s.Home), "свой город");
            Eq("Россия", l.Home != null ? l.Home.Country : null, "страна своего города");
            Eq(new Point(8, -1069), l.Position.GetValueOrDefault(), "положение (в т.ч. на мониторе сверху)");
            Eq(true, l.TopMost, "поверх окон");
            Eq(true, l.Dynamic, "динамическая погода");
            Check(l.Scale.HasValue && Math.Abs(l.Scale.Value - 0.875f) < 0.001, "масштаб");
            Check(l.Custom.Contains(s.Home), "выбранный город попадает в свои");
            Eq(3, l.Custom.Count, "свои города");
            Check(l.Custom.Items[1].Name == "Город с чертой", "символ-разделитель в названии не ломает файл");
            Directory.Delete(Path.GetDirectoryName(path), true);
        }

        static void SettingsLegacy()
        {
            string path = TempFile();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // самая первая версия: только название города, файл с BOM
            File.WriteAllText(path, "city=Омск\r\nx=10\r\ny=20\r\ntopmost=0\r\n", new UTF8Encoding(true));
            Settings l = Settings.Load(path);
            Check(l.Home == null, "город без координат не восстановить — виджет попросит выбрать город");
            Eq(new Point(10, 20), l.Position.GetValueOrDefault(), "положение");
            Check(!l.Scale.HasValue, "масштаб не задан");
            Eq(0, l.Custom.Count, "своих городов нет");
            Directory.Delete(Path.GetDirectoryName(path), true);
        }

        static void MetNoSymbols()
        {
            Eq(0, MetNo.SymbolToWmo("clearsky_night"), "ясно");
            Eq(1, MetNo.SymbolToWmo("fair_day"), "малооблачно");
            Eq(2, MetNo.SymbolToWmo("partlycloudy_polartwilight"), "переменная облачность");
            Eq(3, MetNo.SymbolToWmo("cloudy"), "облачно");
            Eq(45, MetNo.SymbolToWmo("fog"), "туман");
            Eq(61, MetNo.SymbolToWmo("lightrain"), "слабый дождь");
            Eq(65, MetNo.SymbolToWmo("heavyrain"), "сильный дождь");
            Eq(80, MetNo.SymbolToWmo("lightrainshowers_day"), "слабый ливень");
            Eq(66, MetNo.SymbolToWmo("sleet"), "мокрый снег");
            Eq(Sky.Rain, WeatherCodes.ToSky(MetNo.SymbolToWmo("sleet")), "мокрый снег — анимация дождя");
            Eq(73, MetNo.SymbolToWmo("snow"), "снег");
            Eq(95, MetNo.SymbolToWmo("lightssnowshowersandthunder_night"), "опечатка MET «lightssnow…», гроза важнее снега");
            Eq(85, MetNo.SymbolToWmo("lightssnowshowers_day"), "опечатка MET «lightssnow…» — снегопад");
            Eq(3, MetNo.SymbolToWmo(null), "без символа — облачно");
            Eq(3, MetNo.SymbolToWmo("что-то новое"), "неизвестный символ — облачно");
        }

        static void MetNoFormulas()
        {
            Check(Math.Abs(MetNo.ApparentTemp(7.4, 54.8, 4.5) - 2.1) < 0.1, "+7.4°, 55%, 4.5 м/с → около +2°");
            Check(MetNo.ApparentTemp(30, 80, 1) > 30, "жара и влажность — ощущается жарче");
            Check(MetNo.ApparentTemp(-10, 70, 10) < -15, "мороз и ветер — ощущается холоднее");
            Eq(3 * 3600, MetNo.EstimateUtcOffset(37.6), "Москва по долготе — UTC+3");
            Eq(-8 * 3600, MetNo.EstimateUtcOffset(-118.24), "Лос-Анджелес по долготе — UTC−8");
            Check(MetNo.ForecastUrl(new City("Тест", "", 55.755831, 37.617299)).EndsWith("lat=55.7558&lon=37.6173"), "не больше 4 знаков в координатах");
        }

        static void MetNoParse()
        {
            WeatherData d = MetNo.ParseForecast(Moscow, Fixture("metno.json"), 10800);
            Eq(MetNo.Name, d.Source, "источник");
            Eq(7.4, d.Temp, "температура");
            Eq(55, d.Humidity, "влажность округляется");
            Eq(4.5, d.Wind, "ветер");
            Check(Math.Abs(d.Feels - 2.1) < 0.1, "ощущается — по формуле Стедмана");
            Eq(0, d.Code, "clearsky_night → ясно");
            Eq(false, d.IsDay, "ночь по суффиксу _night");
            Eq(10800, d.UtcOffset, "часовой пояс берётся извне");
            Eq(9, d.Hours.Count, "часы — точки с часовым символом в пределах суток");
            if (d.Hours.Count > 0)
            {
                Eq(new DateTime(2026, 10, 25, 16, 0, 0), d.Hours[0].Utc, "первый час");
                Eq(-1, d.Hours[0].Precip, "вероятность осадков неизвестна");
                Eq(false, d.Hours[0].IsDay, "ночь");
            }
            Eq(new DateTime(2026, 10, 26), d.Days.Count > 0 ? d.Days[0].Date : DateTime.MinValue, "дата дня");
            Eq(3, d.Days.Count, "3 дня прогноза");
            if (d.Days.Count == 3)
            {
                Eq("Пн, 26", d.Days[0].Label, "день 1");
                Eq("Вт, 27", d.Days[1].Label, "день 2");
                Eq("Ср, 28", d.Days[2].Label, "день 3");
                foreach (DayForecast f in d.Days)
                    Check(f.Max >= f.Min && f.Max < 40 && f.Min > -40, "разумные max/min: " + f.Label);
            }
        }

        static void Cache()
        {
            string dir = Path.Combine(Path.GetTempPath(), "WeatherWidget.Tests", Guid.NewGuid().ToString("N"));
            WeatherCache cache = new WeatherCache(dir);
            City c = Moscow;
            Check(cache.Load(c, TimeSpan.FromDays(3)) == null, "пустой кэш");

            WeatherData d = OpenMeteo.ParseForecast(c, Fixture("forecast.json"));
            d.FetchedUtc = DateTime.UtcNow.AddHours(-2);
            cache.Save(d);
            WeatherData l = cache.Load(c, TimeSpan.FromDays(3));
            Check(l != null, "загрузка");
            if (l != null)
            {
                Eq(d.Temp, l.Temp, "температура");
                Eq(d.Feels, l.Feels, "ощущается");
                Eq(d.Code, l.Code, "код");
                Eq(d.Sky, l.Sky, "небо");
                Eq(d.IsDay, l.IsDay, "день/ночь");
                Eq(d.UtcOffset, l.UtcOffset, "часовой пояс");
                Eq(OpenMeteo.Name, l.Source, "источник");
                Check(Math.Abs((l.FetchedUtc - d.FetchedUtc).TotalSeconds) < 1, "время получения");
                Eq(d.Days.Count, l.Days.Count, "дни прогноза");
                if (l.Days.Count > 0) Eq("Пн, 26", l.Days[0].Label, "подпись дня");
                if (l.Days.Count > 0) Eq(new DateTime(2026, 10, 26), l.Days[0].Date, "дата дня");
                Eq(d.Hours.Count, l.Hours.Count, "часы прогноза");
                if (l.Hours.Count > 0)
                {
                    Eq(d.Hours[0].Utc, l.Hours[0].Utc, "время часа");
                    Eq(DateTimeKind.Utc, l.Hours[0].Utc.Kind, "время часа — UTC");
                    Eq(d.Hours[0].Temp, l.Hours[0].Temp, "температура часа");
                    Eq(d.Hours[0].Precip, l.Hours[0].Precip, "вероятность осадков");
                    Eq(d.Hours[0].Sky, l.Hours[0].Sky, "небо часа");
                    Eq(d.Hours[0].IsDay, l.Hours[0].IsDay, "день/ночь часа");
                }
                Check(!l.Stale, "сам кэш не помечает данные устаревшими");
            }
            Check(Directory.GetFiles(dir, "*.tmp").Length == 0, "временный файл не остаётся");
            cache.Save(d);
            Check(cache.Load(c, TimeSpan.FromDays(3)) != null, "перезапись поверх старого файла");
            Check(cache.Load(c, TimeSpan.FromHours(1)) == null, "старше допустимого — не используется");
            Check(cache.Load(Spb, TimeSpan.FromDays(3)) == null, "другой город — свой файл");

            // кэш прошлой версии: дни без даты, без часов
            File.WriteAllText(Directory.GetFiles(dir)[0], "fetched=" + DateTime.UtcNow.ToString("o") + "\ntemp=5\ncode=3\nday=Пн, 26|Cloudy|8|3\n", Encoding.UTF8);
            WeatherData legacy = cache.Load(c, TimeSpan.FromDays(3));
            Check(legacy != null && legacy.Days.Count == 1 && legacy.Days[0].Date == DateTime.MinValue && legacy.Hours.Count == 0,
                "кэш прошлой версии читается: день без даты, без часов");

            File.WriteAllText(Directory.GetFiles(dir)[0], "мусор\nfetched=вчера\n", Encoding.UTF8);
            Check(cache.Load(c, TimeSpan.FromDays(3)) == null, "повреждённый файл — null, а не ошибка");
            Directory.Delete(dir, true);
        }

        static void ServiceFallback()
        {
            string dir = Path.Combine(Path.GetTempPath(), "WeatherWidget.Tests", Guid.NewGuid().ToString("N"));
            bool omUp = true, metUp = true;
            int omCalls = 0, metCalls = 0;
            string omJson = Fixture("forecast.json"), metJson = Fixture("metno.json");
            Func<string, string> fakeNet = delegate (string url)
            {
                if (url.Contains("open-meteo")) { omCalls++; if (omUp) return omJson; }
                else if (url.Contains("met.no")) { metCalls++; if (metUp) return metJson; }
                throw new System.Net.WebException("нет связи");
            };
            WeatherService svc = new WeatherService(fakeNet, new WeatherCache(dir), 0);
            City moscow = Moscow;

            WeatherData d = svc.Get(moscow);
            Check(d != null && d.Source == OpenMeteo.Name && !d.Stale, "всё работает — Open-Meteo");
            Eq(0, metCalls, "резерв не трогаем, если основной ответил");

            omUp = false;
            d = svc.Get(moscow);
            Check(d != null && d.Source == MetNo.Name && !d.Stale, "Open-Meteo упал — MET Norway");
            Eq(10800, d != null ? d.UtcOffset : 0, "часовой пояс для MET — последний известный от Open-Meteo");
            Eq(3, omCalls, "Open-Meteo — повтор при сбое (1 + 2 попытки)");

            metUp = false;
            d = svc.Get(moscow);
            Check(d != null && d.Stale, "оба упали — кэш, помеченный как устаревший");
            Eq(MetNo.Name, d != null ? d.Source : null, "в кэше — последние полученные данные");

            Check(svc.Get(Spb) == null, "оба упали и кэша нет — null");

            // новый запуск программы: часовой пояс для MET берётся из кэша Open-Meteo, иначе — оценка по долготе
            omUp = true;
            WeatherService first = new WeatherService(fakeNet, new WeatherCache(dir), 0);
            first.Get(Spb);   // в кэше Петербурга — данные Open-Meteo
            omUp = false; metUp = true;
            WeatherService restarted = new WeatherService(fakeNet, new WeatherCache(dir), 0);
            WeatherData spb = restarted.Get(Spb);
            Eq(10800, spb != null ? spb.UtcOffset : 0, "пояс из кэша");
            WeatherData la = restarted.Get(Cities.World[0]);
            Eq(-8 * 3600, la != null ? la.UtcOffset : 0, "пояс по долготе, если ничего не известно");
            Directory.Delete(dir, true);
        }

        // Медленный или зависший сервис не должен задерживать погоду: через metDelay подключается
        // запасной источник, дольше totalTimeout не ждём (тогда кэш)
        static void SlowNetwork()
        {
            string dir = Path.Combine(Path.GetTempPath(), "WeatherWidget.Tests", Guid.NewGuid().ToString("N"));
            string omJson = Fixture("forecast.json"), metJson = Fixture("metno.json");
            int omDelay = 0, metDelay = 0;
            bool omUp = true, metUp = true;
            int metCalls = 0;
            Func<string, string> net = delegate (string url)
            {
                bool om = url.Contains("open-meteo");
                if (!om) metCalls++;
                System.Threading.Thread.Sleep(om ? omDelay : metDelay);
                if (om ? omUp : metUp) return om ? omJson : metJson;
                throw new System.Net.WebException("нет связи");
            };
            WeatherService svc = new WeatherService(net, new WeatherCache(dir), 0, 300, 1500);
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

            sw.Restart(); WeatherData d = svc.Get(Moscow);
            Check(d != null && d.Source == OpenMeteo.Name && sw.ElapsedMilliseconds < 300, "быстрый Open-Meteo — сразу (" + sw.ElapsedMilliseconds + " мс)");
            Eq(0, metCalls, "запасной не трогаем, если основной ответил быстро");

            omDelay = 3000;   // Open-Meteo «завис»
            sw.Restart(); d = svc.Get(Moscow);
            Check(d != null && d.Source == MetNo.Name && sw.ElapsedMilliseconds < 800, "Open-Meteo завис — MET Norway через ~0,3 с (" + sw.ElapsedMilliseconds + " мс)");

            omDelay = 0; omUp = false; metDelay = 200;   // Open-Meteo сразу падает
            sw.Restart(); d = svc.Get(Moscow);
            Check(d != null && d.Source == MetNo.Name && sw.ElapsedMilliseconds < 600, "Open-Meteo упал — MET Norway без ожидания (" + sw.ElapsedMilliseconds + " мс)");

            omUp = true; omDelay = 4000; metDelay = 4000;   // оба зависли
            sw.Restart(); d = svc.Get(Moscow);
            Check(d != null && d.Stale && sw.ElapsedMilliseconds < 2200, "оба зависли — кэш не позже общего лимита (" + sw.ElapsedMilliseconds + " мс)");

            System.Threading.Thread.Sleep(4500);   // дождаться зависших «запросов», прежде чем удалять папку
            Directory.Delete(dir, true);
        }

        static void AgeAndNotes()
        {
            Eq("только что", Format.Age(TimeSpan.FromSeconds(20)), "секунды");
            Eq("25 мин назад", Format.Age(TimeSpan.FromMinutes(25.7)), "минуты");
            Eq("3 ч назад", Format.Age(TimeSpan.FromHours(3.9)), "часы");
            Eq("2 дн. назад", Format.Age(TimeSpan.FromDays(2.5)), "дни");

            WeatherData d = new WeatherData();
            d.Source = OpenMeteo.Name;
            Eq("", Advice.SourceNote(d, DateTime.UtcNow), "обычные данные — без приписки");
            d.Source = MetNo.Name;
            Check(Advice.SourceNote(d, DateTime.UtcNow).Contains("MET Norway"), "резервный источник указан");
            d.Stale = true;
            d.FetchedUtc = DateTime.UtcNow.AddHours(-5);
            Check(Advice.SourceNote(d, DateTime.UtcNow).Contains("Нет связи — показаны данные 5 ч назад"), "устаревшие данные");
        }

        // Регрессия: при создании окна кадр уходил в Windows с размером без рамки,
        // и окно навсегда «ужималось» на 18×29 пикселей. Окно на долю секунды появляется на экране.
        static void WindowSizeOnStart()
        {
            foreach (float scale in new float[] { 1f, 0.654f, 0.8f, 2f })
            {
                string path = TempFile();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "city=Омск\ncountry=Россия\nlat=54.9885\nlon=73.3242\nscale=" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n");
                using (WeatherForm f = new WeatherForm(path))
                {
                    f.Show();
                    System.Windows.Forms.Application.DoEvents();
                    Size want = new Size((int)Math.Round(240 * scale), (int)Math.Round(440 * scale));
                    Eq(want, f.ClientSize, "размер окна при масштабе " + scale);
                    f.Hide();
                }
                Directory.Delete(Path.GetDirectoryName(path), true);
            }
        }

        static void FirstStart()
        {
            string path = TempFile();   // настроек нет
            using (WeatherForm f = new WeatherForm(path))
            {
                Check(f.Home == null, "город не выбран");
                Eq(0, f.Custom.Count, "предустановленных городов нет");
                using (Bitmap b = f.RenderPreview(null, false, 0))
                    Check(b.Width == 240, "карточка «Выберите город» рисуется");
            }
            string dir = Path.GetDirectoryName(path);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        static void Picker()
        {
            string path = TempFile();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "city=Омск\ncountry=Россия\nlat=54.9885\nlon=73.3242\n" +
                "fav=Лондон|Великобритания|51.5085|-0.12574\nfav=Владивосток|Россия|43.10562|131.87354\n", Encoding.UTF8);
            using (WeatherForm f = new WeatherForm(path))
            using (CityPicker p = new CityPicker(f))
            {
                int own, results, nothing;
                using (Bitmap b = p.RenderPreview("", null)) own = b.Height;
                using (Bitmap b = p.RenderPreview("Берлин", OpenMeteo.ParseSearch(Fixture("search.json")))) results = b.Height;
                using (Bitmap b = p.RenderPreview("Ыыыыы", new List<GeoResult>())) nothing = b.Height;
                Check(own > results, "свои города (3) — панель выше, чем с 2 результатами поиска (" + own + " > " + results + ")");
                Check(nothing < results, "«Ничего не найдено» — панель ниже (" + nothing + " < " + results + ")");
                using (Bitmap b = p.RenderPreview("", null))
                    Check(b.GetPixel(2, 2).A == 0 && b.GetPixel(b.Width / 2, b.Height / 2).A > 200, "вокруг прозрачно, панель непрозрачная");
            }
            Directory.Delete(Path.GetDirectoryName(path), true);
        }

        static void Tooltip()
        {
            using (TipWindow tip = new TipWindow())
            {
                int shortW, longW, longH, shortH;
                using (Bitmap b = tip.RenderPreview("", "Страна: Россия")) { shortW = b.Width; shortH = b.Height; }
                string longText = "Очень длинная строка подсказки, которая не должна растягивать окно на пол-экрана, а переносится";
                using (Bitmap b = tip.RenderPreview("Как ощущается", longText)) { longW = b.Width; longH = b.Height; }
                Check(longW <= 340 + 2 * 12 + 2 * Theme.PanelShadow + 2, "длинный текст переносится, ширина ограничена (" + longW + ")");
                Check(longH > shortH, "с заголовком и переносом подсказка выше");
                using (Bitmap b = tip.RenderPreview("", "Страна: Россия"))
                    Check(b.GetPixel(1, 1).A == 0 && b.GetPixel(b.Width / 2, b.Height / 2).A > 200, "вокруг прозрачно, панель непрозрачная");
                Check(shortW < longW, "короткая подсказка — узкая");
            }
        }

        static void English()
        {
            string path = TempFile();
            L.Use("en");
            try
            {
                Eq("Thunderstorm", WeatherCodes.Describe(95), "описание погоды");
                Eq("Rain from 21:00 (80%) — take an umbrella.", Advice.PrecipNote(WithHours(Sky.Cloudy, 16, 3, 61, 80), Now1830), "дождь впереди");
                Eq("Snow from 21:00 (60%).", Advice.PrecipNote(WithHours(Sky.Cloudy, 16, 3, 73, 60), Now1830), "снег впереди");
                Eq("Rain soon (70%) — take an umbrella.", Advice.PrecipNote(WithHours(Sky.Cloudy, 16, 0, 61, 70), Now1830), "дождь скоро");
                Eq("Take an umbrella — rain until 20:00.", Advice.PrecipNote(WithHours(Sky.Rain, 16, 0, 61, 90, 1, 61, 90), Now1830), "идёт дождь");
                Eq("25 min ago", Format.Age(TimeSpan.FromMinutes(25)), "давность");
                Eq("Sun, 25", L.DayLabel(new DateTime(2026, 10, 25)), "подпись дня");
                Check(Advice.TipText(OpenMeteo.ParseForecast(Moscow, Fixture("forecast.json"))).StartsWith("Feels like "), "подсказка");
                Eq("London", Cities.World[2].Name, "города «динамической погоды»");
                Eq(35, Cities.World.Length, "те же 35 городов");
                Check(OpenMeteo.SearchUrl("Paris").Contains("language=en"), "поиск городов по-английски");
                Eq("Mon, 26", OpenMeteo.ParseForecast(Moscow, Fixture("forecast.json")).Days[0].Label, "дни прогноза по-английски");

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "city=Омск\ncountry=Россия\nlat=54.9885\nlon=73.3242\n", Encoding.UTF8);
                using (WeatherForm f = new WeatherForm(path))
                {
                    f.NamesDownload = delegate(string url) { throw new System.Net.WebException("нет сети"); };
                    Eq("Weather", f.Text, "без выбора в меню — язык Windows (здесь английский)");
                    f.SetLanguage("ru");
                    Check(!L.English, "переключение на русский");
                    Eq("Погода", f.Text, "заголовок окна");
                }
                Eq("ru", Settings.Load(path).Lang, "выбор сохраняется в настройки");
                L.Use("en");
                using (WeatherForm f = new WeatherForm(path))
                    Check(!L.English, "при запуске берётся язык из настроек");
            }
            finally { L.Use("ru"); }
            Directory.Delete(Path.GetDirectoryName(path), true);
        }

        const string OmskSearchRu = "{\"results\":[{\"id\":1496153,\"name\":\"Омск\",\"latitude\":54.99244,\"longitude\":73.36859,\"country\":\"Россия\"}," +
            "{\"id\":1496150,\"name\":\"Омский\",\"latitude\":55.10402,\"longitude\":73.34691,\"country\":\"Россия\"}]}";
        const string OmskEn = "{\"id\":1496153,\"name\":\"Omsk\",\"latitude\":54.99244,\"longitude\":73.36859,\"country\":\"Russia\"}";

        // Сервер для перевода Омска: поиск по-русски и город по номеру по-английски
        static string OmskServer(string url)
        {
            if (url.Contains("/v1/search") && url.Contains("language=ru")) return OmskSearchRu;
            if (url.Contains("/v1/get?id=1496153&language=en")) return OmskEn;
            throw new System.Net.WebException("неожиданный запрос: " + url);
        }

        static void CityNamesTranslate()
        {
            Func<string, string> offline = delegate(string url) { throw new System.Net.WebException("нет сети"); };
            City omsk = new City("Омск", "Россия", 54.99244, 73.36859);
            Eq("ru", omsk.Lang, "язык названия угадывается по буквам");
            Eq("en", new City("Omsk", "Russia", 0, 0).Lang, "латиница — английский");
            Check(CityNames.Translate(omsk, "ru", offline) == omsk, "язык тот же — без запросов");

            City london = CityNames.Translate(new City("Лондон", "Британия", 51.50853, -0.12574), "en", offline);
            Eq("London, United Kingdom", london.MenuLabel, "город из встроенного списка — без сети");
            Eq(51.50853, london.Lat, "координаты прежние (кэш погоды не теряется)");

            City t = CityNames.Translate(omsk, "en", OmskServer);
            Eq("Omsk", t != null ? t.Name : null, "поиск по-русски, затем название по номеру GeoNames");
            Eq("Russia", t.Country, "страна по-английски");
            Eq("en", t.Lang, "язык названия");
            Eq(1496153, t.Id, "номер GeoNames запоминается");
            Eq(54.99244, t.Lat, "координаты прежние");

            City known = new City("Омск", "Россия", 54.99244, 73.36859, "ru", 1496153);
            Eq("Omsk", CityNames.Translate(known, "en", delegate(string url)
            {
                if (url.Contains("/v1/search")) throw new System.Net.WebException("поиск не нужен");
                return OmskServer(url);
            }).Name, "номер известен — сразу по номеру");

            Check(CityNames.Translate(omsk, "en", offline) == null, "нет сети — null, попробовать позже");
            City none = CityNames.Translate(new City("Деревня", "Россия", 10, 10), "en", delegate(string url) { return "{}"; });
            Check(none != null && none.Name == "Деревня" && none.Lang == "en", "перевода нет — название прежнее, больше не спрашиваем");

            Eq(1496153, OpenMeteo.ParseSearch(OmskSearchRu, "ru")[0].City.Id, "номер GeoNames из поиска");
            Check(OpenMeteo.CityUrl(1496153, "en").EndsWith("/v1/get?id=1496153&language=en"), "запрос города по номеру");

            // свой город и выбранный город заменяются, порядок и настройки сохраняются
            string path = TempFile();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "city=Омск\ncountry=Россия\nlat=54.99244\nlon=73.36859\n" +
                "fav=Омск|Россия|54.99244|73.36859\nfav=Лондон|Британия|51.50853|-0.12574\n", Encoding.UTF8);
            using (WeatherForm f = new WeatherForm(path))
            {
                f.ApplyTranslations(new List<City[]> { new City[] { f.Custom.Items[0], t }, new City[] { f.Custom.Items[1], london } });
                Eq("Omsk", f.Home.Name, "выбранный город переведён");
                Eq("Omsk", f.Custom.Items[0].Name, "свой город переведён, порядок прежний");
                Eq("London", f.Custom.Items[1].Name, "второй свой город");
            }
            Settings s = Settings.Load(path);
            Eq("Omsk", s.Home.Name, "сохранено в настройки");
            Eq("en", s.Home.Lang, "язык названия выбранного города сохраняется");
            Eq(1496153, s.Custom.Items[0].Id, "номер GeoNames сохраняется");
            Eq("en", s.Custom.Items[1].Lang, "язык названия своего города сохраняется");
            Eq(2, s.Custom.Count, "городов не прибавилось");
            Directory.Delete(Path.GetDirectoryName(path), true);
        }

        static void Transparency()
        {
            string path = TempFile();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "city=Омск\ncountry=Россия\nlat=54.9885\nlon=73.3242\ntransparency=50\n", Encoding.UTF8);
            using (WeatherForm f = new WeatherForm(path))
            {
                Eq(50, f.BgTransparency, "загружается из настроек");
                WeatherData d = OpenMeteo.ParseForecast(Omsk, Fixture("forecast.json"));
                using (Bitmap b = f.RenderPreview(d, false, 0))
                {
                    int bgA = b.GetPixel(14, 240).A;   // пустой фон у нижнего левого угла
                    Check(bgA > 110 && bgA < 145, "при 50% фон полупрозрачный (альфа " + bgA + ")");
                    int textA = 0;   // самый непрозрачный пиксель в цифрах температуры
                    for (int x = 60; x < 180; x++) for (int y = 145; y < 190; y++) textA = Math.Max(textA, b.GetPixel(x, y).A);
                    Eq(255, textA, "текст температуры непрозрачный");
                }
                f.BgTransparency = 200;
                Eq(WeatherForm.MaxTransparency, f.BgTransparency, "не больше максимума");
                using (Bitmap b = f.RenderPreview(d, false, 0))
                    Eq(1, (int)b.GetPixel(14, 240).A, "при 100% фон невидим, но ловит клики");
                f.BgTransparency = -5;
                Eq(0, f.BgTransparency, "не меньше нуля");
                using (Bitmap b = f.RenderPreview(d, false, 0))
                    Eq(255, (int)b.GetPixel(14, 240).A, "при 0% фон непрозрачный");
                f.BgTransparency = 30;
                f.SaveSettings();
                using (OpacityPanel p = new OpacityPanel(f))
                using (Bitmap b = p.RenderPreview())
                    Check(b.GetPixel(b.Width / 2, b.Height / 2).A > 200, "панель с ползунком рисуется");
            }
            Eq(30, Settings.Load(path).Transparency.GetValueOrDefault(-1), "сохраняется в настройки");
            Directory.Delete(Path.GetDirectoryName(path), true);
        }

        static void SettingsBroken()
        {
            Settings none = Settings.Load(TempFile());
            Check(none.Home == null && !none.Position.HasValue && !none.Dynamic, "нет файла — значения по умолчанию");

            string path = TempFile();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "мусор\nlat=abc\nx=12\nfav=a|b|c\nscale=много\ncity=Неизвестный\n", Encoding.UTF8);
            Settings l = Settings.Load(path);
            Check(l.Home == null, "город без координат и не из списка — не задан");
            Check(!l.Position.HasValue, "только x без y — положение не задано");
            Check(!l.Scale.HasValue, "неверный масштаб пропущен");
            Eq(0, l.Custom.Count, "неполная запись своего города пропущена");
            Directory.Delete(Path.GetDirectoryName(path), true);
        }
    }
}
