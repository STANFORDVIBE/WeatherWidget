using System;
using System.Collections.Generic;

namespace WeatherWidget
{
    // Тип неба — от него зависят анимация и цвет фона
    enum Sky { Sunny, PartlyCloudy, Cloudy, Rain, Snow }

    // Прогноз на один день
    sealed class DayForecast
    {
        public string Label;    // «Пн, 26»
        public DateTime Date;   // местная дата города; MinValue — неизвестна (кэш старых версий)
        public Sky Sky;
        public double Max, Min;
    }

    // Прогноз на один час
    sealed class HourForecast
    {
        public DateTime Utc;        // начало часа, UTC
        public double Temp;         // °C
        public int Code;            // код погоды WMO
        public Sky Sky;
        public bool IsDay;
        public int Precip = -1;     // вероятность осадков, %; -1 — источник её не сообщает (MET Norway)
    }

    // Текущая погода в городе и прогноз на несколько дней
    sealed class WeatherData
    {
        public City City;
        public double Temp, Feels, Wind;    // °C, °C, м/с
        public int Humidity, Code;          // %, код погоды WMO
        public bool IsDay;
        public Sky Sky;
        public int UtcOffset;               // смещение часового пояса города, секунды
        public List<DayForecast> Days = new List<DayForecast>();
        public List<HourForecast> Hours = new List<HourForecast>();   // по времени, от текущего часа

        public string Source = "";          // откуда данные: Open-Meteo или MET Norway
        public DateTime FetchedUtc;         // когда получены
        public bool Stale;                  // свежих данных нет — показан кэш

        // Местное время города
        public DateTime Local(DateTime utc) { return utc.AddSeconds(UtcOffset); }

        // Дни прогноза начиная с завтрашнего (по времени города). Данные из кэша могут быть
        // многодневной давности — прошедшие дни отбрасываются.
        public List<DayForecast> UpcomingDays(DateTime nowUtc, int max)
        {
            DateTime today = Local(nowUtc).Date;
            List<DayForecast> list = new List<DayForecast>();
            foreach (DayForecast f in Days)
                if (list.Count < max && (f.Date == DateTime.MinValue || f.Date > today)) list.Add(f);
            return list;
        }

        // Часы прогноза начиная с текущего (час, внутри которого nowUtc, тоже входит)
        public List<HourForecast> HoursFrom(DateTime nowUtc)
        {
            List<HourForecast> list = new List<HourForecast>();
            foreach (HourForecast h in Hours)
                if (h.Utc > nowUtc.AddHours(-1)) list.Add(h);
            return list;
        }
    }
}
