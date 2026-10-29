using System;
using System.Collections.Generic;
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
        public const int MaxTransparency = 100;   // 100 % — фон невидим, остаются только текст и анимация

        int bgTransparency = 5;         // прозрачность фона карточки, %; текст и анимация всегда непрозрачные

        // Прозрачность фона карточки, 0..MaxTransparency %
        internal int BgTransparency
        {
            get { return bgTransparency; }
            set
            {
                int v = Math.Max(0, Math.Min(MaxTransparency, value));
                if (v == bgTransparency) return;
                bgTransparency = v;
                Present();
            }
        }

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
        readonly Font fHourTemp = new Font("Segoe UI Semibold", 12, GraphicsUnit.Pixel);
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
            surface.Present(Handle, w, h, 255, Render);
        }

        void Render(Graphics g)
        {
            g.Clear(Color.Transparent);   // всё вне карточки — полностью прозрачное
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            RectangleF frame = g.VisibleClipBounds;   // размер кадра в пикселях
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

            // фон с настраиваемой прозрачностью; текст и анимация рисуются поверх непрозрачными
            // не ниже 1: пиксели с нулевой альфой layered-окно пропускает насквозь,
            // и при 100 % карточку нельзя было бы перетащить или открыть меню
            int bgAlpha = Math.Max(1, 255 * (100 - bgTransparency) / 100);
            RectangleF gradRect = new RectangleF(0, -1, W, bh + 2);   // с запасом, чтобы градиент не «заворачивался» на краях
            using (GraphicsPath shape = Theme.RoundRect(new RectangleF(0, 0, W, bh), 22))
            {
                using (LinearGradientBrush bg = new LinearGradientBrush(gradRect, Color.FromArgb(bgAlpha, top), Color.FromArgb(bgAlpha, bottom), 90f))
                    g.FillPath(bg, shape);

                // первая половина перехода — старый город гаснет, вторая — новый проявляется
                WeatherData show = fading && trans < 0.5f ? prevData : data;
                float alpha = !fading ? 1 : (trans < 0.5f ? 1 - trans * 2 : (trans - 0.5f) * 2);
                if (alpha >= 1)
                {
                    g.SetClip(shape);   // содержимое обрезается по карточке (важно, пока прогноз выезжает)
                    DrawContent(g, show, bh);
                    g.ResetClip();
                }
                else if (alpha > 0)
                    DrawContentFaded(g, show, bh, shape, frame, alpha);
            }
        }

        // Содержимое с прозрачностью (смена города): рисуем его на отдельный слой и накладываем.
        // Закрашивать цветом фона нельзя — фон может быть прозрачным, и старый город просвечивал бы.
        void DrawContentFaded(Graphics g, WeatherData d, float bh, GraphicsPath shape, RectangleF frame, float alpha)
        {
            int w = (int)Math.Ceiling(frame.Width), h = (int)Math.Ceiling(frame.Height);
            using (Bitmap layer = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            {
                using (Graphics lg = Graphics.FromImage(layer))
                {
                    lg.SmoothingMode = SmoothingMode.AntiAlias;
                    lg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    lg.ScaleTransform(scale, scale);
                    lg.SetClip(shape);
                    DrawContent(lg, d, bh);
                }
                System.Drawing.Imaging.ColorMatrix cm = new System.Drawing.Imaging.ColorMatrix();
                cm.Matrix33 = alpha;
                using (System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes())
                {
                    ia.SetColorMatrix(cm);
                    GraphicsState st = g.Save();
                    g.ResetTransform();
                    g.DrawImage(layer, new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, ia);
                    g.Restore(st);
                }
            }
        }

        void DrawContent(Graphics g, WeatherData d, float bh)
        {
            City shown = d != null ? d.City : target;
            string cityLabel = (shown != null ? shown.Name : L.T("Выберите город", "Choose a city")) + " ▾";
            g.DrawString(cityLabel, fCity, Brushes.White, cityRect, cityFmt);
            float labelW = Math.Min(g.MeasureString(cityLabel, fCity, PointF.Empty, StringFormat.GenericTypographic).Width, cityRect.Width);
            cityHit = new Rectangle((int)(W / 2f - labelW / 2) - 6, cityRect.Y, (int)labelW + 12, cityRect.Height);   // текст по центру + небольшой запас

            if (d == null)
            {
                string msg = error ?? (shown == null
                    ? L.T("Нажмите «Выберите город»\nи найдите свой город", "Click “Choose a city”\nand find your city")
                    : L.T("Загрузка…", "Loading…"));
                g.DrawString(msg, fDesc, Brushes.White, new RectangleF(0, 0, W, bh), center);
                return;
            }

            WeatherIcons.Draw(g, d.Sky, !d.IsDay, W / 2f, 88f, 1f, phase);
            g.DrawString(Format.Deg(d.Temp), fTemp, Brushes.White, new RectangleF(40, 138, 160, 56), center);
            g.DrawString(WeatherCodes.Describe(d.Code), fDesc, Brushes.White, new RectangleF(0, 196, W, 22), center);
            DrawStatusLine(g, d);
            DrawForecastButton(g);

            // прогноз по часам и на 3 дня — виден, когда карточка раскрыта
            if (exp <= 0) return;
            DateTime now = DateTime.UtcNow;
            DrawHours(g, d, now, H0);
            DrawDays(g, d, now, H0 + HoursH);
        }

        const int HourSlots = 6, HourStep = 2;   // 6 точек через 2 часа — полсуток вперёд

        // Почасовой прогноз: время, погода, температура и вероятность осадков (если заметная)
        void DrawHours(Graphics g, WeatherData d, DateTime now, float fy)
        {
            DrawSeparator(g, fy);
            List<HourForecast> slots = HourSlotsFrom(d.HoursFrom(now), now);
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(215, 255, 255, 255)))
            {
                if (slots.Count == 0)
                {
                    g.DrawString(L.T("почасовой прогноз появится после обновления", "hourly forecast will appear after refresh"), fSmall, dim, new RectangleF(0, fy, W, HoursH), center);
                    return;
                }
                using (SolidBrush rain = new SolidBrush(Color.FromArgb(235, 175, 220, 255)))
                {
                    const float slotW = W / (float)HourSlots;
                    for (int i = 0; i < slots.Count; i++)
                    {
                        HourForecast h = slots[i];
                        float x = slotW * (i + 0.5f);
                        g.DrawString(d.Local(h.Utc).ToString("HH:mm"), fSmall, dim, new RectangleF(x - slotW / 2, fy + 6, slotW, 16), center);
                        WeatherIcons.Draw(g, h.Sky, !h.IsDay, x, fy + 34, 0.24f, phase);
                        g.DrawString(Format.Deg(h.Temp), fHourTemp, Brushes.White, new RectangleF(x - slotW / 2, fy + 48, slotW, 18), center);
                        if (h.Precip >= 20)
                            g.DrawString(h.Precip + "%", fSmall, rain, new RectangleF(x - slotW / 2, fy + 64, slotW, 15), center);
                    }
                }
            }
        }

        // Точки для полоски: следующие часы после текущего, не чаще чем через HourStep часов
        // (у MET Norway шаг ряда бывает больше часа — тогда берём что есть)
        static List<HourForecast> HourSlotsFrom(List<HourForecast> hours, DateTime now)
        {
            List<HourForecast> slots = new List<HourForecast>();
            DateTime next = now;
            foreach (HourForecast h in hours)
            {
                if (slots.Count == HourSlots) break;
                if (h.Utc <= now || h.Utc < next) continue;
                slots.Add(h);
                next = h.Utc.AddHours(HourStep);
            }
            return slots;
        }

        // Прогноз на 3 дня; прошедшие дни (данные из старого кэша) не показываются
        void DrawDays(Graphics g, WeatherData d, DateTime now, float fy)
        {
            DrawSeparator(g, fy);
            List<DayForecast> days = d.UpcomingDays(now, 3);
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(215, 255, 255, 255)))
            {
                for (int i = 0; i < days.Count; i++)
                {
                    DayForecast day = days[i];
                    float x = W / 2f + (i - (days.Count - 1) / 2f) * 80;   // по центру, сколько бы дней ни было
                    string label = day.Date != DateTime.MinValue ? L.DayLabel(day.Date) : day.Label;   // язык мог смениться после загрузки
                    g.DrawString(label, fDay, dim, new RectangleF(x - 40, fy + 8, 80, 18), center);
                    WeatherIcons.Draw(g, day.Sky, false, x, fy + 44, 0.36f, phase);
                    g.DrawString(Format.Deg(day.Max) + " / " + Format.Deg(day.Min), fDayTemp, Brushes.White,
                        new RectangleF(x - 40, fy + 70, 80, 20), center);
                }
            }
        }

        static void DrawSeparator(Graphics g, float y)
        {
            using (Pen line = new Pen(Color.FromArgb(70, 255, 255, 255), 1))
                g.DrawLine(line, 20, y + 2, W - 20, y + 2);
        }

        // Строка «местное время 12:30» (или режим/ошибка) и место под кнопку прогноза справа от неё
        void DrawStatusLine(Graphics g, WeatherData d)
        {
            string time = DateTime.UtcNow.AddSeconds(d.UtcOffset).ToString("HH:mm");   // время города, а не компьютера
            string s = error ??
                (d.Stale && loading ? L.T("обновление…", "updating…")   // показан кэш, свежие данные уже грузятся
               : d.Stale ? L.T("нет связи · данные ", "offline · data from ") + Format.Age(DateTime.UtcNow - d.FetchedUtc)
               : (dynamic ? L.T("динамическая погода · ", "dynamic weather · ") : L.T("местное время ", "local time ")) + time);

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
