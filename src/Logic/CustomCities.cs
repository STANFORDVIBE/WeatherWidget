using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WeatherWidget
{
    // «Свои» города — найденные через поиск. Показываются в меню под стандартными.
    // Хранится не больше Max, самые старые вытесняются; стандартные города сюда не попадают.
    sealed class CustomCities
    {
        public const int Max = 10;
        readonly List<City> items = new List<City>();

        public ReadOnlyCollection<City> Items { get { return items.AsReadOnly(); } }
        public int Count { get { return items.Count; } }

        // Запомнить город (повторный выбор переносит его в конец списка)
        public void Remember(City c)
        {
            if (c == null || Cities.IsStandard(c)) return;
            items.RemoveAll(x => x.Same(c));
            items.Add(c);
            while (items.Count > Max) items.RemoveAt(0);
        }

        // Добавить, только если такого ещё нет (порядок не меняется)
        public void AddIfMissing(City c)
        {
            if (c == null || Cities.IsStandard(c) || Contains(c)) return;
            Remember(c);
        }

        public bool Contains(City c) { return items.Exists(x => x.Same(c)); }

        public void Remove(City c) { items.RemoveAll(x => x.Same(c)); }
    }
}
