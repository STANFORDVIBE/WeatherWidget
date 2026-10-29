using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace WeatherWidget
{
    // Векторные анимированные иконки погоды. phase — время анимации (1 шаг = 40 мс).
    static class WeatherIcons
    {
        // Иконка с центром (cx, cy); k — масштаб (1 = большая иконка на карточке)
        public static void Draw(Graphics g, Sky sky, bool night, float cx, float cy, float k, float phase)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(k, k);
            switch (sky)
            {
                case Sky.Sunny:
                    if (night) { DrawStars(g, phase); DrawMoon(g, 0, 0, 28, phase); }
                    else DrawSun(g, 0, 0, 30, phase);
                    break;
                case Sky.PartlyCloudy:
                    if (night) DrawMoon(g, 18, -14, 20, phase); else DrawSun(g, 18, -14, 22, phase);
                    DrawCloud(g, -12 + (float)Math.Sin(phase * 0.04) * 6, 14, 0.8f, Color.FromArgb(245, 250, 255));
                    break;
                case Sky.Cloudy:
                    DrawCloud(g, 20 + (float)Math.Sin(phase * 0.03) * 8, -12, 0.7f, Color.FromArgb(200, 210, 222));
                    DrawCloud(g, -10 + (float)Math.Sin(phase * 0.04 + 1) * 6, 10, 0.9f, Color.FromArgb(235, 240, 246));
                    break;
                case Sky.Rain:
                    DrawDrops(g, 0, 0, false, phase);
                    DrawCloud(g, (float)Math.Sin(phase * 0.03) * 4, -12, 0.95f, Color.FromArgb(175, 185, 200));
                    break;
                case Sky.Snow:
                    DrawDrops(g, 0, 0, true, phase);
                    DrawCloud(g, (float)Math.Sin(phase * 0.03) * 4, -12, 0.95f, Color.FromArgb(225, 232, 242));
                    break;
            }
            g.Restore(st);
        }

        static void DrawSun(Graphics g, float cx, float cy, float r, float phase)
        {
            float angle = phase * 0.015f;   // лучи медленно вращаются и «дышат»
            using (Pen ray = new Pen(Color.FromArgb(255, 220, 90), r * 0.13f))
            {
                ray.StartCap = LineCap.Round;
                ray.EndCap = LineCap.Round;
                for (int i = 0; i < 10; i++)
                {
                    double a = angle + i * Math.PI / 5;
                    float r1 = r * 1.3f, r2 = r * (1.6f + 0.1f * (float)Math.Sin(phase * 0.1 + i));
                    g.DrawLine(ray, cx + (float)Math.Cos(a) * r1, cy + (float)Math.Sin(a) * r1,
                                    cx + (float)Math.Cos(a) * r2, cy + (float)Math.Sin(a) * r2);
                }
            }
            using (SolidBrush glow = new SolidBrush(Color.FromArgb(70, 255, 230, 120)))
                g.FillEllipse(glow, cx - r * 1.15f, cy - r * 1.15f, r * 2.3f, r * 2.3f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 205, 50)))
                g.FillEllipse(b, cx - r, cy - r, r * 2, r * 2);
        }

        static void DrawMoon(Graphics g, float cx, float cy, float r, float phase)
        {
            int glowAlpha = 45 + (int)(20 * Math.Sin(phase * 0.05));   // мягкое «дыхание» свечения
            using (SolidBrush glow = new SolidBrush(Color.FromArgb(glowAlpha, 220, 230, 255)))
                g.FillEllipse(glow, cx - r * 1.4f, cy - r * 1.4f, r * 2.8f, r * 2.8f);

            // полумесяц: круг луны минус смещённый круг
            GraphicsState st = g.Save();
            using (GraphicsPath cut = new GraphicsPath())
            {
                cut.AddEllipse(cx - r * 0.35f, cy - r * 1.25f, r * 1.9f, r * 1.9f);
                g.SetClip(cut, CombineMode.Exclude);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 240, 200)))
                    g.FillEllipse(b, cx - r, cy - r, r * 2, r * 2);
            }
            g.Restore(st);
        }

        static void DrawStars(Graphics g, float phase)
        {
            float[,] pos = { { -62, -32 }, { 58, -42 }, { -44, 38 }, { 66, 22 }, { -74, 4 }, { 34, -58 }, { 48, 50 } };
            for (int i = 0; i < pos.GetLength(0); i++)
            {
                int a = 130 + (int)(110 * Math.Sin(phase * 0.07 + i * 1.7));   // мерцание
                float s = 2.5f + (i % 3) * 0.7f;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 255, 255, 230)))
                    g.FillEllipse(b, pos[i, 0] - s / 2, pos[i, 1] - s / 2, s, s);
            }
        }

        static void DrawCloud(Graphics g, float cx, float cy, float s, Color c)
        {
            using (GraphicsPath p = new GraphicsPath(FillMode.Winding))   // Winding — без «дыр» на пересечениях
            {
                // боковые круги касаются плоского низа (y = cy + 26s), а прямоугольник между ними
                // идёт от их центров — так низ ровный и переходит в бока без ступенек
                AddCircle(p, cx - 33 * s, cy + 9 * s, 17 * s);
                AddCircle(p, cx - 4 * s, cy - 8 * s, 25 * s);
                AddCircle(p, cx + 31 * s, cy + 7 * s, 19 * s);
                p.AddRectangle(new RectangleF(cx - 33 * s, cy + 9 * s, 64 * s, 17 * s));
                using (SolidBrush b = new SolidBrush(c))
                    g.FillPath(b, p);
            }
        }

        static void AddCircle(GraphicsPath p, float x, float y, float r)
        {
            p.AddEllipse(x - r, y - r, 2 * r, 2 * r);
        }

        // Капли дождя или снежинки, падающие из-под облака
        static void DrawDrops(Graphics g, float cx, float cy, bool snow, float phase)
        {
            const int n = 9;
            const float fall = 55;
            using (Pen drop = new Pen(Color.FromArgb(150, 200, 255), 2.5f))
            using (SolidBrush flake = new SolidBrush(Color.White))
            {
                drop.StartCap = LineCap.Round;
                drop.EndCap = LineCap.Round;
                for (int i = 0; i < n; i++)
                {
                    float speed = snow ? 0.8f : 2.6f;
                    float y = (phase * speed + i * 23) % fall;
                    float x = cx - 36 + i * 9;
                    float yy = cy + 8 + y;
                    int alpha = (int)(255 * (1 - y / fall));   // внизу тают
                    if (snow)
                    {
                        flake.Color = Color.FromArgb(alpha, 255, 255, 255);
                        float sx = x + (float)Math.Sin(phase * 0.08 + i) * 4;
                        g.FillEllipse(flake, sx - 3, yy - 3, 6, 6);
                    }
                    else
                    {
                        drop.Color = Color.FromArgb(alpha, 150, 200, 255);
                        g.DrawLine(drop, x, yy, x - 3, yy + 9);
                    }
                }
            }
        }
    }
}
