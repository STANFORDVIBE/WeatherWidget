using System.IO;
using System.Text;

namespace WeatherWidget
{
    static class SafeFile
    {
        // Запись через временный файл и замену: если программа упадёт посреди записи,
        // останется старый файл целиком, а не «половинка»
        public static void WriteAllLines(string path, string[] lines)
        {
            string tmp = path + ".tmp";
            File.WriteAllLines(tmp, lines, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }
    }
}
