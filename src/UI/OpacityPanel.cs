using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WeatherWidget
{
    // Панель «Прозрачность фона» с ползунком, в стиле панели выбора города.
    // Виджет меняется сразу, пока двигаешь ползунок. Колёсико и стрелки — шаг 5 %,
    // Esc, Enter или клик мимо — закрыть (значение сохраняется).
    sealed class OpacityPanel : Form
    {
        const int Shadow = Theme.PanelShadow;
        const int PanelW = 260, PanelH = 92, Pad = 16, Radius = 14, Step = 5;
        const int TrackY = Pad + 38, KnobR = 9;

        readonly WeatherForm owner;
        readonly LayeredSurface surface = new LayeredSurface();
        readonly Font fTitle = new Font("Segoe UI Semibold", 13, GraphicsUnit.Pixel);
        readonly Font fHint = new Font("Segoe UI", 11, GraphicsUnit.Pixel);
        bool dragging, knobHover;
        float dpi = 1f;   // масштаб экрана, где открыта панель

        public OpacityPanel(WeatherForm owner)
        {
            this.owner = owner;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            Size = new Size(PanelW + 2 * Shadow, PanelH + 2 * Shadow);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        // Открыть под карточкой (а если внизу нет места — над ней); координаты экранные
        public void ShowNear(int centerX, int cardTop, int cardBottom)
        {
            dpi = Dpi.AtPoint(new Point(centerX, cardBottom));
            Size = new Size(Dpi.Px(PanelW + 2 * Shadow, dpi), Dpi.Px(PanelH + 2 * Shadow, dpi));
            Rectangle wa = Screen.FromPoint(new Point(centerX, cardBottom)).WorkingArea;
            int x = Math.Max(wa.Left, Math.Min(centerX - Width / 2, wa.Right - Width));
            int gap = Dpi.Px(Shadow - 8, dpi);
            int y = cardBottom - gap;
            if (y + Height > wa.Bottom) y = cardTop - Height + gap;
            Location = new Point(x, Math.Max(wa.Top, y));
            Show(owner);
            Activate();
            Present();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Close();   // клик мимо панели — закрыть
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            owner.SaveSettings();
            surface.Dispose();
            fTitle.Dispose();
            fHint.Dispose();
        }

        // ---------- Значение ----------

        int Value
        {
            get { return owner.BgTransparency; }
            set
            {
                int v = Math.Max(0, Math.Min(WeatherForm.MaxTransparency, value));
                if (v == owner.BgTransparency) return;
                owner.BgTransparency = v;   // виджет перерисуется сразу
                Present();
            }
        }

        float TrackLeft { get { return Shadow + Pad + KnobR; } }
        float TrackRight { get { return Shadow + PanelW - Pad - KnobR; } }
        float KnobX { get { return TrackLeft + (TrackRight - TrackLeft) * Value / WeatherForm.MaxTransparency; } }
        float TrackCenterY { get { return Shadow + TrackY; } }

        int ValueAt(int x)
        {
            float k = (x - TrackLeft) / (TrackRight - TrackLeft);
            return (int)Math.Round(Math.Max(0, Math.Min(1, k)) * WeatherForm.MaxTransparency);
        }

        bool OnTrack(Point p)
        {
            return p.X >= TrackLeft - KnobR - 4 && p.X <= TrackRight + KnobR + 4 && Math.Abs(p.Y - TrackCenterY) <= 14;
        }

        // ---------- Мышь и клавиатура ----------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Point p = Dpi.ToBase(e.Location, dpi);
            if (e.Button != MouseButtons.Left || !OnTrack(p)) return;
            dragging = true;
            Value = ValueAt(p.X);   // клик по полосе — ползунок сразу туда
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point p = Dpi.ToBase(e.Location, dpi);
            if (dragging) { Value = ValueAt(p.X); return; }
            bool over = OnTrack(p);
            Cursor = over ? Cursors.Hand : Cursors.Default;
            if (over != knobHover) { knobHover = over; Present(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (knobHover && !dragging) { knobHover = false; Present(); }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Value += e.Delta > 0 ? Step : -Step;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Escape:
                case Keys.Enter:
                    Close();
                    return true;
                case Keys.Left:
                case Keys.Down:
                    Value -= Step;
                    return true;
                case Keys.Right:
                case Keys.Up:
                    Value += Step;
                    return true;
                case Keys.Home:
                    Value = 0;
                    return true;
                case Keys.End:
                    Value = WeatherForm.MaxTransparency;
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---------- Отрисовка ----------

        void Present()
        {
            if (!IsHandleCreated || !Visible || IsDisposed) return;
            surface.Present(Handle, Width, Height, 255, Render, dpi);
        }

        // Картинка панели без показа окна — для тестов
        internal Bitmap RenderPreview()
        {
            Bitmap b = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b)) Render(g);
            return b;
        }

        void Render(Graphics g)
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            Theme.DrawPanel(g, new RectangleF(Shadow, Shadow, PanelW, PanelH), Radius);

            // заголовок и значение
            float x = Shadow + Pad, y = Shadow + Pad - 2;
            g.DrawString(L.T("Прозрачность фона", "Background transparency"), fTitle, Brushes.White, x, y);
            string val = Value + "%";
            SizeF vs = g.MeasureString(val, fTitle);
            using (SolidBrush accent = new SolidBrush(Theme.Accent))
                g.DrawString(val, fTitle, accent, Shadow + PanelW - Pad - vs.Width + 4, y);

            // полоса: заполненная часть бирюзовая
            float cy = TrackCenterY, kx = KnobX;
            using (GraphicsPath track = Theme.RoundRect(new RectangleF(TrackLeft, cy - 3, TrackRight - TrackLeft, 6), 3))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                g.FillPath(b, track);
            if (kx > TrackLeft + 1)
                using (GraphicsPath fill = Theme.RoundRect(new RectangleF(TrackLeft, cy - 3, kx - TrackLeft, 6), 3))
                using (SolidBrush b = new SolidBrush(Theme.Accent))
                    g.FillPath(b, fill);

            // ручка
            float r = KnobR + (knobHover || dragging ? 1.5f : 0);
            using (SolidBrush halo = new SolidBrush(Color.FromArgb(knobHover || dragging ? 60 : 30, 79, 195, 176)))
                g.FillEllipse(halo, kx - r - 5, cy - r - 5, 2 * r + 10, 2 * r + 10);
            using (SolidBrush knob = new SolidBrush(Color.White))
                g.FillEllipse(knob, kx - r, cy - r, 2 * r, 2 * r);

            // подписи краёв
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(130, 255, 255, 255)))
            {
                g.DrawString(L.T("непрозрачный", "opaque"), fHint, dim, TrackLeft - KnobR, cy + 13);
                string right = L.T("прозрачный", "transparent");
                SizeF rs = g.MeasureString(right, fHint);
                g.DrawString(right, fHint, dim, TrackRight + KnobR - rs.Width + 3, cy + 13);
            }
        }
    }
}
