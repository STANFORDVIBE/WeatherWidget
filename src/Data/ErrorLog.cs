using System;
using System.IO;
using System.Text;

namespace WeatherWidget
{
    // Журнал ошибок %APPDATA%\WeatherWidget\error.log: виджет не исчезает молча, а причину видно.
    // Туда же одной строкой пишется, какой источник погоды не ответил и почему (WeatherService.Failed).
    // Одна и та же ошибка подряд (например, в каждом кадре анимации) пишется один раз.
    // Файл больше 256 КБ начинается заново, прошлый остаётся рядом как error.old.log.
    static class ErrorLog
    {
        public const long MaxBytes = 256 * 1024;
        static readonly object sync = new object();
        static string last;

        public static string DefaultPath
        {
            get { return Path.Combine(Path.GetDirectoryName(Settings.DefaultPath), "error.log"); }
        }

        // Не бросает исключений: журнал не должен сам ронять виджет
        public static void Write(string path, Exception e, DateTime now)
        {
            if (e == null) return;
            Append(path, e.ToString(), now.ToString("yyyy-MM-dd HH:mm:ss") + "  WeatherWidget " + Program.Version +
                "\r\n" + e + "\r\n\r\n");
        }

        // Короткая запись в одну строку — например, какой источник погоды не ответил и почему
        public static void Note(string path, string message, DateTime now)
        {
            if (string.IsNullOrEmpty(message)) return;
            Append(path, message, now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + "\r\n");
        }

        // text — для сравнения с прошлой записью, entry — что дописать в файл
        static void Append(string path, string text, string entry)
        {
            try
            {
                lock (sync)
                {
                    if (text == last) return;
                    last = text;
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    FileInfo fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > MaxBytes)
                    {
                        string old = Path.ChangeExtension(path, ".old.log");
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(path, old);
                    }
                    File.AppendAllText(path, entry, Encoding.UTF8);
                }
            }
            catch { }
        }

        // Для тестов: следующая ошибка запишется, даже если совпадает с прошлой
        internal static void Reset() { lock (sync) last = null; }
    }
}
