using System.Windows.Forms;
using Microsoft.Win32;

namespace WeatherWidget
{
    // Запуск вместе с Windows: запись в HKCU\...\Run (права администратора не нужны)
    static class AutoStart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "WeatherWidget";
        // с этим ключом Windows запускает виджет при входе — сеть в эти секунды ещё не поднялась
        public const string Arg = "--autostart";

        public static bool IsEnabled()
        {
            return Current() != null;
        }

        public static void Set(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue(ValueName, Command(Application.ExecutablePath));
                    else k.DeleteValue(ValueName, false);
                }
            }
            catch { }
        }

        // Автозапуск включён, но записан по-старому (без ключа) или для другого места exe — переписать
        public static void Sync()
        {
            string cur = Current();
            if (cur != null && cur != Command(Application.ExecutablePath)) Set(true);
        }

        // Команда запуска: "C:\...\WeatherWidget.exe" --autostart
        public static string Command(string exe)
        {
            return "\"" + exe + "\" " + Arg;
        }

        static string Current()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k == null ? null : k.GetValue(ValueName) as string;
            }
            catch { return null; }
        }
    }
}
