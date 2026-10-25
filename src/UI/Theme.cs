using System.Drawing;
using System.Drawing.Drawing2D;

namespace WeatherWidget
{
    // Цвета фона и вспомогательная геометрия
    static class Theme
    {
        // Всплывающие панели (выбор города, подсказки): тёмный фон, бирюзовый акцент
        public static readonly Color Accent = Color.FromArgb(79, 195, 176);
        public const int PanelShadow = 14;   // поле вокруг панели под тень

        // Тёмная панель со скруглёнными углами, мягкой тенью и тонкой рамкой
        public static void DrawPanel(Graphics g, RectangleF panel, float radius)
        {
            for (int i = PanelShadow; i >= 1; i -= 2)
                using (GraphicsPath p = RoundRect(new RectangleF(panel.X - i + 2, panel.Y - i + 5, panel.Width + 2 * i - 4, panel.Height + 2 * i - 4), radius + i))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(7, 0, 0, 0)))
                    g.FillPath(b, p);

            using (GraphicsPath p = RoundRect(panel, radius))
            {
                using (LinearGradientBrush bg = new LinearGradientBrush(new RectangleF(panel.X, panel.Y - 1, panel.Width, panel.Height + 2),
                    Color.FromArgb(250, 26, 36, 54), Color.FromArgb(250, 34, 49, 72), 90f))
                    g.FillPath(bg, p);
                using (Pen border = new Pen(Color.FromArgb(34, 255, 255, 255)))
                    g.DrawPath(border, p);
            }
        }

        // Градиент фона карточки по погоде; ночью темнее
        public static void BgColors(WeatherData d, out Color top, out Color bottom)
        {
            Sky sky = d != null ? d.Sky : Sky.Cloudy;
            bool night = d != null && !d.IsDay;
            switch (sky)
            {
                case Sky.Sunny: top = Color.FromArgb(14, 143, 179); bottom = Color.FromArgb(79, 195, 176); break;          // «бирюзовое лето»
                case Sky.PartlyCloudy: top = Color.FromArgb(70, 130, 200); bottom = Color.FromArgb(140, 180, 220); break;
                case Sky.Rain: top = Color.FromArgb(45, 60, 85); bottom = Color.FromArgb(85, 105, 135); break;
                case Sky.Snow: top = Color.FromArgb(90, 115, 150); bottom = Color.FromArgb(160, 180, 205); break;
                default: top = Color.FromArgb(54, 98, 122); bottom = Color.FromArgb(128, 166, 180); break;                // пасмурно: «стальная бирюза»
            }
            if (night)
            {
                if (sky == Sky.Sunny || sky == Sky.PartlyCloudy) { top = Color.FromArgb(12, 24, 52); bottom = Color.FromArgb(38, 62, 104); }
                else { top = Darken(top, 0.55f); bottom = Darken(bottom, 0.55f); }
            }
        }

        public static Color Lerp(Color a, Color b, float k)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * k), (int)(a.G + (b.G - a.G) * k), (int)(a.B + (b.B - a.B) * k));
        }

        public static Color Darken(Color c, float k)
        {
            return Color.FromArgb((int)(c.R * k), (int)(c.G * k), (int)(c.B * k));
        }

        // Плавный разгон и торможение анимации (0..1 → 0..1)
        public static float Ease(float t) { return t * t * (3 - 2 * t); }

        public static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
