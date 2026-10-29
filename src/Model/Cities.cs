using System;

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
            new object[] { "Ханой", "Вьетнам", "Hanoi", "Vietnam", 21.0278, 105.8342 },

            // ещё 65 крупных городов (координаты и названия — GeoNames, assets\cities.txt)
            new object[] { "Москва", "Россия", "Moscow", "Russia", 55.7520, 37.6178 },
            new object[] { "Санкт-Петербург", "Россия", "Saint Petersburg", "Russia", 59.9386, 30.3141 },
            new object[] { "Новосибирск", "Россия", "Novosibirsk", "Russia", 55.0226, 82.9317 },
            new object[] { "Екатеринбург", "Россия", "Yekaterinburg", "Russia", 56.8573, 60.6153 },
            new object[] { "Казань", "Россия", "Kazan", "Russia", 55.7887, 49.1221 },
            new object[] { "Владивосток", "Россия", "Vladivostok", "Russia", 43.1056, 131.8735 },
            new object[] { "Киев", "Украина", "Kyiv", "Ukraine", 50.4547, 30.5238 },
            new object[] { "Ташкент", "Узбекистан", "Tashkent", "Uzbekistan", 41.2647, 69.2163 },
            new object[] { "Баку", "Азербайджан", "Baku", "Azerbaijan", 40.3777, 49.8920 },
            new object[] { "Алматы", "Казахстан", "Almaty", "Kazakhstan", 43.2525, 76.9115 },
            new object[] { "Бишкек", "Кыргызстан", "Bishkek", "Kyrgyzstan", 42.8700, 74.5900 },
            new object[] { "Дублин", "Ирландия", "Dublin", "Ireland", 53.3331, -6.2489 },
            new object[] { "Брюссель", "Бельгия", "Brussels", "Belgium", 50.8505, 4.3488 },
            new object[] { "Цюрих", "Швейцария", "Zurich", "Switzerland", 47.3667, 8.5500 },
            new object[] { "Мюнхен", "Германия", "Munich", "Germany", 48.1374, 11.5755 },
            new object[] { "Милан", "Италия", "Milan", "Italy", 45.4643, 9.1895 },
            new object[] { "Будапешт", "Венгрия", "Budapest", "Hungary", 47.4984, 19.0404 },
            new object[] { "Копенгаген", "Дания", "Copenhagen", "Denmark", 55.6759, 12.5655 },
            new object[] { "Осло", "Норвегия", "Oslo", "Norway", 59.9127, 10.7461 },
            new object[] { "Хельсинки", "Финляндия", "Helsinki", "Finland", 60.1695, 24.9354 },
            new object[] { "Рига", "Латвия", "Riga", "Latvia", 56.9460, 24.1059 },
            new object[] { "Бухарест", "Румыния", "Bucharest", "Romania", 44.4323, 26.1063 },
            new object[] { "Белград", "Сербия", "Belgrade", "Serbia", 44.8040, 20.4651 },
            new object[] { "Тель-Авив", "Израиль", "Tel Aviv", "Israel", 32.0809, 34.7806 },
            new object[] { "Эр-Рияд", "Саудовская Аравия", "Riyadh", "Saudi Arabia", 24.6877, 46.7219 },
            new object[] { "Тегеран", "Иран", "Tehran", "Iran", 35.6944, 51.4215 },
            new object[] { "Доха", "Катар", "Doha", "Qatar", 25.2855, 51.5310 },
            new object[] { "Лагос", "Нигерия", "Lagos", "Nigeria", 6.4541, 3.3947 },
            new object[] { "Найроби", "Кения", "Nairobi", "Kenya", -1.2833, 36.8167 },
            new object[] { "Кейптаун", "ЮАР", "Cape Town", "South Africa", -33.9258, 18.4232 },
            new object[] { "Йоханнесбург", "ЮАР", "Johannesburg", "South Africa", -26.2023, 28.0436 },
            new object[] { "Касабланка", "Марокко", "Casablanca", "Morocco", 33.5883, -7.6114 },
            new object[] { "Аддис-Абеба", "Эфиопия", "Addis Ababa", "Ethiopia", 9.0250, 38.7469 },
            new object[] { "Киншаса", "ДР Конго", "Kinshasa", "DR Congo", -4.3276, 15.3136 },
            new object[] { "Дели", "Индия", "Delhi", "India", 28.6519, 77.2315 },
            new object[] { "Бангалор", "Индия", "Bengaluru", "India", 12.9719, 77.5937 },
            new object[] { "Колката", "Индия", "Kolkata", "India", 22.5626, 88.3630 },
            new object[] { "Карачи", "Пакистан", "Karachi", "Pakistan", 24.8608, 67.0104 },
            new object[] { "Дакка", "Бангладеш", "Dhaka", "Bangladesh", 23.7104, 90.4074 },
            new object[] { "Катманду", "Непал", "Kathmandu", "Nepal", 27.7017, 85.3206 },
            new object[] { "Коломбо", "Шри-Ланка", "Colombo", "Sri Lanka", 6.9355, 79.8487 },
            new object[] { "Шанхай", "Китай", "Shanghai", "China", 31.2222, 121.4581 },
            new object[] { "Гонконг", "Китай", "Hong Kong", "China", 22.2783, 114.1747 },
            new object[] { "Тайбэй", "Тайвань", "Taipei", "Taiwan", 25.0531, 121.5264 },
            new object[] { "Осака", "Япония", "Osaka", "Japan", 34.6938, 135.5011 },
            new object[] { "Манила", "Филиппины", "Manila", "Philippines", 14.6042, 120.9822 },
            new object[] { "Джакарта", "Индонезия", "Jakarta", "Indonesia", -6.2146, 106.8451 },
            new object[] { "Куала-Лумпур", "Малайзия", "Kuala Lumpur", "Malaysia", 3.1412, 101.6865 },
            new object[] { "Хошимин", "Вьетнам", "Ho Chi Minh City", "Vietnam", 10.8230, 106.6296 },
            new object[] { "Улан-Батор", "Монголия", "Ulaanbaatar", "Mongolia", 47.9077, 106.8832 },
            new object[] { "Мельбурн", "Австралия", "Melbourne", "Australia", -37.8140, 144.9633 },
            new object[] { "Окленд", "Новая Зеландия", "Auckland", "New Zealand", -36.8485, 174.7635 },
            new object[] { "Перт", "Австралия", "Perth", "Australia", -31.9522, 115.8614 },
            new object[] { "Сан-Франциско", "США", "San Francisco", "USA", 37.7749, -122.4194 },
            new object[] { "Сиэтл", "США", "Seattle", "USA", 47.6062, -122.3321 },
            new object[] { "Ванкувер", "Канада", "Vancouver", "Canada", 49.2497, -123.1193 },
            new object[] { "Монреаль", "Канада", "Montreal", "Canada", 45.5088, -73.5878 },
            new object[] { "Вашингтон", "США", "Washington", "USA", 38.8951, -77.0364 },
            new object[] { "Гонолулу", "США", "Honolulu", "USA", 21.3069, -157.8583 },
            new object[] { "Буэнос-Айрес", "Аргентина", "Buenos Aires", "Argentina", -34.6131, -58.3772 },
            new object[] { "Сан-Паулу", "Бразилия", "São Paulo", "Brazil", -23.5475, -46.6361 },
            new object[] { "Лима", "Перу", "Lima", "Peru", -12.0432, -77.0282 },
            new object[] { "Богота", "Колумбия", "Bogotá", "Colombia", 4.6097, -74.0817 },
            new object[] { "Сантьяго", "Чили", "Santiago", "Chile", -33.4569, -70.6483 },
            new object[] { "Гавана", "Куба", "Havana", "Cuba", 23.1330, -82.3830 }
        };

        // Встроенный город в том же месте — с названием на языке lang; null — такого нет
        public static City Builtin(City c, string lang)
        {
            foreach (object[] r in Data)
                if (Math.Abs((double)r[4] - c.Lat) < 0.05 && Math.Abs((double)r[5] - c.Lon) < 0.05)
                    return lang == "en"
                        ? new City((string)r[2], (string)r[3], c.Lat, c.Lon, "en", c.Id)
                        : new City((string)r[0], (string)r[1], c.Lat, c.Lon, "ru", c.Id);
            return null;
        }

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
