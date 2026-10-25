using System;
using System.Collections.Generic;

namespace WeatherWidget
{
    // Порядок городов для «динамической погоды»: каждый круг — новый случайный порядок,
    // все города по одному разу, и два города одной страны не идут подряд (в том числе на стыке кругов).
    sealed class CityRotation
    {
        readonly City[] pool;
        readonly Random rnd;
        List<City> order = new List<City>();
        int index = -1;
        City lastShown;   // с ним сверяется начало нового круга

        public CityRotation(City[] pool, Random rnd)
        {
            this.pool = pool;
            this.rnd = rnd;
        }

        // Начать новый круг со следующего вызова Next()
        public void Restart()
        {
            index = order.Count;
        }

        public City Next()
        {
            index++;
            if (index >= order.Count)
            {
                order = Shuffle(lastShown);
                index = 0;
            }
            lastShown = order[index];
            return lastShown;
        }

        List<City> Shuffle(City previous)
        {
            // в редких случаях развести соседей не удаётся — тогда перемешиваем заново
            List<City> list = null;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                list = ShuffleOnce(previous);
                if (!HasSameCountryNeighbours(list, previous)) break;
            }
            return list;
        }

        List<City> ShuffleOnce(City previous)
        {
            List<City> list = new List<City>(pool);
            for (int i = list.Count - 1; i > 0; i--)   // перемешивание Фишера–Йетса
            {
                int j = rnd.Next(i + 1);
                City t = list[i]; list[i] = list[j]; list[j] = t;
            }
            // развести соседей из одной страны
            for (int i = 0; i < list.Count; i++)
            {
                City prev = i > 0 ? list[i - 1] : previous;
                if (prev == null || list[i].Country != prev.Country) continue;
                for (int j = i + 1; j < list.Count; j++)
                {
                    if (list[j].Country != prev.Country)
                    {
                        City t = list[i]; list[i] = list[j]; list[j] = t;
                        break;
                    }
                }
            }
            return list;
        }

        public static bool HasSameCountryNeighbours(IList<City> list, City previous)
        {
            for (int i = 0; i < list.Count; i++)
            {
                City prev = i > 0 ? list[i - 1] : previous;
                if (prev != null && prev.Country == list[i].Country) return true;
            }
            return false;
        }
    }
}
