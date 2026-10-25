using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

namespace WeatherWidget
{
    // Панель выбора города в стиле виджета: поиск по миру и список своих городов.
    // Окно с попиксельной прозрачностью (сглаженные углы, тень), поэтому поле поиска
    // нарисовано самостоятельно: текст набирается с клавиатуры, Ctrl+V вставляет,
    // стрелки выбирают строку, Enter — выбрать, Esc или клик мимо — закрыть.
    sealed class CityPicker : Form
    {
        const int Shadow = 14;                 // поле вокруг панели под тень
        const int PanelW = 280, Pad = 10, SearchH = 38, RowH = 46, StatusH = 58, Radius = 14;
        const int MaxQuery = 60;
        static readonly Color Accent = Color.FromArgb(79, 195, 176);

        sealed class Row
        {
            public City City;
            public string Title, Subtitle;
            public bool Current, Removable;
        }

        readonly WeatherForm owner;
        readonly LayeredSurface surface = new LayeredSurface();
        readonly System.Windows.Forms.Timer caretTimer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer debounce = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer fadeTimer = new System.Windows.Forms.Timer();
        readonly Font fSearch = new Font("Segoe UI", 14, GraphicsUnit.Pixel);
        readonly Font fName = new Font("Segoe UI Semibold", 14, GraphicsUnit.Pixel);
        readonly Font fSub = new Font("Segoe UI", 11, GraphicsUnit.Pixel);
        readonly Font fStatus = new Font("Segoe UI", 12, GraphicsUnit.Pixel);
        readonly StringFormat ellipsis = new StringFormat
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        readonly StringFormat centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        string query = "";
        bool caretOn = true;
        List<Row> rows = new List<Row>();
        string status;                 // вместо списка: подсказка, «Поиск…», «Ничего не найдено»
        int hover = -1, selected = -1;
        bool hoverX;
        int searchSeq;                 // номер последнего поиска — устаревшие ответы игнорируются
        int fade;                      // непрозрачность при появлении, 0..255
        bool openUp;                   // панель открыта над виджетом (внизу не хватило места)
        int anchorY;                   // край, к которому «прилипает» панель при изменении высоты

        public CityPicker(WeatherForm owner)
        {
            this.owner = owner;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;

            caretTimer.Interval = 530;   // мигание курсора
            caretTimer.Tick += delegate { caretOn = !caretOn; Present(); };
            debounce.Interval = 400;     // искать, когда пользователь перестал печатать
            debounce.Tick += delegate { debounce.Stop(); Search(); };
            fadeTimer.Interval = 15;     // плавное появление
            fadeTimer.Tick += delegate
            {
                fade = Math.Min(255, fade + 40);
                if (fade >= 255) fadeTimer.Stop();
                Present();
            };

            ShowOwnCities();
            Size = new Size(PanelW + 2 * Shadow, PanelHeight() + 2 * Shadow);
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

        // Открыть под названием города (cityBottom — низ надписи, cityTop — верх, на экране)
        public void ShowNear(int centerX, int cityTop, int cityBottom)
        {
            Rectangle wa = Screen.FromPoint(new Point(centerX, cityBottom)).WorkingArea;
            int x = Math.Max(wa.Left, Math.Min(centerX - Width / 2, wa.Right - Width));
            openUp = cityBottom - Shadow + 4 + Height > wa.Bottom;
            anchorY = openUp ? cityTop + Shadow - 4 : cityBottom - Shadow + 4;
            Location = new Point(x, openUp ? anchorY - Height : anchorY);
            fade = 0;
            Show(owner);
            Activate();
            caretTimer.Start();
            fadeTimer.Start();
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
            caretTimer.Dispose();
            debounce.Dispose();
            fadeTimer.Dispose();
            surface.Dispose();
        }

        // ---------- Содержимое ----------

        void ShowOwnCities()
        {
            rows = new List<Row>();
            foreach (City c in owner.Custom.Items)
            {
                Row r = new Row();
                r.City = c;
                r.Title = c.Name;
                r.Subtitle = c.Country;
                r.Current = !owner.Dynamic && c.Same(owner.Home);
                r.Removable = !c.Same(owner.Home);   // выбранный город удалить нельзя
                rows.Add(r);
            }
            status = rows.Count == 0 ? "Найдите свой город —\nначните вводить название" : null;
        }

        void ShowResults(List<GeoResult> found)
        {
            rows = new List<Row>();
            if (found == null) { status = "Нет связи с сервером"; return; }
            foreach (GeoResult g in found)
            {
                Row r = new Row();
                r.City = g.City;
                r.Title = g.City.Name;
                int comma = g.Label.IndexOf(", ");
                r.Subtitle = comma >= 0 ? g.Label.Substring(comma + 2) : g.City.Country;   // «регион, страна»
                r.Current = !owner.Dynamic && g.City.Same(owner.Home);
                rows.Add(r);
            }
            status = rows.Count == 0 ? "Ничего не найдено" : null;
            selected = rows.Count > 0 ? 0 : -1;   // Enter выберет первый результат
        }

        int PanelHeight()
        {
            return Pad + SearchH + Pad + (status != null ? StatusH : rows.Count * RowH) + Pad - 2;
        }

        // Подогнать высоту окна под содержимое (край у названия города остаётся на месте)
        void Relayout()
        {
            int h = PanelHeight() + 2 * Shadow;
            if (h != Height)
                Bounds = new Rectangle(Left, openUp ? anchorY - h : Top, Width, h);
            Present();
        }

        void OnQueryChanged()
        {
            caretOn = true;
            caretTimer.Stop();
            caretTimer.Start();
            hover = -1;
            selected = -1;
            debounce.Stop();
            if (query.Trim().Length < 2)
            {
                searchSeq++;   // отменить ответ на прошлый запрос
                ShowOwnCities();
            }
            else
            {
                rows = new List<Row>();
                status = "Поиск…";
                debounce.Start();
            }
            Relayout();
        }

        void Search()
        {
            string q = query.Trim();
            int seq = ++searchSeq;
            string url = OpenMeteo.SearchUrl(q);
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<GeoResult> found = null;
                try { found = OpenMeteo.ParseSearch(Http.Download(url)); }
                catch { }
                try
                {
                    if (!IsDisposed && IsHandleCreated)
                        BeginInvoke((MethodInvoker)delegate
                        {
                            if (IsDisposed || seq != searchSeq) return;   // уже ввели другой запрос
                            ShowResults(found);
                            Relayout();
                        });
                }
                catch { }
            });
        }

        void Pick(Row r)
        {
            City c = r.City;
            Close();
            owner.SelectHome(c);
        }

        void Remove(Row r)
        {
            owner.Custom.Remove(r.City);
            owner.SaveSettings();
            hover = -1;
            hoverX = false;
            ShowOwnCities();
            Relayout();
        }

        // ---------- Клавиатура ----------

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Escape:
                    Close();
                    return true;
                case Keys.Enter:
                    if (status == null && rows.Count > 0) Pick(rows[selected >= 0 ? selected : 0]);
                    return true;
                case Keys.Down:
                    if (rows.Count > 0) { selected = Math.Min(rows.Count - 1, selected + 1); Present(); }
                    return true;
                case Keys.Up:
                    if (rows.Count > 0) { selected = Math.Max(0, selected - 1); Present(); }
                    return true;
                case Keys.Back:
                    if (query.Length > 0) { query = query.Substring(0, query.Length - 1); OnQueryChanged(); }
                    return true;
                case Keys.Control | Keys.Back:
                    if (query.Length > 0) { query = ""; OnQueryChanged(); }
                    return true;
                case Keys.Control | Keys.V:
                    try
                    {
                        string t = Clipboard.GetText().Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
                        query = (query + t).Substring(0, Math.Min(MaxQuery, (query + t).Length));
                        OnQueryChanged();
                    }
                    catch { }
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (char.IsControl(e.KeyChar) || query.Length >= MaxQuery) return;
            query += e.KeyChar;
            OnQueryChanged();
        }

        // ---------- Мышь ----------

        Rectangle RowRect(int i)
        {
            return new Rectangle(Shadow + 6, ListTop + i * RowH, PanelW - 12, RowH - 2);
        }

        Rectangle XRect(Rectangle row)
        {
            return new Rectangle(row.Right - 32, row.Y + (row.Height - 24) / 2, 24, 24);
        }

        int ListTop { get { return Shadow + Pad + SearchH + Pad; } }

        int RowAt(Point p)
        {
            if (status != null) return -1;
            for (int i = 0; i < rows.Count; i++) if (RowRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = RowAt(e.Location);
            bool x = h >= 0 && rows[h].Removable && XRect(RowRect(h)).Contains(e.Location);
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
            if (h != hover || x != hoverX)
            {
                hover = h;
                hoverX = x;
                if (h >= 0) selected = h;
                Present();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover != -1) { hover = -1; hoverX = false; Present(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            int i = RowAt(e.Location);
            if (i < 0) return;
            if (rows[i].Removable && XRect(RowRect(i)).Contains(e.Location)) Remove(rows[i]);
            else Pick(rows[i]);
        }

        // ---------- Отрисовка ----------

        void Present()
        {
            if (!IsHandleCreated || !Visible || IsDisposed) return;
            surface.Present(Handle, Width, Height, (byte)fade, Render);
        }

        // Картинка панели без показа окна — для тестов и README
        internal Bitmap RenderPreview(string previewQuery, List<GeoResult> results)
        {
            query = previewQuery ?? "";
            if (results != null) ShowResults(results); else ShowOwnCities();
            caretOn = true;
            Size = new Size(PanelW + 2 * Shadow, PanelHeight() + 2 * Shadow);
            Bitmap b = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b)) Render(g);
            return b;
        }

        void Render(Graphics g)
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            int panelH = Height - 2 * Shadow;
            RectangleF panel = new RectangleF(Shadow, Shadow, PanelW, panelH);

            // мягкая тень
            for (int i = Shadow; i >= 1; i -= 2)
                using (GraphicsPath p = Theme.RoundRect(new RectangleF(panel.X - i + 2, panel.Y - i + 5, panel.Width + 2 * i - 4, panel.Height + 2 * i - 4), Radius + i))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(7, 0, 0, 0)))
                    g.FillPath(b, p);

            // панель
            using (GraphicsPath p = Theme.RoundRect(panel, Radius))
            {
                using (LinearGradientBrush bg = new LinearGradientBrush(new RectangleF(panel.X, panel.Y - 1, panel.Width, panel.Height + 2),
                    Color.FromArgb(250, 26, 36, 54), Color.FromArgb(250, 34, 49, 72), 90f))
                    g.FillPath(bg, p);
                using (Pen border = new Pen(Color.FromArgb(34, 255, 255, 255)))
                    g.DrawPath(border, p);
            }

            DrawSearch(g);

            float sepY = ListTop - Pad / 2f - 1;
            using (Pen sep = new Pen(Color.FromArgb(30, 255, 255, 255)))
                g.DrawLine(sep, Shadow + Pad, sepY, Shadow + PanelW - Pad, sepY);

            if (status != null)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(170, 255, 255, 255)))
                    g.DrawString(status, fStatus, b, new RectangleF(Shadow, ListTop, PanelW, StatusH - 6), centered);
                return;
            }
            for (int i = 0; i < rows.Count; i++) DrawRow(g, i);
        }

        void DrawSearch(Graphics g)
        {
            Rectangle box = new Rectangle(Shadow + Pad, Shadow + Pad, PanelW - 2 * Pad, SearchH);
            using (GraphicsPath p = Theme.RoundRect(box, 10))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(26, 255, 255, 255)))
                g.FillPath(b, p);

            // лупа
            float cx = box.X + 15, cy = box.Y + box.Height / 2f - 1;
            using (Pen lens = new Pen(Color.FromArgb(170, 255, 255, 255), 1.6f))
            {
                lens.StartCap = LineCap.Round;
                lens.EndCap = LineCap.Round;
                g.DrawEllipse(lens, cx - 5.5f, cy - 5.5f, 11, 11);
                g.DrawLine(lens, cx + 4, cy + 4, cx + 8, cy + 8);
            }

            float tx = box.X + 30, avail = box.Width - 40;
            float ty = box.Y + (box.Height - fSearch.GetHeight(g)) / 2;
            StringFormat typo = StringFormat.GenericTypographic;
            if (query == "")
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 255, 255, 255)))
                    g.DrawString("Поиск города", fSearch, b, tx, ty, typo);
            }
            GraphicsState st = g.Save();
            g.SetClip(new RectangleF(tx - 1, box.Y, avail + 2, box.Height));
            float tw = query == "" ? 0 : g.MeasureString(query, fSearch, PointF.Empty, typo).Width;
            float shift = Math.Max(0, tw - avail);   // длинный текст сдвигается влево, курсор всегда виден
            if (query != "") g.DrawString(query, fSearch, Brushes.White, tx - shift, ty, typo);
            if (caretOn && ContainsFocus || caretOn && !Visible)
                using (Pen caret = new Pen(Color.FromArgb(220, 255, 255, 255), 1.4f))
                    g.DrawLine(caret, tx + tw - shift + 1, box.Y + 10, tx + tw - shift + 1, box.Bottom - 10);
            g.Restore(st);
        }

        void DrawRow(Graphics g, int i)
        {
            Row r = rows[i];
            Rectangle rc = RowRect(i);
            if (i == hover || i == selected)
                using (GraphicsPath p = Theme.RoundRect(rc, 9))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(i == hover ? 26 : 18, 255, 255, 255)))
                    g.FillPath(b, p);

            if (r.Current)
                using (SolidBrush dot = new SolidBrush(Accent))
                    g.FillEllipse(dot, rc.X + 10, rc.Y + rc.Height / 2f - 4, 8, 8);

            float textX = rc.X + 26, textW = rc.Width - 26 - (r.Removable ? 34 : 8);
            g.DrawString(r.Title, fName, Brushes.White, new RectangleF(textX, rc.Y + 5, textW, 20), ellipsis);
            if (!string.IsNullOrEmpty(r.Subtitle))
                using (SolidBrush sub = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
                    g.DrawString(r.Subtitle, fSub, sub, new RectangleF(textX, rc.Y + 24, textW, 16), ellipsis);

            if (r.Removable && i == hover)
            {
                Rectangle x = XRect(rc);
                using (SolidBrush b = new SolidBrush(hoverX ? Color.FromArgb(200, 214, 72, 72) : Color.FromArgb(28, 255, 255, 255)))
                    g.FillEllipse(b, x);
                using (Pen p = new Pen(Color.FromArgb(230, 255, 255, 255), 1.6f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    const int m = 8;
                    g.DrawLine(p, x.Left + m, x.Top + m, x.Right - m, x.Bottom - m);
                    g.DrawLine(p, x.Right - m, x.Top + m, x.Left + m, x.Bottom - m);
                }
            }
        }
    }
}
