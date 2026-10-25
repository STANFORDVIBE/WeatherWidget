using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WeatherWidget
{
    // Отрисовка карточки и анимации
    sealed partial class WeatherForm
    {
        // Все анимации считаются от реального времени, а не от числа тиков таймера —
        // поэтому идут ровно, даже если таймер срабатывает неравномерно.
        const int TransMs = 600, ExpMs = 260;
        const byte WindowAlpha = 242;   // общая непрозрачность виджета (~95%)

        readonly System.Windows.Forms.Timer animTimer = new System.Windows.Forms.Timer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly LayeredSurface surface = new LayeredSurface();
        float phase;                    // время фоновых анимаций (1 шаг = 40 мс)
        WeatherData prevData;           // город, который гаснет при плавной смене
        float trans = 1;                // прогресс смены города 0..1 (1 — смена завершена)
        double transStart;
        float exp, expTarget, expFrom;  // раскрытие прогноза 0..1
        double expStart;
        bool btnHover;

        // шрифты и форматы создаются один раз, а не в каждом кадре
        readonly Font fCity = new Font("Segoe UI Semibold", 17, GraphicsUnit.Pixel);
        readonly Font fTemp = new Font("Segoe UI Light", 52, GraphicsUnit.Pixel);
        readonly Font fDesc = new Font("Segoe UI", 15, GraphicsUnit.Pixel);
        readonly Font fSmall = new Font("Segoe UI", 11, GraphicsUnit.Pixel);
        readonly Font fDay = new Font("Segoe UI", 12, GraphicsUnit.Pixel);
        readonly Font fDayTemp = new Font("Segoe UI Semibold", 13, GraphicsUnit.Pixel);
        readonly StringFormat center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        readonly StringFormat cityFmt = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };

        // области макета 240×H (в базовых координатах)
        readonly Rectangle cityRect = new Rectangle(16, 8, 208, 26);   // строка с названием города
        Rectangle cityHit = Rectangle.Empty;   // сама надпись «Город ▾» — только она кликабельна; считается при отрисовке
        readonly Rectangle tempRect = new Rectangle(40, 138, 160, 56);
        Rectangle btnRect = Rectangle.Empty;   // кнопка прогноза; положение считается при отрисовке (справа от времени)

        // текущая высота карточки: от свёрнутой (H0) до раскрытой (H1)
        float BaseH { get { return H0 + (H1 - H0) * exp; } }

        void Animate()
        {
            double now = clock.Elapsed.TotalMilliseconds;
            phase = (float)(now / 40);
            if (trans < 1) trans = (float)Math.Min(1, (now - transStart) / TransMs);
            if (exp != expTarget)
            {
                float t = (float)Math.Min(1, (now - expStart) / ExpMs);
                exp = t >= 1 ? expTarget : expFrom + (expTarget - expFrom) * Theme.Ease(t);
            }
            // во время переходов — ~60 кадров/с для плавности, в покое — 25 кадров/с, чтобы не грузить систему
            int want = trans < 1 || exp != expTarget ? 15 : 40;
            if (animTimer.Interval != want) animTimer.Interval = want;
            Present();
        }

        // Картинка карточки без показа окна — для README и инструментов (tools\)
        internal Bitmap RenderPreview(WeatherData d, bool expanded, float atPhase)
        {
            data = d;
            prevData = null;
            trans = 1;
            error = null;
            exp = expTarget = expanded ? 1 : 0;
            phase = atPhase;
            scale = 1;
            Bitmap b = new Bitmap(W, (int)BaseH, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b)) Render(g);
            return b;
        }

        void StartCityTransition()
        {
            prevData = data;
            trans = 0;
            transStart = clock.Elapsed.TotalMilliseconds;
        }

        void ToggleForecast()
        {
            expFrom = exp;
            expTarget = expTarget > 0.5f ? 0 : 1;
            expStart = clock.Elapsed.TotalMilliseconds;
        }

        // Нарисовать кадр и показать его.
        // UpdateLayeredWindow подгоняет окно под размер кадра, поэтому кадр должен быть ровно
        // размером с окно: берём размер у самого окна (а не из кэша WinForms) и рисуем только
        // у показанного окна. Пока окно создаётся, у него на миг есть рамка с заголовком —
        // если тогда отдать кадр размером с внутреннюю часть, окно навсегда «ужмётся» на размер рамки
        // (так раньше сбивался размер виджета).
        void Present()
        {
            if (!IsHandleCreated || !Visible) return;
            NativeMethods.RECT rc;
            if (!NativeMethods.GetWindowRect(Handle, out rc)) return;
            int w = rc.Right - rc.Left, h = rc.Bottom - rc.Top;
            if (w <= 0 || h <= 0) return;
            scale = w / (float)W;
            surface.Present(Handle, w, h, WindowAlpha, Render);
        }

        void Render(Graphics g)
        {
            g.Clear(Color.Transparent);   // всё вне карточки — полностью прозрачное
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.ScaleTransform(scale, scale);   // дальше рисуем в базовых координатах (ширина 240)
            float bh = BaseH;

            // фон: при смене города плавно перетекает из старого цвета в новый
            Color top, bottom;
            Theme.BgColors(data, out top, out bottom);
            bool fading = trans < 1 && prevData != null;
            if (fading)
            {
                Color pt, pb;
                Theme.BgColors(prevData, out pt, out pb);
                float k = Theme.Ease(trans);
                top = Theme.Lerp(pt, top, k);
                bottom = Theme.Lerp(pb, bottom, k);
            }

            RectangleF gradRect = new RectangleF(0, -1, W, bh + 2);   // с запасом, чтобы градиент не «заворачивался» на краях
            using (GraphicsPath shape = Theme.RoundRect(new RectangleF(0, 0, W, bh), 22))
            {
                using (LinearGradientBrush bg = new LinearGradientBrush(gradRect, top, bottom, 90f))
                    g.FillPath(bg, shape);

                // содержимое обрезается по карточке (важно, пока прогноз выезжает)
                g.SetClip(shape);
                // первая половина перехода — старый город гаснет, вторая — новый проявляется
                DrawContent(g, fading && trans < 0.5f ? prevData : data, bh);
                g.ResetClip();

                float alpha = !fading ? 1 : (trans < 0.5f ? 1 - trans * 2 : (trans - 0.5f) * 2);
                if (alpha < 1)
                {
                    int a = (int)(255 * (1 - alpha));
                    using (LinearGradientBrush veil = new LinearGradientBrush(gradRect, Color.FromArgb(a, top), Color.FromArgb(a, bottom), 90f))
                        g.FillPath(veil, shape);
                }
            }
        }

        void DrawContent(Graphics g, WeatherData d, float bh)
        {
            City shown = d != null ? d.City : target;
            string cityLabel = (shown != null ? shown.Name : "Выберите город") + " ▾";
            g.DrawString(cityLabel, fCity, Brushes.White, cityRect, cityFmt);
            float labelW = Math.Min(g.MeasureString(cityLabel, fCity, PointF.Empty, StringFormat.GenericTypographic).Width, cityRect.Width);
            cityHit = new Rectangle((int)(W / 2f - labelW / 2) - 6, cityRect.Y, (int)labelW + 12, cityRect.Height);   // текст по центру + небольшой запас

            if (d == null)
            {
                string msg = error ?? (shown == null ? "Нажмите «Выберите город»\nи найдите свой город" : "Загрузка…");
                g.DrawString(msg, fDesc, Brushes.White, new RectangleF(0, 0, W, bh), center);
                return;
            }

            WeatherIcons.Draw(g, d.Sky, !d.IsDay, W / 2f, 88f, 1f, phase);
            g.DrawString(Format.Deg(d.Temp), fTemp, Brushes.White, new RectangleF(40, 138, 160, 56), center);
            g.DrawString(WeatherCodes.Describe(d.Code), fDesc, Brushes.White, new RectangleF(0, 196, W, 22), center);
            DrawStatusLine(g, d);
            DrawForecastButton(g);

            // прогноз на 3 дня — виден, когда карточка раскрыта
            if (exp <= 0) return;
            const float fy = H0;
            using (Pen line = new Pen(Color.FromArgb(70, 255, 255, 255), 1))
                g.DrawLine(line, 20, fy + 2, W - 20, fy + 2);
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(215, 255, 255, 255)))
            {
                for (int i = 0; i < d.Days.Count; i++)
                {
                    DayForecast day = d.Days[i];
                    float x = 40 + i * 80;
                    g.DrawString(day.Label, fDay, dim, new RectangleF(x - 40, fy + 8, 80, 18), center);
                    WeatherIcons.Draw(g, day.Sky, false, x, fy + 44, 0.36f, phase);
                    g.DrawString(Format.Deg(day.Max) + " / " + Format.Deg(day.Min), fDayTemp, Brushes.White,
                        new RectangleF(x - 40, fy + 70, 80, 20), center);
                }
            }
        }

        // Строка «местное время 12:30» (или режим/ошибка) и место под кнопку прогноза справа от неё
        void DrawStatusLine(Graphics g, WeatherData d)
        {
            string time = DateTime.UtcNow.AddSeconds(d.UtcOffset).ToString("HH:mm");   // время города, а не компьютера
            string s = error ??
                (d.Stale ? "нет связи · данные " + Format.Age(DateTime.UtcNow - d.FetchedUtc)
                         : (dynamic ? "динамическая погода · " : "местное время ") + time);

            const float bs = 14, gap = 6, cy = 231;   // текст и кнопка вместе — по центру
            StringFormat typo = StringFormat.GenericTypographic;
            float tw = g.MeasureString(s, fSmall, PointF.Empty, typo).Width;
            float x0 = (W - (tw + gap + bs)) / 2;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                g.DrawString(s, fSmall, b, new PointF(x0, cy - fSmall.GetHeight(g) / 2), typo);
            btnRect = new Rectangle((int)Math.Round(x0 + tw + gap), (int)(cy - bs / 2), (int)bs, (int)bs);
        }

        // Кнопка прогноза: стрелка плавно переворачивается при раскрытии
        void DrawForecastButton(Graphics g)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(btnHover ? 90 : 45, 255, 255, 255)))
                g.FillEllipse(b, btnRect);
            using (Pen p = new Pen(Color.White, 1.5f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                float cx = btnRect.X + btnRect.Width / 2f, cy = btnRect.Y + btnRect.Height / 2f;
                float dy = 1.8f * (1 - 2 * exp);
                g.DrawLines(p, new PointF[] { new PointF(cx - 3.5f, cy - dy), new PointF(cx, cy + dy), new PointF(cx + 3.5f, cy - dy) });
            }
        }
    }
}
