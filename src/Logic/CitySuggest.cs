using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WeatherWidget
{
    // Подсказки при опечатках в поиске города. Open-Meteo ищет только по точному началу названия:
    // «Sydny», «Londn», «Barselona» у него ничего не находят, а «Sidney» — лишь городки в США.
    // Поэтому запрос сравнивается со встроенным списком крупных городов (KnownCities)
    // по расстоянию Дамерау–Левенштейна: сколько букв надо заменить, вставить, удалить
    // или переставить местами, чтобы получилось название.
    static class CitySuggest
    {
        // Сколько опечаток прощаем: в коротком запросе — меньше, иначе подходит что угодно
        public static int MaxTypos(int length)
        {
            if (length < 4) return 0;
            if (length <= 5) return 1;
            if (length <= 9) return 2;
            return 3;
        }

        public static List<GeoResult> Find(string query, string lang, int max)
        {
            return Find(query, lang, max, KnownCities.All);
        }

        // Похожие города: сначала с меньшим числом опечаток, среди равных — крупнее.
        // Названия — на языке lang, но искать можно на любом из двух языков.
        public static List<GeoResult> Find(string query, string lang, int max, IList<KnownCity> cities)
        {
            List<GeoResult> list = new List<GeoResult>();
            string q = Normalize(query);
            int limit = MaxTypos(q.Length);
            if (limit == 0) return list;

            List<KeyValuePair<int, KnownCity>> found = new List<KeyValuePair<int, KnownCity>>();
            foreach (KnownCity c in cities)
            {
                int d = Math.Min(Typos(q, c.KeyEn, limit), Typos(q, c.KeyRu, limit));
                if (d <= limit) found.Add(new KeyValuePair<int, KnownCity>(d, c));
            }
            found.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : b.Value.Population.CompareTo(a.Value.Population));

            for (int i = 0; i < found.Count && list.Count < max; i++)
            {
                KnownCity c = found[i].Value;
                GeoResult g = new GeoResult();
                g.City = new City(c.Name(lang), c.Country(lang), c.Lat, c.Lon, lang, c.Id);
                g.Label = c.Country(lang) != "" ? g.City.Name + ", " + c.Country(lang) : g.City.Name;
                g.Population = c.Population;
                g.Typos = found[i].Key;
                list.Add(g);
            }
            return list;
        }

        // Опечаток между запросом и названием. Запрос может быть ещё не дописан —
        // тогда сравнивается с началом названия той же длины («Sidn» ≈ «Sydn|ey»).
        static int Typos(string q, string name, int limit)
        {
            if (name.Length + limit < q.Length) return int.MaxValue;   // название заметно короче — не оно
            int full = Distance(q, name);
            return name.Length > q.Length ? Math.Min(full, Distance(q, name.Substring(0, q.Length))) : full;
        }

        // Расстояние Дамерау–Левенштейна (вариант «оптимальное выравнивание строк»):
        // замена, вставка, удаление буквы и перестановка двух соседних — по одной правке
        public static int Distance(string a, string b)
        {
            int[,] d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    int v = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                        v = Math.Min(v, d[i - 2, j - 2] + 1);
                    d[i, j] = v;
                }
            return d[a.Length, b.Length];
        }

        // Для сравнения: строчные буквы, ё → е, без диакритики (å → a), дефисы и точки — пробелы
        public static string Normalize(string s)
        {
            string decomposed = (s ?? "").ToLowerInvariant().Replace('ё', 'е').Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            bool space = true;   // пробелы в начале и подряд не нужны
            foreach (char ch in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                {
                    if (ch == '̆' && sb.Length > 0 && sb[sb.Length - 1] == 'и') sb[sb.Length - 1] = 'й';   // й — отдельная буква
                    continue;
                }
                bool sep = char.IsWhiteSpace(ch) || ch == '-' || ch == '.' || ch == '\'' || ch == '’';
                if (sep) { if (!space) sb.Append(' '); space = true; }
                else { sb.Append(ch); space = false; }
            }
            return sb.ToString().TrimEnd();
        }

        // Итоговый список для панели: результаты Open-Meteo и подсказки без повторов.
        // Подсказка ставится первой, если город крупнее всего, что нашёл сервер (набрали «Sidney» —
        // сначала Сидней в Австралии, а не городок в Огайо). Сервер ничего не нашёл — только подсказки.
        // server == null — нет связи: остаются подсказки (null, если и их нет).
        public static List<GeoResult> Merge(List<GeoResult> server, List<GeoResult> guesses, int max)
        {
            if (server == null) return guesses.Count > 0 ? guesses : null;
            List<GeoResult> fresh = new List<GeoResult>();
            long top = 0;
            foreach (GeoResult s in server) top = Math.Max(top, s.Population);
            foreach (GeoResult g in guesses)
                if (!server.Exists(s => s.City.SamePlace(g.City))) fresh.Add(g);

            List<GeoResult> list = new List<GeoResult>();
            if (server.Count == 0) list.AddRange(fresh);
            else
            {
                // наверх — только самые похожие (меньше всего опечаток): иначе «Sidney» поднимал бы и «Видное»
                int best = int.MaxValue;
                foreach (GeoResult g in fresh) best = Math.Min(best, g.Typos);
                foreach (GeoResult g in fresh)
                    if (g.Typos == best && g.Population > top && list.Count < 2) list.Add(g);
                list.AddRange(server);
            }
            if (list.Count > max) list.RemoveRange(max, list.Count - max);
            return list;
        }
    }
}
