namespace WeatherWidget
{
    // Встроенный список городов. Предустановленных городов для выбора нет:
    // пользователь находит свой город поиском, и он запоминается (CustomCities).
    static class Cities
    {
        // Города для режима «Динамическая погода» — с названиями на языке интерфейса
        public static City[] World { get { return L.English ? WorldEn : WorldRu; } }

        static readonly City[] WorldRu, WorldEn;

        // название и страна по-русски, по-английски, координаты
        static readonly object[][] Data =
        {
            new object[] { "Лос-Анджелес", "США", "Los Angeles", "USA", 34.0522, -118.2437 },
            new object[] { "Нью-Йорк", "США", "New York", "USA", 40.7128, -74.0060 },
            new object[] { "Лондон", "Великобритания", "London", "United Kingdom", 51.5074, -0.1278 },
            new object[] { "Берлин", "Германия", "Berlin", "Germany", 52.5200, 13.4050 },
            new object[] { "Париж", "Франция", "Paris", "France", 48.8566, 2.3522 },
            new object[] { "Рим", "Италия", "Rome", "Italy", 41.9028, 12.4964 },
            new object[] { "Мадрид", "Испания", "Madrid", "Spain", 40.4168, -3.7038 },
            new object[] { "Амстердам", "Нидерланды", "Amsterdam", "Netherlands", 52.3676, 4.9041 },
            new object[] { "Прага", "Чехия", "Prague", "Czechia", 50.0755, 14.4378 },
            new object[] { "Стамбул", "Турция", "Istanbul", "Türkiye", 41.0082, 28.9784 },
            new object[] { "Дубай", "ОАЭ", "Dubai", "UAE", 25.2048, 55.2708 },
            new object[] { "Каир", "Египет", "Cairo", "Egypt", 30.0444, 31.2357 },
            new object[] { "Токио", "Япония", "Tokyo", "Japan", 35.6762, 139.6503 },
            new object[] { "Сеул", "Южная Корея", "Seoul", "South Korea", 37.5665, 126.9780 },
            new object[] { "Пекин", "Китай", "Beijing", "China", 39.9042, 116.4074 },
            new object[] { "Бангкок", "Таиланд", "Bangkok", "Thailand", 13.7563, 100.5018 },
            new object[] { "Сингапур", "Сингапур", "Singapore", "Singapore", 1.3521, 103.8198 },
            new object[] { "Сидней", "Австралия", "Sydney", "Australia", -33.8688, 151.2093 },
            new object[] { "Торонто", "Канада", "Toronto", "Canada", 43.6532, -79.3832 },
            new object[] { "Рио-де-Жанейро", "Бразилия", "Rio de Janeiro", "Brazil", -22.9068, -43.1729 },
            new object[] { "Вена", "Австрия", "Vienna", "Austria", 48.2082, 16.3738 },
            new object[] { "Барселона", "Испания", "Barcelona", "Spain", 41.3851, 2.1734 },
            new object[] { "Лиссабон", "Португалия", "Lisbon", "Portugal", 38.7223, -9.1393 },
            new object[] { "Афины", "Греция", "Athens", "Greece", 37.9838, 23.7275 },
            new object[] { "Стокгольм", "Швеция", "Stockholm", "Sweden", 59.3293, 18.0686 },
            new object[] { "Варшава", "Польша", "Warsaw", "Poland", 52.2297, 21.0122 },
            new object[] { "Минск", "Беларусь", "Minsk", "Belarus", 53.9006, 27.5590 },
            new object[] { "Астана", "Казахстан", "Astana", "Kazakhstan", 51.1694, 71.4491 },
            new object[] { "Ереван", "Армения", "Yerevan", "Armenia", 40.1792, 44.4991 },
            new object[] { "Тбилиси", "Грузия", "Tbilisi", "Georgia", 41.7151, 44.8271 },
            new object[] { "Чикаго", "США", "Chicago", "USA", 41.8781, -87.6298 },
            new object[] { "Майами", "США", "Miami", "USA", 25.7617, -80.1918 },
            new object[] { "Мехико", "Мексика", "Mexico City", "Mexico", 19.4326, -99.1332 },
            new object[] { "Мумбаи", "Индия", "Mumbai", "India", 19.0760, 72.8777 },
            new object[] { "Ханой", "Вьетнам", "Hanoi", "Vietnam", 21.0278, 105.8342 }
        };

        static Cities()
        {
            WorldRu = new City[Data.Length];
            WorldEn = new City[Data.Length];
            for (int i = 0; i < Data.Length; i++)
            {
                object[] r = Data[i];
                WorldRu[i] = new City((string)r[0], (string)r[1], (double)r[4], (double)r[5]);
                WorldEn[i] = new City((string)r[2], (string)r[3], (double)r[4], (double)r[5]);
            }
        }
    }
}
