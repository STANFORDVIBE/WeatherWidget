using System;
using System.Drawing;

namespace WeatherWidget
{
    // Масштаб экрана в Windows (1 — 100 %, 1.5 — 150 %). Виджет помечен как DPI-aware (assets\app.manifest),
    // поэтому окна рисуются в физических пикселях: базовый макет умножается на этот масштаб.
    // У процесса без манифеста (тесты, tools\) Windows всегда отвечает 96 точек — то есть 1.
    static class Dpi
    {
        public static float AtPoint(Point p)
        {
            try
            {
                IntPtr mon = NativeMethods.MonitorFromPoint(new NativeMethods.POINT { X = p.X, Y = p.Y }, NativeMethods.MONITOR_DEFAULTTONEAREST);
                uint x, y;
                if (NativeMethods.GetDpiForMonitor(mon, 0, out x, out y) == 0 && x > 0) return x / 96f;
            }
            catch (DllNotFoundException) { }          // Windows 7
            catch (EntryPointNotFoundException) { }   // Windows 8
            return 1f;
        }

        // Размер в физических пикселях
        public static int Px(float v, float dpi) { return (int)Math.Round(v * dpi); }

        // Точка окна -> базовые координаты макета
        public static Point ToBase(Point p, float dpi) { return new Point((int)(p.X / dpi), (int)(p.Y / dpi)); }
    }
}
