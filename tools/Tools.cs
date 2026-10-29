using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace WeatherWidget.Tools
{
    // Генерация оформления из настоящего кода отрисовки виджета:
    //   WeatherWidget.Tools.exe icon <файл.ico>        — иконка программы
    //   WeatherWidget.Tools.exe screenshots <папка>    — картинки для README
    static class Program
    {
        // города для картинок
        static readonly City Moscow = new City("Москва", "Россия", 55.7558, 37.6173);
        static readonly City Spb = new City("Санкт-Петербург", "Россия", 59.9343, 30.3351);
        static readonly City Omsk = new City("Омск", "Россия", 54.9885, 73.3242);
        static readonly City Khanty = new City("Ханты-Мансийск", "Россия", 61.0042, 69.0019);

        [STAThread]
        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (args.Length == 2 && args[0] == "icon") { MakeIcon(args[1]); return 0; }
            if (args.Length == 2 && args[0] == "screenshots") { MakeScreenshots(args[1], "ru"); return 0; }
            if (args.Length == 3 && args[0] == "screenshots") { MakeScreenshots(args[1], args[2]); return 0; }
            Console.WriteLine("Использование: WeatherWidget.Tools.exe icon <файл.ico> | screenshots <папка> [ru|en]");
            return 1;
        }

        // ---------- Иконка ----------

        // ICO из нескольких размеров: маленькие — несжатые 32-битные (их понимают все, включая WinForms),
        // крупные — PNG (так Windows хранит большие иконки, иначе файл был бы огромным)
        static void MakeIcon(string path)
        {
            int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
            List<byte[]> pngs = new List<byte[]>();
            foreach (int s in sizes)
                using (Bitmap b = DrawIcon(s))
                    pngs.Add(s <= 64 ? ToIconDib(b) : ToPng(b));

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using (BinaryWriter w = new BinaryWriter(File.Create(path)))
            {
                w.Write((short)0);              // reserved
                w.Write((short)1);              // тип: иконка
                w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));   // 0 означает 256
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)0);           // цветов в палитре
                    w.Write((byte)0);           // reserved
                    w.Write((short)1);          // плоскостей
                    w.Write((short)32);         // бит на пиксель
                    w.Write(pngs[i].Length);
                    w.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (byte[] p in pngs) w.Write(p);
            }
            Console.WriteLine("Иконка: " + path);
        }

        static byte[] ToPng(Bitmap b)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                b.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        // Кадр иконки в формате DIB: BITMAPINFOHEADER, пиксели BGRA снизу вверх, маска AND (вся прозрачность — в альфе)
        static byte[] ToIconDib(Bitmap b)
        {
            int w = b.Width, h = b.Height;
            int maskStride = ((w + 31) / 32) * 4;
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter wr = new BinaryWriter(ms))
            {
                wr.Write(40); wr.Write(w); wr.Write(h * 2);         // высота удвоена: цвет + маска
                wr.Write((short)1); wr.Write((short)32);
                wr.Write(0); wr.Write(w * h * 4 + maskStride * h);
                wr.Write(0); wr.Write(0); wr.Write(0); wr.Write(0);
                for (int y = h - 1; y >= 0; y--)
                    for (int x = 0; x < w; x++)
                    {
                        Color c = b.GetPixel(x, y);
                        wr.Write(c.B); wr.Write(c.G); wr.Write(c.R); wr.Write(c.A);
                    }
                wr.Write(new byte[maskStride * h]);
                return ms.ToArray();
            }
        }

        static Bitmap DrawIcon(int s)
        {
            Bitmap b = new Bitmap(s, s, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float pad = s * 0.04f;
                RectangleF r = new RectangleF(pad, pad, s - 2 * pad, s - 2 * pad);
                WeatherData sunny = new WeatherData();
                sunny.Sky = Sky.Sunny;
                sunny.IsDay = true;
                Color top, bottom;
                Theme.BgColors(sunny, out top, out bottom);
                using (GraphicsPath shape = Theme.RoundRect(r, s * 0.22f))
                using (LinearGradientBrush bg = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), top, bottom, 90f))
                    g.FillPath(bg, shape);
                float k = s * 0.78f / 105f;   // иконка «солнце за облаком» занимает ~105×90 при k = 1
                WeatherIcons.Draw(g, Sky.PartlyCloudy, false, s / 2f - 2 * k, s / 2f + 6 * k, k, 0);
            }
            return b;
        }

        // ---------- Картинки для README ----------

        // ru — docs\preview.png и forecast.png, en — preview.en.png и forecast.en.png для README.en.md
        static void MakeScreenshots(string dir, string lang)
        {
            L.Use(lang);
            string suffix = L.English ? ".en" : "";
            Directory.CreateDirectory(dir);
            string tempSettings = Path.Combine(Path.GetTempPath(), "WeatherWidget.Tools", Guid.NewGuid().ToString("N"), "settings.txt");
            using (WeatherForm form = new WeatherForm(tempSettings))   // не трогает настройки пользователя
            {
                // Ряд карточек с разной погодой
                List<Bitmap> cards = new List<Bitmap>();
                // для английской версии — города мира с английскими названиями
                City[] row = L.English
                    ? new City[] { Cities.World[0], Cities.World[1], Cities.World[2], Cities.World[24], Cities.World[12] }
                    : new City[] { Moscow, Spb, Cities.World[2], Khanty, Omsk };
                cards.Add(form.RenderPreview(Sample(row[0], 18, 0, true, L.English ? -25200 : 10800), false, 30));
                cards.Add(form.RenderPreview(Sample(row[1], 7, 3, true, L.English ? -14400 : 10800), false, 30));
                cards.Add(form.RenderPreview(Sample(row[2], 11, 63, true, 0), false, 30));
                cards.Add(form.RenderPreview(Sample(row[3], -12, 73, true, L.English ? 3600 : 18000), false, 30));
                cards.Add(form.RenderPreview(Sample(row[4], -4, 0, false, L.English ? 32400 : 21600), false, 30));
                SaveRow(cards, null, Path.Combine(dir, "preview" + suffix + ".png"));

                // Раскрытый прогноз и подсказка
                WeatherData d = Sample(L.English ? Cities.World[2] : Moscow, 9, 2, true, L.English ? 0 : 10800);
                d.Feels = 6; d.Wind = 4.5; d.Humidity = 72;
                Bitmap expanded = form.RenderPreview(d, true, 30);
                Bitmap tip;
                using (TipWindow tw = new TipWindow()) tip = tw.RenderPreview(L.T("Как ощущается", "How it feels"), Advice.TipText(d));
                SaveRow(new List<Bitmap> { expanded }, tip, Path.Combine(dir, "forecast" + suffix + ".png"));
            }
            string tempDir = Path.GetDirectoryName(tempSettings);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Console.WriteLine("Картинки: " + Path.GetFullPath(dir));
        }

        static WeatherData Sample(City c, double temp, int code, bool day, int utcOffset)
        {
            WeatherData d = new WeatherData();
            d.City = c;
            d.Temp = temp;
            d.Feels = temp - 2;
            d.Wind = 3;
            d.Humidity = 60;
            d.Code = code;
            d.Sky = WeatherCodes.ToSky(code);
            d.IsDay = day;
            d.UtcOffset = utcOffset;
            // даты — от сегодняшнего дня: прошедшие дни виджет не показывает
            DateTime nowUtc = DateTime.UtcNow;
            DateTime today = d.Local(nowUtc).Date;
            int[] codes = { 3, 63, 0 };
            for (int i = 0; i < 3; i++)
            {
                DayForecast f = new DayForecast();
                f.Date = today.AddDays(i + 1);
                f.Label = L.DayLabel(f.Date);
                f.Sky = WeatherCodes.ToSky(codes[i]);
                f.Max = temp + 2 - i;
                f.Min = temp - 5 - i;
                d.Days.Add(f);
            }
            // сутки по часам: днём теплее, через 5–7 часов дождь
            DateTime hour0 = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < 24; i++)
            {
                HourForecast h = new HourForecast();
                h.Utc = hour0.AddHours(i);
                int localHour = d.Local(h.Utc).Hour;
                h.IsDay = localHour >= 7 && localHour < 18;
                h.Temp = temp + (h.IsDay ? 1 : -2) - i * 0.15;
                bool rain = i >= 5 && i <= 7;
                h.Code = rain ? 61 : i < 3 ? code : 2;
                h.Precip = rain ? 70 + i : 10;
                h.Sky = WeatherCodes.ToSky(h.Code);
                d.Hours.Add(h);
            }
            return d;
        }

        // Карточки в ряд на мягком фоне (как на рабочем столе), справа — подсказка
        static void SaveRow(List<Bitmap> cards, Bitmap tip, string path)
        {
            const int gap = 28;
            int w = gap, h = 0;
            foreach (Bitmap c in cards) { w += c.Width + gap; h = Math.Max(h, c.Height); }
            if (tip != null) { w += tip.Width + gap; h = Math.Max(h, tip.Height); }
            h += gap * 2;
            using (Bitmap outB = new Bitmap(w, h))
            {
                using (Graphics g = Graphics.FromImage(outB))
                {
                    using (LinearGradientBrush bg = new LinearGradientBrush(new Rectangle(0, 0, w, h),
                        Color.FromArgb(206, 222, 238), Color.FromArgb(232, 226, 240), 30f))
                        g.FillRectangle(bg, 0, 0, w, h);
                    int x = gap;
                    foreach (Bitmap c in cards)
                    {
                        DrawShadow(g, new Rectangle(x, gap, c.Width, c.Height));
                        g.DrawImage(c, x, gap);
                        x += c.Width + gap;
                    }
                    if (tip != null) g.DrawImage(tip, x, gap + 120);
                }
                outB.Save(path, ImageFormat.Png);
            }
            foreach (Bitmap c in cards) c.Dispose();
            if (tip != null) tip.Dispose();
        }

        static void DrawShadow(Graphics g, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 6; i >= 1; i--)
            {
                RectangleF rr = new RectangleF(r.X - i + 2, r.Y - i + 5, r.Width + 2 * i - 4, r.Height + 2 * i - 4);
                using (GraphicsPath p = Theme.RoundRect(rr, 22 + i))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(8, 20, 30, 60)))
                    g.FillPath(b, p);
            }
        }
    }
}
