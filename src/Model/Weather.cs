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
        public Sky Sky;
        public double Max, Min;
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

        public string Source = "";          // откуда данные: Open-Meteo или MET Norway
        public DateTime FetchedUtc;         // когда получены
        public bool Stale;                  // свежих данных нет — показан кэш
    }
}
