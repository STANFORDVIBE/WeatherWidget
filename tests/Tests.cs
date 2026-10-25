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

        static int Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            fixtures = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\tests\fixtures"));

            Run("Коды погоды", WeatherCodesMap);
            Run("Формат температуры", DegFormat);
            Run("Ветер и влажность простыми словами", Feelings);
            Run("Совет по одежде", ClothesAdvice);
            Run("Разбор прогноза Open-Meteo", ParseForecast);
            Run("Прогноз без блока daily", ParseForecastWithoutDaily);
            Run("Разбор поиска города", ParseSearch);
            Run("Списки городов", CityLists);
            Run("Свои города", CustomCitiesList);
            Run("Порядок «динамической погоды»", Rotation);
            Run("Настройки: сохранение и загрузка", SettingsRoundTrip);
            Run("Настройки: старый формат файла", SettingsLegacy);
            Run("Настройки: повреждённый и отсутствующий файл", SettingsBroken);

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
            City moscow = Cities.Favorites[0];
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
            }
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
            Eq(6, Cities.Favorites.Length, "стандартных городов");
            Eq(35, Cities.World.Length, "городов «динамической погоды»");
            HashSet<string> names = new HashSet<string>();
            foreach (City c in Cities.World)
            {
                Check(names.Add(c.Name), "без повторов: " + c.Name);
                Check(c.Country != "", "страна указана: " + c.Name);
                Check(Math.Abs(c.Lat) <= 90 && Math.Abs(c.Lon) <= 180, "координаты в допустимых пределах: " + c.Name);
            }
            Eq("Лондон, Великобритания", Cities.World[2].MenuLabel, "подпись зарубежного города");
            Eq("Омск", Cities.Favorites[2].MenuLabel, "подпись российского города");
            Check(Cities.IsStandard(new City("Омск", "Россия", 54.99, 73.32)), "Омск — стандартный (координаты чуть другие)");
        }

        static void CustomCitiesList()
        {
            CustomCities list = new CustomCities();
            list.Remember(Cities.Favorites[2]);
            Eq(0, list.Count, "стандартный город не добавляется");

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
            Check(ReferenceEquals(Cities.Favorites[2], l.Home), "Омск — стандартный экземпляр");
            Eq(new Point(10, 20), l.Position.GetValueOrDefault(), "положение");
            Check(!l.Scale.HasValue, "масштаб не задан");
            Eq(0, l.Custom.Count, "своих городов нет");
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
