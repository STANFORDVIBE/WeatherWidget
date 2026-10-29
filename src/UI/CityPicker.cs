using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace WeatherWidget
{
    // Панель выбора города в стиле виджета: поиск по миру и список своих городов.
    // Окно с попиксельной прозрачностью (сглаженные углы, тень), поэтому поле поиска
    // нарисовано самостоятельно: текст набирается с клавиатуры, Ctrl+V вставляет,
    // стрелки выбирают строку, Enter — выбрать, Esc или клик мимо — закрыть.
    // Что искать — CitySearch, отрисовка — CityPicker.Rendering.cs.
    sealed partial class CityPicker : Form
    {
        const int Shadow = Theme.PanelShadow;  // поле вокруг панели под тень
        const int PanelW = 280, Pad = 10, SearchH = 38, RowH = 46, StatusH = 58, Radius = 14;
        const int MaxQuery = 60;

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
        float dpi = 1f;                // масштаб экрана, где открыта панель

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
            dpi = Dpi.AtPoint(new Point(centerX, cityBottom));
            Size = new Size(Dpi.Px(PanelW + 2 * Shadow, dpi), Dpi.Px(PanelHeight() + 2 * Shadow, dpi));
            Rectangle wa = Screen.FromPoint(new Point(centerX, cityBottom)).WorkingArea;
            int x = Math.Max(wa.Left, Math.Min(centerX - Width / 2, wa.Right - Width));
            int gap = Dpi.Px(Shadow - 4, dpi);
            openUp = cityBottom - gap + Height > wa.Bottom;
            anchorY = openUp ? cityTop + gap : cityBottom - gap;
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
            DisposeGraphics();
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
            status = rows.Count == 0 ? L.T("Найдите свой город —\nначните вводить название", "Find your city —\nstart typing its name") : null;
        }

        void ShowResults(List<GeoResult> found)
        {
            rows = new List<Row>();
            if (found == null) { status = L.T("Нет связи с сервером", "Can't reach the server"); return; }
            foreach (GeoResult g in found)
            {
                Row r = new Row();
                r.City = g.City;
                r.Title = g.City.Name;
                int comma = g.Label.IndexOf(", ");
                r.Subtitle = comma >= 0 ? g.Label.Substring(comma + 2) : g.City.Country;   // «регион, страна»
                if (g.Typos > 0) r.Subtitle = L.T("может, вы искали? · ", "did you mean? · ") + r.Subtitle;   // подсказка при опечатке
                r.Current = !owner.Dynamic && g.City.Same(owner.Home);
                rows.Add(r);
            }
            status = rows.Count == 0 ? L.T("Ничего не найдено", "Nothing found") : null;
            selected = rows.Count > 0 ? 0 : -1;   // Enter выберет первый результат
        }

        int PanelHeight()
        {
            return Pad + SearchH + Pad + (status != null ? StatusH : rows.Count * RowH) + Pad - 2;
        }

        // Подогнать высоту окна под содержимое (край у названия города остаётся на месте)
        void Relayout()
        {
            int h = Dpi.Px(PanelHeight() + 2 * Shadow, dpi);
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
                status = L.T("Поиск…", "Searching…");
                debounce.Start();
            }
            Relayout();
        }

        void Search()
        {
            string q = query;
            int seq = ++searchSeq;
            string uiLang = L.Code;
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<GeoResult> found = CitySearch.Find(q, uiLang, Http.Download);
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
            Point p = Dpi.ToBase(e.Location, dpi);
            int h = RowAt(p);
            bool x = h >= 0 && rows[h].Removable && XRect(RowRect(h)).Contains(p);
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
            Point p = Dpi.ToBase(e.Location, dpi);
            int i = RowAt(p);
            if (i < 0) return;
            if (rows[i].Removable && XRect(RowRect(i)).Contains(p)) Remove(rows[i]);
            else Pick(rows[i]);
        }
    }
}
