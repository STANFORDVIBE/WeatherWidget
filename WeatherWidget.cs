using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

class City
{
    public string Name;
    public string Country;
    public double Lat;
    public double Lon;
    public City(string name, string country, double lat, double lon) { Name = name; Country = country; Lat = lat; Lon = lon; }

    public bool Same(City o)
    {
        return o != null && o.Name == Name && Math.Abs(o.Lat - Lat) < 0.01 && Math.Abs(o.Lon - Lon) < 0.01;
    }
}

enum Sky { Sunny, PartlyCloudy, Cloudy, Rain, Snow }

class DayForecast
{
    public string Label;
    public Sky Sky;
    public double Max, Min;
}

class WeatherData
{
    public City City;
    public double Temp, Feels, Wind;
    public int Humidity, Code;
    public bool IsDay;
    public Sky Sky;
    public int UtcOffset;               // смещение часового пояса города, секунды
    public List<DayForecast> Days = new List<DayForecast>();
}

// Пункт «своего» города в меню: при наведении справа появляется крестик для удаления.
class CustomCityItem : ToolStripMenuItem
{
    public event EventHandler RemoveClicked;
    public bool Removable = true;
    bool overX;

    // Пункт, у которого курсор сейчас над крестиком. Меню закрывается раньше, чем
    // срабатывает клик, поэтому по этому полю закрытие отменяется заранее.
    public static CustomCityItem Armed;

    void SetOverX(bool o)
    {
        if (o == overX) return;
        overX = o;
        if (o) Armed = this; else if (Armed == this) Armed = null;
        Invalidate();
    }

    public CustomCityItem(string text) : base(text)
    {
        ShortcutKeyDisplayString = "      ";   // запас места справа под крестик
    }

    Rectangle XRect { get { return new Rectangle(Width - 28, (Height - 18) / 2, 18, 18); } }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetOverX(Removable && XRect.Contains(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetOverX(false);
    }

    protected override void OnClick(EventArgs e)
    {
        if (overX)   // клик по крестику — удалить, а не выбрать город
        {
            SetOverX(false);
            if (RemoveClicked != null) RemoveClicked(this, EventArgs.Empty);
            return;
        }
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!Removable || !Selected) return;   // крестик виден только у строки под курсором
        Rectangle r = XRect;
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (overX)
            using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                g.FillEllipse(b, r);
        using (Pen p = new Pen(overX ? Color.FromArgb(200, 40, 40) : Color.FromArgb(110, 110, 110), 1.6f))
        {
            const int m = 5;
            g.DrawLine(p, r.Left + m, r.Top + m, r.Right - m, r.Bottom - m);
            g.DrawLine(p, r.Right - m, r.Top + m, r.Left + m, r.Bottom - m);
        }
    }
}

// Всплывающая подсказка в стиле системной. Своя, потому что стандартный ToolTip
// WinForms не показывается, пока окно виджета не активно (а виджет обычно не активен).
class TipWindow : Form
{
    const int Pad = 8;
    string title = "", body = "";
    Font fTitle = new Font("Segoe UI", 9f, FontStyle.Bold);
    Font fBody = new Font("Segoe UI", 9f);

    public TipWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = SystemColors.Info;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override bool ShowWithoutActivation { get { return true; } }   // не забирать фокус

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80 | 0x8 | 0x08000000;   // WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE
            cp.ClassStyle |= 0x20000;                // CS_DROPSHADOW — тень, как у системной подсказки
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        // подсказка «прозрачна» для мыши — курсор над ней не уводит мышь с виджета
        if (m.Msg == 0x84) { m.Result = (IntPtr)(-1); return; }   // WM_NCHITTEST -> HTTRANSPARENT
        base.WndProc(ref m);
    }

    // показать подсказку у точки экрана (обычно чуть ниже и правее курсора)
    public void ShowAt(string t, string b, Point at)
    {
        title = t;
        body = b;
        Size ts = title == "" ? Size.Empty : TextRenderer.MeasureText(title, fTitle);
        Size bs = TextRenderer.MeasureText(body, fBody);
        int w = Math.Max(ts.Width, bs.Width) + Pad * 2;
        int h = bs.Height + Pad * 2 + (title == "" ? 0 : ts.Height + 4);

        // не вылезать за край экрана
        Rectangle wa = Screen.FromPoint(at).WorkingArea;
        int x = Math.Max(wa.Left, Math.Min(at.X, wa.Right - w));
        int y = at.Y + h <= wa.Bottom ? at.Y : at.Y - h - 30;
        y = Math.Max(wa.Top, y);
        Bounds = new Rectangle(x, y, w, h);
        Invalidate();
        if (!Visible) Show();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        using (Pen p = new Pen(SystemColors.ControlDark))
            g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        int y = Pad;
        if (title != "")
        {
            TextRenderer.DrawText(g, title, fTitle, new Point(Pad, y), SystemColors.InfoText);
            y += TextRenderer.MeasureText(title, fTitle).Height + 4;
        }
        TextRenderer.DrawText(g, body, fBody, new Point(Pad, y), SystemColors.InfoText);
    }
}

class WeatherForm : Form
{
    // Избранные города России (в меню выбора)
    static readonly City[] Favorites = new City[]
    {
        new City("Москва", "Россия", 55.7558, 37.6173),
        new City("Санкт-Петербург", "Россия", 59.9343, 30.3351),
        new City("Омск", "Россия", 54.9885, 73.3242),
        new City("Ростов-на-Дону", "Россия", 47.2357, 39.7015),
        new City("Челябинск", "Россия", 55.1644, 61.4368),
        new City("Ханты-Мансийск", "Россия", 61.0042, 69.0019)
    };

    // Города для режима «Динамическая погода»
    static readonly City[] World = new City[]
    {
        new City("Лос-Анджелес", "США", 34.0522, -118.2437),
        new City("Нью-Йорк", "США", 40.7128, -74.0060),
        new City("Лондон", "Великобритания", 51.5074, -0.1278),
        new City("Берлин", "Германия", 52.5200, 13.4050),
        new City("Париж", "Франция", 48.8566, 2.3522),
        new City("Рим", "Италия", 41.9028, 12.4964),
        new City("Мадрид", "Испания", 40.4168, -3.7038),
        new City("Амстердам", "Нидерланды", 52.3676, 4.9041),
        new City("Прага", "Чехия", 50.0755, 14.4378),
        new City("Стамбул", "Турция", 41.0082, 28.9784),
        new City("Дубай", "ОАЭ", 25.2048, 55.2708),
        new City("Каир", "Египет", 30.0444, 31.2357),
        new City("Токио", "Япония", 35.6762, 139.6503),
        new City("Сеул", "Южная Корея", 37.5665, 126.9780),
        new City("Пекин", "Китай", 39.9042, 116.4074),
        new City("Бангкок", "Таиланд", 13.7563, 100.5018),
        new City("Сингапур", "Сингапур", 1.3521, 103.8198),
        new City("Сидней", "Австралия", -33.8688, 151.2093),
        new City("Торонто", "Канада", 43.6532, -79.3832),
        new City("Рио-де-Жанейро", "Бразилия", -22.9068, -43.1729),
        new City("Вена", "Австрия", 48.2082, 16.3738),
        new City("Барселона", "Испания", 41.3851, 2.1734),
        new City("Лиссабон", "Португалия", 38.7223, -9.1393),
        new City("Афины", "Греция", 37.9838, 23.7275),
        new City("Стокгольм", "Швеция", 59.3293, 18.0686),
        new City("Варшава", "Польша", 52.2297, 21.0122),
        new City("Минск", "Беларусь", 53.9006, 27.5590),
        new City("Астана", "Казахстан", 51.1694, 71.4491),
        new City("Ереван", "Армения", 40.1792, 44.4991),
        new City("Тбилиси", "Грузия", 41.7151, 44.8271),
        new City("Чикаго", "США", 41.8781, -87.6298),
        new City("Майами", "США", 25.7617, -80.1918),
        new City("Мехико", "Мексика", 19.4326, -99.1332),
        new City("Мумбаи", "Индия", 19.0760, 72.8777),
        new City("Ханой", "Вьетнам", 21.0278, 105.8342)
    };

    const int W = 240, H0 = 256, H1 = 356;    // базовый размер (масштаб 100%): свёрнутый / с прогнозом
    const float MinScale = 0.6f, MaxScale = 2.5f;
    const int WM_NCHITTEST = 0x84, WM_SIZING = 0x214, EM_SETCUEBANNER = 0x1501;
    const int HTCLIENT = 1, HTTOPLEFT = 13, HTTOPRIGHT = 14, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string AppName = "WeatherWidget";

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    enum TipKind { None, City, Temp }

    float scale = 1f;
    City home = Favorites[0];       // город, выбранный пользователем
    City target;                    // город, который сейчас загружается/показывается
    WeatherData data;
    string error = null;
    bool dynamic = false;
    List<City> dynOrder = new List<City>();   // текущий круг «динамической погоды» в случайном порядке
    int dynIndex = 0;
    Random rnd = new Random();

    // Следующий город для «динамической погоды»: каждый круг — новый случайный порядок,
    // все города по одному разу, и два города одной страны не идут подряд.
    City NextDynamicCity()
    {
        dynIndex++;
        if (dynIndex >= dynOrder.Count)
        {
            City last = dynOrder.Count > 0 ? dynOrder[dynOrder.Count - 1] : null;
            dynOrder = ShuffleWorld(last);
            dynIndex = 0;
        }
        return dynOrder[dynIndex];
    }

    List<City> ShuffleWorld(City previous)
    {
        // в редких случаях в конце круга развести соседей не удаётся — тогда перемешиваем заново
        List<City> list = null;
        for (int attempt = 0; attempt < 50; attempt++)
        {
            list = ShuffleOnce(previous);
            if (!HasSameCountryNeighbours(list, previous)) break;
        }
        return list;
    }

    static bool HasSameCountryNeighbours(List<City> list, City previous)
    {
        for (int i = 0; i < list.Count; i++)
        {
            City prev = i > 0 ? list[i - 1] : previous;
            if (prev != null && prev.Country == list[i].Country) return true;
        }
        return false;
    }

    List<City> ShuffleOnce(City previous)
    {
        List<City> list = new List<City>(World);
        for (int i = list.Count - 1; i > 0; i--)   // перемешивание Фишера–Йетса
        {
            int j = rnd.Next(i + 1);
            City t = list[i]; list[i] = list[j]; list[j] = t;
        }
        // развести соседей из одной страны (и не повторить страну на стыке кругов)
        for (int i = 0; i < list.Count; i++)
        {
            City prev = i > 0 ? list[i - 1] : previous;
            if (prev == null || list[i].Country != prev.Country) continue;
            for (int j = i + 1; j < list.Count; j++)
            {
                if (list[j].Country != prev.Country)
                {
                    City t = list[i]; list[i] = list[j]; list[j] = t;
                    break;
                }
            }
        }
        return list;
    }

    // Все анимации считаются от реального времени, а не от числа тиков таймера —
    // поэтому идут ровно, даже если таймер срабатывает неравномерно.
    const int TransMs = 600, ExpMs = 260;
    Stopwatch clock = Stopwatch.StartNew();
    float phase = 0;
    WeatherData prevData;           // город, который гаснет при плавной смене
    float trans = 1;                // прогресс смены города 0..1 (1 — смена завершена)
    double transStart;
    float exp = 0, expTarget = 0;   // раскрытие прогноза 0..1
    float expFrom = 0;
    double expStart;
    bool btnHover = false;

    // шрифты и форматы создаются один раз, а не в каждом кадре
    Font fCity = new Font("Segoe UI Semibold", 17, GraphicsUnit.Pixel);
    Font fTemp = new Font("Segoe UI Light", 52, GraphicsUnit.Pixel);
    Font fDesc = new Font("Segoe UI", 15, GraphicsUnit.Pixel);
    Font fSmall = new Font("Segoe UI", 11, GraphicsUnit.Pixel);
    Font fDay = new Font("Segoe UI", 12, GraphicsUnit.Pixel);
    Font fDayTemp = new Font("Segoe UI Semibold", 13, GraphicsUnit.Pixel);
    StringFormat center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
    StringFormat cityFmt = new StringFormat
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center,
        Trimming = StringTrimming.EllipsisCharacter,
        FormatFlags = StringFormatFlags.NoWrap
    };
    Rectangle cityRect = new Rectangle(16, 8, 208, 26);
    Rectangle tempRect = new Rectangle(40, 138, 160, 56);
    Rectangle btnRect = Rectangle.Empty;   // кнопка прогноза; положение считается при отрисовке (справа от времени)

    bool BtnHit(Point bp) { Rectangle r = btnRect; r.Inflate(5, 5); return data != null && r.Contains(bp); }

    float BaseH { get { return H0 + (H1 - H0) * exp; } }

    System.Windows.Forms.Timer animTimer = new System.Windows.Forms.Timer();
    System.Windows.Forms.Timer refreshTimer = new System.Windows.Forms.Timer();
    System.Windows.Forms.Timer dynTimer = new System.Windows.Forms.Timer();
    TipWindow tip = new TipWindow();
    TipKind tipShown = TipKind.None;
    ContextMenuStrip menu = new ContextMenuStrip();
    ContextMenuStrip cityPopup = new ContextMenuStrip();   // список городов по клику на название
    ToolStripMenuItem cityMenu = new ToolStripMenuItem("Город");
    ToolStripMenuItem dynItem = new ToolStripMenuItem("Динамическая погода");
    ToolStripMenuItem topMostItem = new ToolStripMenuItem("Поверх всех окон");
    ToolStripMenuItem autoStartItem = new ToolStripMenuItem("Запускать вместе с Windows");
    List<CityMenu> cityMenus = new List<CityMenu>();

    string settingsPath;

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

    public WeatherForm()
    {
        Text = "Погода";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        // Окно всегда высотой с раскрытую карточку; место ниже свёрнутой карточки
        // полностью прозрачное (и не перехватывает клики).
        // Так при раскрытии прогноза окно не меняет размер — нет рывков и мерцания.
        ClientSize = new Size(W, H1);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName, "settings.txt");
        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(wa.Right - W - 30, wa.Top + 30);
        LoadSettings();
        UpdateShape();

        cityMenus.Add(new CityMenu(this, cityMenu.DropDown));
        cityMenus.Add(new CityMenu(this, cityPopup));
        KeepOpenOnRemove(menu);
        KeepOpenOnRemove(cityMenu.DropDown);
        KeepOpenOnRemove(cityPopup);
        menu.Items.Add(cityMenu);
        dynItem.Checked = dynamic;
        dynItem.Click += delegate { SetDynamic(!dynamic); };
        menu.Items.Add(dynItem);
        menu.Items.Add("Обновить", null, delegate { Fetch(target); });
        menu.Items.Add(new ToolStripSeparator());
        topMostItem.Checked = TopMost;
        topMostItem.Click += delegate { TopMost = !TopMost; topMostItem.Checked = TopMost; SaveSettings(); };
        menu.Items.Add(topMostItem);
        autoStartItem.Checked = IsAutoStart();
        autoStartItem.Click += delegate { SetAutoStart(!autoStartItem.Checked); autoStartItem.Checked = IsAutoStart(); };
        menu.Items.Add(autoStartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, delegate { Close(); });
        ContextMenuStrip = menu;


        animTimer.Interval = 40;
        animTimer.Tick += delegate
        {
            double now = clock.Elapsed.TotalMilliseconds;
            phase = (float)(now / 40);   // фоновые анимации: прежняя скорость, 1 шаг = 40 мс
            if (trans < 1) trans = (float)Math.Min(1, (now - transStart) / TransMs);
            if (exp != expTarget)
            {
                float t = (float)Math.Min(1, (now - expStart) / ExpMs);
                exp = t >= 1 ? expTarget : expFrom + (expTarget - expFrom) * Ease(t);
            }
            // во время переходов — ~60 кадров/с для плавности, в покое — 25 кадров/с, чтобы не грузить систему
            int want = trans < 1 || exp != expTarget ? 15 : 40;
            if (animTimer.Interval != want) animTimer.Interval = want;
            Present();
        };
        animTimer.Start();

        refreshTimer.Interval = 15 * 60 * 1000;
        refreshTimer.Tick += delegate { Fetch(target); };
        refreshTimer.Start();

        dynTimer.Interval = 45 * 1000;
        dynTimer.Tick += delegate { Fetch(NextDynamicCity()); };

        FormClosing += delegate { SaveSettings(); };
        Load += delegate
        {
            if (dynamic) { dynIndex = int.MaxValue - 1; dynTimer.Start(); Fetch(NextDynamicCity()); }
            else Fetch(home);
        };
    }

    // ---------- Выбор города ----------

    // Свои города: найденные через поиск запоминаются в меню под стандартными (до MaxCustom, старые вытесняются)
    const int MaxCustom = 10;
    List<City> custom = new List<City>();
    // клик по крестику не должен закрывать меню
    static void KeepOpenOnRemove(ToolStripDropDown d)
    {
        d.Closing += (s, e) =>
        {
            if (CustomCityItem.Armed != null && e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true;
        };
    }

    static bool IsStandard(City c)
    {
        foreach (City f in Favorites) if (f.Same(c)) return true;
        return false;
    }

    void RememberCity(City c)
    {
        if (IsStandard(c)) return;
        for (int i = custom.Count - 1; i >= 0; i--) if (custom[i].Same(c)) custom.RemoveAt(i);
        custom.Add(c);
        while (custom.Count > MaxCustom) custom.RemoveAt(0);
    }

    void SelectHome(City c)
    {
        RememberCity(c);
        home = c;
        if (dynamic) { dynamic = false; dynItem.Checked = false; dynTimer.Stop(); }
        SaveSettings();
        Fetch(home);
    }

    void SetDynamic(bool on)
    {
        dynamic = on;
        dynItem.Checked = on;
        if (on) { dynIndex = int.MaxValue - 1; dynTimer.Stop(); dynTimer.Start(); Fetch(NextDynamicCity()); }
        else { dynTimer.Stop(); Fetch(home); }
        SaveSettings();
    }

    void CloseMenus()
    {
        menu.Close();
        cityPopup.Close();
    }

    // Меню выбора города: поиск по миру + избранные города России
    class CityMenu
    {
        WeatherForm form;
        ToolStripDropDown dd;
        ToolStripTextBox box = new ToolStripTextBox();
        List<ToolStripItem> results = new List<ToolStripItem>();
        List<ToolStripMenuItem> favItems = new List<ToolStripMenuItem>();
        List<ToolStripItem> customItems = new List<ToolStripItem>();
        System.Windows.Forms.Timer debounce = new System.Windows.Forms.Timer();

        public CityMenu(WeatherForm f, ToolStripDropDown d)
        {
            form = f;
            dd = d;
            box.AutoSize = false;
            box.Width = 220;
            box.TextBox.HandleCreated += delegate { SetCue(); };
            box.TextChanged += delegate { debounce.Stop(); debounce.Start(); };
            box.KeyDown += OnKey;
            debounce.Interval = 450;
            debounce.Tick += delegate { debounce.Stop(); Search(); };

            dd.Items.Add(box);
            dd.Items.Add(new ToolStripSeparator());
            foreach (City c in Favorites)
            {
                ToolStripMenuItem it = new ToolStripMenuItem(c.Name);
                it.Tag = c;
                it.Click += OnPick;
                favItems.Add(it);
                dd.Items.Add(it);
            }
            dd.Opening += delegate { RebuildCustom(); };
            dd.Opened += delegate
            {
                box.Text = "";
                ClearResults();
                foreach (ToolStripMenuItem it in favItems) it.Checked = !form.dynamic && ((City)it.Tag).Same(form.home);
                box.Focus();
            };
        }

        // свои города — отдельным блоком под стандартными
        void RebuildCustom()
        {
            foreach (ToolStripItem it in customItems) { dd.Items.Remove(it); it.Dispose(); }
            customItems.Clear();
            if (form.custom.Count == 0) return;
            ToolStripSeparator sep = new ToolStripSeparator();
            customItems.Add(sep);
            dd.Items.Add(sep);
            foreach (City c in form.custom)
            {
                string label = c.Country != "" && c.Country != "Россия" ? c.Name + ", " + c.Country : c.Name;
                CustomCityItem it = new CustomCityItem(label);
                it.Tag = c;
                it.Click += OnPick;
                it.RemoveClicked += OnRemove;
                it.Checked = !form.dynamic && c.Same(form.home);
                it.Removable = !c.Same(form.home);   // текущий выбранный город удалить нельзя
                customItems.Add(it);
                dd.Items.Add(it);
            }
        }

        void OnRemove(object sender, EventArgs e)
        {
            City c = (City)((ToolStripItem)sender).Tag;
            form.BeginInvoke((MethodInvoker)delegate   // после завершения клика: убрать город и перестроить список
            {
                form.custom.RemoveAll(x => x.Same(c));
                form.SaveSettings();
                RebuildCustom();
            });
        }

        void SetCue()
        {
            SendMessage(box.TextBox.Handle, EM_SETCUEBANNER, (IntPtr)1, "Поиск города");
        }

        void OnPick(object sender, EventArgs e)
        {
            form.SelectHome((City)((ToolStripItem)sender).Tag);
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            if (results.Count > 0 && results[0].Tag is City)
            {
                City c = (City)results[0].Tag;
                form.CloseMenus();
                form.SelectHome(c);
            }
        }

        void ClearResults()
        {
            foreach (ToolStripItem r in results) { dd.Items.Remove(r); r.Dispose(); }
            results.Clear();
        }

        void SetResults(List<ToolStripItem> items)
        {
            dd.SuspendLayout();
            ClearResults();
            for (int i = 0; i < items.Count; i++)
            {
                dd.Items.Insert(1 + i, items[i]);   // сразу под строкой поиска
                results.Add(items[i]);
            }
            dd.ResumeLayout();
        }

        static ToolStripItem Status(string text)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(text);
            it.Enabled = false;
            return it;
        }

        void Search()
        {
            string q = box.Text.Trim();
            if (q.Length < 2) { ClearResults(); return; }
            SetResults(new List<ToolStripItem> { Status("Поиск…") });

            string url = "https://geocoding-api.open-meteo.com/v1/search?count=8&language=ru&format=json&name=" + Uri.EscapeDataString(q);
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<City> found = new List<City>();
                List<string> labels = new List<string>();
                bool failed = false;
                try
                {
                    Dictionary<string, object> root = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(Download(url));
                    if (root.ContainsKey("results"))
                    {
                        foreach (object o in (object[])root["results"])
                        {
                            Dictionary<string, object> r = (Dictionary<string, object>)o;
                            string name = Str(r, "name"), country = Str(r, "country"), region = Str(r, "admin1");
                            if (name == "" || !r.ContainsKey("latitude") || !r.ContainsKey("longitude")) continue;
                            found.Add(new City(name, country, Num(r["latitude"]), Num(r["longitude"])));
                            string label = name;
                            if (region != "" && region != name) label += ", " + region;
                            if (country != "") label += ", " + country;
                            labels.Add(label);
                        }
                    }
                }
                catch { failed = true; }

                form.SafeInvoke(delegate
                {
                    if (box.Text.Trim() != q) return;   // пользователь уже ввёл другой запрос
                    List<ToolStripItem> items = new List<ToolStripItem>();
                    if (failed) items.Add(Status("Нет связи с сервером"));
                    else if (found.Count == 0) items.Add(Status("Ничего не найдено"));
                    for (int i = 0; i < found.Count; i++)
                    {
                        ToolStripMenuItem it = new ToolStripMenuItem(labels[i]);
                        it.Tag = found[i];
                        it.Click += OnPick;
                        items.Add(it);
                    }
                    SetResults(items);
                });
            });
        }
    }

    // ---------- Данные ----------

    static string Download(string url)
    {
        using (WebClient wc = new WebClient())
        {
            wc.Encoding = Encoding.UTF8;
            return wc.DownloadString(url);
        }
    }

    static string Str(Dictionary<string, object> d, string key)
    {
        object v;
        return d.TryGetValue(key, out v) && v != null ? v.ToString() : "";
    }

    void Fetch(City c)
    {
        target = c;
        string url = "https://api.open-meteo.com/v1/forecast?latitude=" + c.Lat.ToString(CultureInfo.InvariantCulture) +
            "&longitude=" + c.Lon.ToString(CultureInfo.InvariantCulture) +
            "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,is_day" +
            "&daily=weather_code,temperature_2m_max,temperature_2m_min&forecast_days=4" +
            "&wind_speed_unit=ms&timezone=auto";

        ThreadPool.QueueUserWorkItem(delegate
        {
            WeatherData d = null;
            for (int attempt = 0; attempt < 3 && d == null; attempt++)   // сеть иногда сбоит — пробуем ещё раз
            {
                try { d = ParseWeather(c, Download(url)); }
                catch { Thread.Sleep(1500); }
            }
            SafeInvoke(delegate
            {
                if (c != target) return;
                if (d != null)
                {
                    if (data != null && !data.City.Same(d.City))   // плавная смена города
                    {
                        prevData = data;
                        trans = 0;
                        transStart = clock.Elapsed.TotalMilliseconds;
                    }
                    data = d;
                    error = null;
                }
                else if (!dynamic) error = "Нет связи с сервером";   // в динамике просто ждём следующий город
                Invalidate();
            });
        });
    }

    static WeatherData ParseWeather(City c, string json)
    {
        Dictionary<string, object> root = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
        Dictionary<string, object> cur = (Dictionary<string, object>)root["current"];
        WeatherData d = new WeatherData();
        d.City = c;
        d.Temp = Num(cur["temperature_2m"]);
        d.Feels = Num(cur["apparent_temperature"]);
        d.Wind = Num(cur["wind_speed_10m"]);
        d.Humidity = (int)Num(cur["relative_humidity_2m"]);
        d.Code = (int)Num(cur["weather_code"]);
        d.IsDay = Num(cur["is_day"]) != 0;
        d.Sky = SkyFromCode(d.Code);
        d.UtcOffset = root.ContainsKey("utc_offset_seconds") ? (int)Num(root["utc_offset_seconds"]) : 0;

        try
        {
            Dictionary<string, object> daily = (Dictionary<string, object>)root["daily"];
            object[] time = (object[])daily["time"];
            object[] codes = (object[])daily["weather_code"];
            object[] max = (object[])daily["temperature_2m_max"];
            object[] min = (object[])daily["temperature_2m_min"];
            CultureInfo ru = new CultureInfo("ru-RU");
            for (int i = 1; i < time.Length && d.Days.Count < 3; i++)   // с завтрашнего дня
            {
                if (codes[i] == null || max[i] == null || min[i] == null) continue;
                DateTime dt = DateTime.ParseExact((string)time[i], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                DayForecast f = new DayForecast();
                f.Label = ru.DateTimeFormat.GetAbbreviatedDayName(dt.DayOfWeek) + ", " + dt.Day;
                f.Sky = SkyFromCode((int)Num(codes[i]));
                f.Max = Num(max[i]);
                f.Min = Num(min[i]);
                d.Days.Add(f);
            }
        }
        catch { }   // без прогноза виджет всё равно показывает текущую погоду
        return d;
    }

    void SafeInvoke(MethodInvoker m)
    {
        try { if (IsHandleCreated && !IsDisposed) BeginInvoke(m); } catch { }
    }

    static double Num(object o) { return Convert.ToDouble(o, CultureInfo.InvariantCulture); }

    static Sky SkyFromCode(int c)
    {
        if (c <= 1) return Sky.Sunny;
        if (c == 2) return Sky.PartlyCloudy;
        if (c == 3 || c == 45 || c == 48) return Sky.Cloudy;
        if ((c >= 71 && c <= 77) || c == 85 || c == 86) return Sky.Snow;
        return Sky.Rain;
    }

    static string Describe(int c)
    {
        if (c == 0) return "Ясно";
        if (c == 1) return "Преимущественно ясно";
        if (c == 2) return "Переменная облачность";
        if (c == 3) return "Пасмурно";
        if (c == 45 || c == 48) return "Туман";
        if (c >= 51 && c <= 57) return "Морось";
        if (c >= 61 && c <= 67) return "Дождь";
        if (c >= 71 && c <= 77) return "Снег";
        if (c >= 80 && c <= 82) return "Ливень";
        if (c == 85 || c == 86) return "Снегопад";
        if (c >= 95) return "Гроза";
        return "";
    }

    static string Advice(WeatherData d)
    {
        string s;
        if (d.Feels >= 25) s = "Жарко — футболка, шорты и головной убор.";
        else if (d.Feels >= 18) s = "Тепло — можно в футболке.";
        else if (d.Feels >= 12) s = "Прохладно — худи или лёгкая кофта.";
        else if (d.Feels >= 5) s = "Свежо — ветровка или лёгкая куртка.";
        else if (d.Feels >= -5) s = "Холодно — тёплая куртка и шапка.";
        else if (d.Feels >= -15) s = "Мороз — зимняя куртка, шапка, шарф, перчатки.";
        else s = "Сильный мороз — пуховик, термобельё, закройте лицо.";
        if (d.Sky == Sky.Rain) s += "\nВозьмите зонт или дождевик.";
        if (d.Sky == Sky.Snow) s += "\nНепромокаемая обувь не помешает.";
        if (d.Wind >= 8) s += "\nСильный ветер — одевайтесь теплее.";
        return s;
    }

    static string TipText(WeatherData d)
    {
        return "Ощущается как " + Deg(d.Feels) + "C\n" +
            "Ветер " + d.Wind.ToString("0.#", CultureInfo.InvariantCulture) + " м/с — " + WindFeel(d.Wind) + "\n" +
            "Влажность " + d.Humidity + "% — " + HumidityFeel(d.Humidity, d.Feels) + "\n\n" +
            Advice(d);
    }

    // ветер простыми словами (по шкале Бофорта)
    static string WindFeel(double w)
    {
        if (w < 0.5) return "штиль, ветра нет";
        if (w < 2) return "лёгкий ветерок, почти не чувствуется";
        if (w < 4) return "слабый ветер, шевелит листья";
        if (w < 6) return "умеренный, развевает волосы и одежду";
        if (w < 9) return "сильный, на улице заметно холоднее";
        if (w < 12) return "очень сильный, трудно идти против ветра";
        return "штормовой, лучше переждать дома";
    }

    // влажность простыми словами: в тепле влажность ощущается как духота, в холоде — как промозглость
    static string HumidityFeel(int h, double feels)
    {
        if (h < 30) return "сухой воздух, сушит кожу и губы";
        if (h < 60) return "комфортно";
        if (h < 80)
        {
            if (feels >= 22) return "влажно, немного душно";
            if (feels <= 5) return "сыровато, холод чувствуется сильнее";
            return "влажновато, но терпимо";
        }
        if (feels >= 22) return "очень влажно, душно и липко";
        if (feels <= 5) return "сыро и промозгло, холод пробирает";
        return "очень влажно, сыро";
    }

    static string Deg(double v)
    {
        int r = (int)Math.Round(v);
        return (r > 0 ? "+" : "") + r + "°";
    }

    // ---------- Масштаб ----------

    void UpdateShape()
    {
        scale = ClientSize.Width / (float)W;
        Present();
    }

    static float Ease(float t) { return t * t * (3 - 2 * t); }

    bool fixingSize = false;

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        // Высота окна всегда должна соответствовать раскрытой карточке (ширина × H1/W),
        // иначе раскрытый прогноз обрежется. Поправляем, если размер пришёл другим.
        int want = (int)Math.Round(ClientSize.Width * (double)H1 / W);
        if (!fixingSize && ClientSize.Width > 0 && ClientSize.Height != want)
        {
            fixingSize = true;
            ClientSize = new Size(ClientSize.Width, want);
            fixingSize = false;
        }
        UpdateShape();
    }

    protected override void OnResizeEnd(EventArgs e)
    {
        base.OnResizeEnd(e);
        SaveSettings();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            // углы окна работают как ручки изменения размера (Windows сама покажет курсор ↘↙)
            base.WndProc(ref m);
            if (m.Result.ToInt32() == HTCLIENT)
            {
                long lp = m.LParam.ToInt64();
                Point p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
                int z = Math.Max(14, (int)(18 * scale));
                int cardBottom = (int)(BaseH * scale);   // низ карточки (окно ниже прозрачное)
                bool l = p.X < z, r = p.X >= ClientSize.Width - z;
                bool t = p.Y < z, b = p.Y >= cardBottom - z && p.Y < cardBottom;
                if (t && l) m.Result = (IntPtr)HTTOPLEFT;
                else if (t && r) m.Result = (IntPtr)HTTOPRIGHT;
                else if (b && l) m.Result = (IntPtr)HTBOTTOMLEFT;
                else if (b && r) m.Result = (IntPtr)HTBOTTOMRIGHT;
            }
            return;
        }
        if (m.Msg == WM_SIZING)
        {
            // сохраняем пропорции и ограничиваем масштаб
            RECT rc = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
            int edge = m.WParam.ToInt32();
            int w = rc.Right - rc.Left;
            w = Math.Max((int)(W * MinScale), Math.Min((int)(W * MaxScale), w));
            int h = (int)Math.Round(w * (double)H1 / W);
            if (edge == 1 || edge == 4 || edge == 7) rc.Left = rc.Right - w; else rc.Right = rc.Left + w;
            if (edge == 3 || edge == 4 || edge == 5) rc.Top = rc.Bottom - h; else rc.Bottom = rc.Top + h;
            Marshal.StructureToPtr(rc, m.LParam, false);
            m.Result = (IntPtr)1;
            return;
        }
        base.WndProc(ref m);
    }

    // ---------- Мышь ----------

    // координаты мыши -> координаты базового макета (ширина 240)
    Point ToBase(Point p) { return new Point((int)(p.X / scale), (int)(p.Y / scale)); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (ToBase(e.Location).Y > BaseH) return;   // ниже карточки — пусто
        if (BtnHit(ToBase(e.Location)))
        {
            HideTip();
            expFrom = exp;                          // раскрыть / свернуть прогноз
            expTarget = expTarget > 0.5f ? 0 : 1;
            expStart = clock.Elapsed.TotalMilliseconds;
            return;
        }
        if (cityRect.Contains(ToBase(e.Location)))
        {
            HideTip();
            cityPopup.Show(this, new Point((int)(20 * scale), (int)(cityRect.Bottom * scale)));
            return;
        }
        HideTip();
        if (e.Clicks == 2)   // двойной клик — включить / выключить «динамическую погоду»
        {
            SetDynamic(!dynamic);
            return;
        }
        ReleaseCapture();
        SendMessage(Handle, 0xA1, 2, 0); // перетаскивание окна
        SaveSettings();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Point bp = ToBase(e.Location);
        btnHover = BtnHit(bp);
        Cursor = cityRect.Contains(bp) || btnHover ? Cursors.Hand : Cursors.Default;

        TipKind want = TipKind.None;
        if (data != null && cityRect.Contains(bp)) want = TipKind.City;
        else if (data != null && tempRect.Contains(bp)) want = TipKind.Temp;
        if (want == tipShown) return;

        HideTip();
        Point at = PointToScreen(new Point(e.X + 12, e.Y + 20));
        if (want == TipKind.City)
        {
            string country = data.City.Country == "" ? "неизвестна" : data.City.Country;
            tip.ShowAt("", "Страна: " + country, at);
        }
        else if (want == TipKind.Temp)
            tip.ShowAt("Как ощущается", TipText(data), at);
        tipShown = want;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        btnHover = false;
        HideTip();
    }

    void HideTip()
    {
        if (tipShown != TipKind.None) { tip.Hide(); tipShown = TipKind.None; }
    }

    // ---------- Отрисовка ----------

    // Окно с попиксельной прозрачностью (layered window): картинка целиком рисуется
    // в 32-битный буфер с альфа-каналом и отдаётся Windows через UpdateLayeredWindow.
    // Благодаря этому края карточки сглажены, а всё вне карточки прозрачно и пропускает клики.

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int CX, CY; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight;
        public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }

    [DllImport("user32.dll")]
    static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, IntPtr pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")]
    static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);

    const byte WindowAlpha = 242;   // общая непрозрачность виджета (~95%)
    IntPtr memDC, dib, oldObj;
    Bitmap surface;                 // GDI+ обёртка над памятью DIB — рисуем прямо в неё
    int surfW, surfH;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80000;   // WS_EX_LAYERED
            return cp;
        }
    }

    void EnsureSurface(int w, int h)
    {
        if (surface != null && w == surfW && h == surfH) return;
        FreeSurface();
        BITMAPINFOHEADER bi = new BITMAPINFOHEADER();
        bi.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
        bi.biWidth = w;
        bi.biHeight = -h;   // строки сверху вниз, как у Bitmap
        bi.biPlanes = 1;
        bi.biBitCount = 32;
        memDC = CreateCompatibleDC(IntPtr.Zero);
        IntPtr bits;
        dib = CreateDIBSection(memDC, ref bi, 0, out bits, IntPtr.Zero, 0);
        oldObj = SelectObject(memDC, dib);
        surface = new Bitmap(w, h, w * 4, System.Drawing.Imaging.PixelFormat.Format32bppPArgb, bits);
        surfW = w;
        surfH = h;
    }

    void FreeSurface()
    {
        if (surface != null) { surface.Dispose(); surface = null; }
        if (memDC != IntPtr.Zero) { SelectObject(memDC, oldObj); DeleteObject(dib); DeleteDC(memDC); memDC = IntPtr.Zero; }
    }

    // нарисовать кадр и показать его
    void Present()
    {
        if (!IsHandleCreated || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        EnsureSurface(ClientSize.Width, ClientSize.Height);
        using (Graphics g = Graphics.FromImage(surface)) Render(g);

        SIZE size = new SIZE { CX = surfW, CY = surfH };
        POINT src = new POINT();
        BLENDFUNCTION blend = new BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = WindowAlpha, AlphaFormat = 1 };
        UpdateLayeredWindow(Handle, IntPtr.Zero, IntPtr.Zero, ref size, memDC, ref src, 0, ref blend, 2 /* ULW_ALPHA */);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Present();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        FreeSurface();
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
        BgColors(data, out top, out bottom);
        bool fading = trans < 1 && prevData != null;
        if (fading)
        {
            Color pt, pb;
            BgColors(prevData, out pt, out pb);
            float k = Ease(trans);
            top = Lerp(pt, top, k);
            bottom = Lerp(pb, bottom, k);
        }
        // карточка со скруглёнными углами, края сглажены
        RectangleF gradRect = new RectangleF(0, -1, W, bh + 2);   // с запасом, чтобы градиент не «заворачивался» на краях
        using (GraphicsPath shape = RoundRect(new RectangleF(0, 0, W, bh), 22))
        {
            using (LinearGradientBrush bg = new LinearGradientBrush(gradRect, top, bottom, 90f))
                g.FillPath(bg, shape);

            // содержимое обрезается по карточке (важно, пока прогноз выезжает)
            g.SetClip(shape);
            // первая половина перехода — старый город гаснет, вторая — новый проявляется
            WeatherData show = fading && trans < 0.5f ? prevData : data;
            DrawContent(g, show, bh);
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

    static Color Lerp(Color a, Color b, float k)
    {
        return Color.FromArgb((int)(a.R + (b.R - a.R) * k), (int)(a.G + (b.G - a.G) * k), (int)(a.B + (b.B - a.B) * k));
    }

    static void BgColors(WeatherData d, out Color top, out Color bottom)
    {
        Sky sky = d != null ? d.Sky : Sky.Cloudy;
        bool night = d != null && !d.IsDay;
        switch (sky)
        {
            case Sky.Sunny: top = Color.FromArgb(14, 143, 179); bottom = Color.FromArgb(79, 195, 176); break;
            case Sky.PartlyCloudy: top = Color.FromArgb(70, 130, 200); bottom = Color.FromArgb(140, 180, 220); break;
            case Sky.Rain: top = Color.FromArgb(45, 60, 85); bottom = Color.FromArgb(85, 105, 135); break;
            case Sky.Snow: top = Color.FromArgb(90, 115, 150); bottom = Color.FromArgb(160, 180, 205); break;
            default: top = Color.FromArgb(54, 98, 122); bottom = Color.FromArgb(128, 166, 180); break;   // пасмурно: «стальная бирюза»
        }
        if (night)
        {
            if (sky == Sky.Sunny || sky == Sky.PartlyCloudy) { top = Color.FromArgb(12, 24, 52); bottom = Color.FromArgb(38, 62, 104); }
            else { top = Darken(top, 0.55f); bottom = Darken(bottom, 0.55f); }
        }
    }

    void DrawContent(Graphics g, WeatherData d, float bh)
    {
        City shown = d != null ? d.City : target;
        if (shown != null)
            g.DrawString(shown.Name + " ▾", fCity, Brushes.White, cityRect, cityFmt);

        if (d == null)
        {
            g.DrawString(error ?? "Загрузка…", fDesc, Brushes.White, new RectangleF(0, 0, W, bh), center);
            return;
        }

        DrawIcon(g, d.Sky, !d.IsDay, W / 2f, 88f, 1f);

        g.DrawString(Deg(d.Temp), fTemp, Brushes.White, new RectangleF(40, 138, 160, 56), center);
        g.DrawString(Describe(d.Code), fDesc, Brushes.White, new RectangleF(0, 196, W, 22), center);

        Font f = fSmall;
        using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
        {
            // местное время показанного города (а не время компьютера)
            string time = DateTime.UtcNow.AddSeconds(d.UtcOffset).ToString("HH:mm");
            string s = (dynamic ? "динамическая погода · " : "местное время ") + time;
            if (error != null) s = error;

            // текст и маленькая кнопка прогноза справа от него, вместе по центру
            const float bs = 14, gap = 6, cy = 231;
            StringFormat typo = StringFormat.GenericTypographic;
            float tw = g.MeasureString(s, f, PointF.Empty, typo).Width;
            float x0 = (W - (tw + gap + bs)) / 2;
            g.DrawString(s, f, b, new PointF(x0, cy - f.GetHeight(g) / 2), typo);
            btnRect = new Rectangle((int)Math.Round(x0 + tw + gap), (int)(cy - bs / 2), (int)bs, (int)bs);
        }

        // кнопка прогноза: стрелка плавно переворачивается при раскрытии
        using (SolidBrush b = new SolidBrush(Color.FromArgb(btnHover ? 90 : 45, 255, 255, 255)))
            g.FillEllipse(b, btnRect);
        using (Pen p = new Pen(Color.White, 1.5f))
        {
            p.StartCap = LineCap.Round;
            p.EndCap = LineCap.Round;
            p.LineJoin = LineJoin.Round;
            float cx = btnRect.X + btnRect.Width / 2f, cyb = btnRect.Y + btnRect.Height / 2f;
            float dy = 1.8f * (1 - 2 * exp);
            g.DrawLines(p, new PointF[] { new PointF(cx - 3.5f, cyb - dy), new PointF(cx, cyb + dy), new PointF(cx + 3.5f, cyb - dy) });
        }

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
                DrawIcon(g, day.Sky, false, x, fy + 44, 0.36f);
                g.DrawString(Deg(day.Max) + " / " + Deg(day.Min), fDayTemp, Brushes.White, new RectangleF(x - 40, fy + 70, 80, 20), center);
            }
        }
    }

    static Color Darken(Color c, float k)
    {
        return Color.FromArgb((int)(c.R * k), (int)(c.G * k), (int)(c.B * k));
    }

    // Иконка погоды с центром (cx, cy); k — масштаб (1 = большая иконка)
    void DrawIcon(Graphics g, Sky sky, bool night, float cx, float cy, float k)
    {
        GraphicsState st = g.Save();
        g.TranslateTransform(cx, cy);
        g.ScaleTransform(k, k);
        switch (sky)
        {
            case Sky.Sunny:
                if (night) { DrawStars(g); DrawMoon(g, 0, 0, 28); }
                else DrawSun(g, 0, 0, 30);
                break;
            case Sky.PartlyCloudy:
                if (night) DrawMoon(g, 18, -14, 20); else DrawSun(g, 18, -14, 22);
                DrawCloud(g, -12 + (float)Math.Sin(phase * 0.04) * 6, 14, 0.8f, Color.FromArgb(245, 250, 255));
                break;
            case Sky.Cloudy:
                DrawCloud(g, 20 + (float)Math.Sin(phase * 0.03) * 8, -12, 0.7f, Color.FromArgb(200, 210, 222));
                DrawCloud(g, -10 + (float)Math.Sin(phase * 0.04 + 1) * 6, 10, 0.9f, Color.FromArgb(235, 240, 246));
                break;
            case Sky.Rain:
                DrawDrops(g, 0, 0, false);
                DrawCloud(g, (float)Math.Sin(phase * 0.03) * 4, -12, 0.95f, Color.FromArgb(175, 185, 200));
                break;
            case Sky.Snow:
                DrawDrops(g, 0, 0, true);
                DrawCloud(g, (float)Math.Sin(phase * 0.03) * 4, -12, 0.95f, Color.FromArgb(225, 232, 242));
                break;
        }
        g.Restore(st);
    }

    void DrawSun(Graphics g, float cx, float cy, float r)
    {
        float angle = phase * 0.015f;
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

    void DrawMoon(Graphics g, float cx, float cy, float r)
    {
        // мягкое «дыхание» свечения
        int glowAlpha = 45 + (int)(20 * Math.Sin(phase * 0.05));
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

    void DrawStars(Graphics g)
    {
        float[,] pos = { { -62, -32 }, { 58, -42 }, { -44, 38 }, { 66, 22 }, { -74, 4 }, { 34, -58 }, { 48, 50 } };
        for (int i = 0; i < pos.GetLength(0); i++)
        {
            int a = 130 + (int)(110 * Math.Sin(phase * 0.07 + i * 1.7));
            float s = 2.5f + (i % 3) * 0.7f;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 255, 255, 230)))
                g.FillEllipse(b, pos[i, 0] - s / 2, pos[i, 1] - s / 2, s, s);
        }
    }

    static void DrawCloud(Graphics g, float cx, float cy, float s, Color c)
    {
        using (GraphicsPath p = new GraphicsPath(FillMode.Winding))   // Winding — без «дыр» на пересечениях
        {
            p.AddEllipse(cx - 50 * s, cy - 10 * s, 44 * s, 36 * s);
            p.AddEllipse(cx - 28 * s, cy - 34 * s, 52 * s, 52 * s);
            p.AddEllipse(cx + 6 * s, cy - 20 * s, 44 * s, 44 * s);
            p.AddRectangle(new RectangleF(cx - 30 * s, cy + 2 * s, 60 * s, 24 * s));
            using (SolidBrush b = new SolidBrush(c))
                g.FillPath(b, p);
        }
    }

    void DrawDrops(Graphics g, float cx, float cy, bool snow)
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
                int alpha = (int)(255 * (1 - y / fall));
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

    static GraphicsPath RoundRect(RectangleF r, float rad)
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

    // ---------- Настройки ----------

    void LoadSettings()
    {
        try
        {
            if (!File.Exists(settingsPath)) return;
            string name = null, country = "";
            double lat = double.NaN, lon = double.NaN;
            foreach (string line in File.ReadAllLines(settingsPath, Encoding.UTF8))
            {
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string k = line.Substring(0, eq).Trim('﻿', ' '), v = line.Substring(eq + 1);
                if (k == "city") name = v;
                else if (k == "country") country = v;
                else if (k == "lat" || k == "lon")
                {
                    double n;
                    if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out n)) { if (k == "lat") lat = n; else lon = n; }
                }
                else if (k == "x" || k == "y")
                {
                    int n;
                    if (int.TryParse(v, out n)) Location = k == "x" ? new Point(n, Location.Y) : new Point(Location.X, n);
                }
                else if (k == "topmost") TopMost = v == "1";
                else if (k == "dynamic") dynamic = v == "1";
                else if (k == "fav")
                {
                    // свой город: Название|Страна|широта|долгота
                    string[] p = v.Split('|');
                    double fl, fo;
                    if (p.Length == 4 &&
                        double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out fl) &&
                        double.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out fo))
                        RememberCity(new City(p[0], p[1], fl, fo));
                }
                else if (k == "scale")
                {
                    float s;
                    if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out s))
                    {
                        s = Math.Max(MinScale, Math.Min(MaxScale, s));
                        ClientSize = new Size((int)Math.Round(W * s), (int)Math.Round(H1 * s));
                    }
                }
            }

            if (name != null)
            {
                if (!double.IsNaN(lat) && !double.IsNaN(lon)) home = new City(name, country, lat, lon);
                foreach (City c in Favorites)
                    if (c.Name == name) home = c;   // свой экземпляр избранного (и совместимость со старыми настройками)
                // свой город, выбранный до появления списка своих городов, — тоже в список
                bool known = IsStandard(home);
                foreach (City c in custom) if (c.Same(home)) known = true;
                if (!known) RememberCity(home);
            }

            // если окно оказалось за пределами экранов — вернуть на основной
            bool visible = false;
            foreach (Screen s in Screen.AllScreens) if (s.WorkingArea.IntersectsWith(new Rectangle(Location, Size))) visible = true;
            if (!visible) { Rectangle wa = Screen.PrimaryScreen.WorkingArea; Location = new Point(wa.Right - Width - 30, wa.Top + 30); }
        }
        catch { }
    }

    void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
            List<string> lines = new List<string>(new string[]
            {
                "city=" + home.Name,
                "country=" + home.Country,
                "lat=" + home.Lat.ToString(CultureInfo.InvariantCulture),
                "lon=" + home.Lon.ToString(CultureInfo.InvariantCulture),
                "x=" + Location.X,
                "y=" + Location.Y,
                "topmost=" + (TopMost ? "1" : "0"),
                "dynamic=" + (dynamic ? "1" : "0"),
                "scale=" + scale.ToString("0.###", CultureInfo.InvariantCulture)
            });
            foreach (City c in custom)
                lines.Add("fav=" + c.Name.Replace("|", " ") + "|" + c.Country.Replace("|", " ") + "|" +
                    c.Lat.ToString(CultureInfo.InvariantCulture) + "|" + c.Lon.ToString(CultureInfo.InvariantCulture));
            File.WriteAllLines(settingsPath, lines.ToArray(), new UTF8Encoding(false));
        }
        catch { }
    }

    static bool IsAutoStart()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(AppName) != null;
        }
        catch { return false; }
    }

    static void SetAutoStart(bool on)
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (k == null) return;
                if (on) k.SetValue(AppName, "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue(AppName, false);
            }
        }
        catch { }
    }

    [STAThread]
    static void Main()
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new WeatherForm());
    }
}
