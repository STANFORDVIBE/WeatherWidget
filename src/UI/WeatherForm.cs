using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows.Forms;

namespace WeatherWidget
{
    // Окно виджета: состояние, меню, таймеры и загрузка погоды.
    // Работа с окном и мышью — WeatherForm.Window.cs, отрисовка — WeatherForm.Rendering.cs.
    sealed partial class WeatherForm : Form
    {
        // базовый размер (масштаб 100%): свёрнутая карточка / с прогнозом (по часам + по дням)
        const int W = 240, H0 = 256, HoursH = 84, DaysH = 100, H1 = H0 + HoursH + DaysH;
        const float MinScale = 0.6f, MaxScale = 2.5f;
        const int RefreshMinutes = 15, DynamicSeconds = 45, OfflineRetryMinutes = 2;

        readonly string settingsPath;
        readonly WeatherService weather;
        City home;                         // город, выбранный пользователем (null — ещё не выбран)
        City target;                       // город, который сейчас загружается/показывается
        WeatherData data;
        string error;
        bool loading;                      // идёт загрузка погоды для target
        bool dynamic;
        readonly CustomCities custom;
        CityRotation rotation;             // города — на языке интерфейса, пересоздаётся при смене языка
        string lang;                       // выбранный в меню язык; null — как в Windows

        readonly System.Windows.Forms.Timer refreshTimer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer dynTimer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer retryTimer = new System.Windows.Forms.Timer();   // повтор, пока нет связи
        // меню пересобирается при смене языка
        ContextMenuStrip menu;
        ToolStripMenuItem dynItem, topMostItem, autoStartItem;

        // для панели выбора города (CityPicker)
        internal City Home { get { return home; } }
        internal bool Dynamic { get { return dynamic; } }
        internal CustomCities Custom { get { return custom; } }

        public WeatherForm() : this(Settings.DefaultPath) { }

        public WeatherForm(string settingsPath)
        {
            this.settingsPath = settingsPath;
            WeatherCache cache = new WeatherCache(Path.Combine(Path.GetDirectoryName(settingsPath), "cache"));
            weather = new WeatherService(Http.Download, cache, 1500);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            // Окно всегда высотой с раскрытую карточку; место ниже свёрнутой карточки
            // полностью прозрачное и не перехватывает клики. Так при раскрытии прогноза
            // окно не меняет размер — нет рывков и мерцания.
            ClientSize = new Size(W, H1);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            Settings s = Settings.Load(settingsPath);
            custom = s.Custom;
            ApplySettings(s);
            Text = L.T("Погода", "Weather");
            rotation = new CityRotation(Cities.World, new Random());

            BuildMenus();
            StartTimers();

            FormClosing += delegate { SaveSettings(); };
            Load += delegate
            {
                if (dynamic) { rotation.Restart(); dynTimer.Start(); Fetch(rotation.Next()); }
                else Fetch(home);
                TranslateCities();   // язык сменили, когда не было связи, — перевести сейчас
            };
            // город ещё не выбран — сразу предложить выбрать
            Shown += delegate { if (home == null && !dynamic) ShowCityMenu(); };
        }

        void ApplySettings(Settings s)
        {
            if (s.Home != null) home = s.Home;
            // язык не выбран в меню — остаётся тот, что определили по Windows при запуске
            lang = s.Lang;
            if (lang != null) L.Use(lang);
            dynamic = s.Dynamic;
            if (s.Transparency.HasValue) bgTransparency = Math.Max(0, Math.Min(MaxTransparency, s.Transparency.Value));
            TopMost = s.TopMost;
            if (s.Scale.HasValue)
            {
                float k = Math.Max(MinScale, Math.Min(MaxScale, s.Scale.Value));
                ClientSize = new Size((int)Math.Round(W * k), (int)Math.Round(H1 * k));
            }
            UpdateScale();

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = s.Position ?? new Point(wa.Right - Width - 30, wa.Top + 30);
            // если окно оказалось за пределами экранов (например, отключили монитор) — вернуть на основной
            bool visible = false;
            foreach (Screen sc in Screen.AllScreens) if (sc.WorkingArea.IntersectsWith(new Rectangle(Location, Size))) visible = true;
            if (!visible) Location = new Point(wa.Right - Width - 30, wa.Top + 30);
        }

        internal void SaveSettings()
        {
            Settings s = new Settings();
            s.Home = home;
            s.Position = Location;
            s.TopMost = TopMost;
            s.Dynamic = dynamic;
            s.Scale = scale;
            s.Transparency = bgTransparency;
            s.Lang = lang;
            foreach (City c in custom.Items) s.Custom.Remember(c);
            s.Save(settingsPath);
        }

        void BuildMenus()
        {
            ContextMenuStrip old = menu;
            menu = new ContextMenuStrip();
            menu.Items.Add(L.T("Выбрать город…", "Choose city…"), null, delegate { ShowCityMenu(); });
            dynItem = new ToolStripMenuItem(L.T("Динамическая погода", "Dynamic weather"));
            dynItem.Checked = dynamic;
            dynItem.Click += delegate { SetDynamic(!dynamic); };
            menu.Items.Add(dynItem);
            menu.Items.Add(L.T("Обновить", "Refresh"), null, delegate { Fetch(target); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("Прозрачность…", "Transparency…"), null, delegate { ShowOpacityPanel(); });
            topMostItem = new ToolStripMenuItem(L.T("Поверх всех окон", "Always on top"));
            topMostItem.Checked = TopMost;
            topMostItem.Click += delegate { TopMost = !TopMost; topMostItem.Checked = TopMost; SaveSettings(); };
            menu.Items.Add(topMostItem);
            autoStartItem = new ToolStripMenuItem(L.T("Запускать вместе с Windows", "Start with Windows"));
            autoStartItem.Checked = AutoStart.IsEnabled();
            autoStartItem.Click += delegate { AutoStart.Set(!autoStartItem.Checked); autoStartItem.Checked = AutoStart.IsEnabled(); };
            menu.Items.Add(autoStartItem);
            // названия языков — на самих языках, чтобы их узнал тот, кто не читает текущий
            ToolStripMenuItem langItem = new ToolStripMenuItem("Язык / Language");
            ToolStripMenuItem ru = new ToolStripMenuItem("Русский", null, delegate { SetLanguage("ru"); });
            ToolStripMenuItem en = new ToolStripMenuItem("English", null, delegate { SetLanguage("en"); });
            ru.Checked = !L.English;
            en.Checked = L.English;
            langItem.DropDownItems.Add(ru);
            langItem.DropDownItems.Add(en);
            menu.Items.Add(langItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("О программе", "About"), null, delegate { ShowAbout(); });
            menu.Items.Add(L.T("Выход", "Exit"), null, delegate { Close(); });
            ContextMenuStrip = menu;
            if (old != null) old.Dispose();
        }

        // Сменить язык сразу, без перезапуска: меню, карточка и города «динамической погоды»
        internal void SetLanguage(string code)
        {
            lang = code;
            L.Use(code);
            Text = L.T("Погода", "Weather");
            SaveSettings();
            // меню пересобираем после того, как закроется текущее (клик пришёл из него)
            SafeInvoke(delegate { BuildMenus(); });
            rotation = new CityRotation(Cities.World, new Random());
            if (dynamic) { rotation.Restart(); dynTimer.Stop(); dynTimer.Start(); Fetch(rotation.Next()); }
            else if (target != null) { error = null; Fetch(target); }   // описание погоды и ошибки — на новом языке
            TranslateCities();
        }

        bool translating;
        internal Func<string, string> NamesDownload = Http.Download;   // тесты подменяют сеть

        // Свои города (и выбранный) — с названиями на языке интерфейса. Названия берутся с сервера
        // в фоне; без связи остаются прежними и переводятся позже — при запуске или когда вернётся сеть.
        void TranslateCities()
        {
            if (translating) return;
            string lang = L.Code;
            List<City> todo = new List<City>();
            foreach (City c in custom.Items) if (c.Lang != lang) todo.Add(c);
            if (home != null && home.Lang != lang && !custom.Contains(home)) todo.Add(home);
            if (todo.Count == 0) return;
            translating = true;
            Func<string, string> download = NamesDownload;
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<City[]> done = new List<City[]>();   // пары: было — стало
                foreach (City c in todo)
                {
                    City t = CityNames.Translate(c, lang, download);
                    if (t != null) done.Add(new City[] { c, t });
                }
                SafeInvoke(delegate
                {
                    translating = false;
                    if (lang == L.Code) ApplyTranslations(done);
                    else TranslateCities();   // пока переводили, язык сменили ещё раз
                });
            });
        }

        internal void ApplyTranslations(List<City[]> done)
        {
            if (done.Count == 0) return;
            bool refetch = false;
            foreach (City[] p in done)
            {
                custom.Replace(p[0], p[1]);
                if (p[0].Same(home)) home = p[1];
                if (data != null && p[0].Same(data.City)) data.City = p[1];
                // загрузка, начатая со старым названием, будет отброшена (target уже другой) — запросить заново
                if (p[0].Same(target)) { target = p[1]; refetch = true; }
            }
            SaveSettings();
            if (refetch) Fetch(target);
        }

        void StartTimers()
        {
            animTimer.Interval = 40;
            animTimer.Tick += delegate { Animate(); };
            animTimer.Start();

            refreshTimer.Interval = RefreshMinutes * 60 * 1000;
            refreshTimer.Tick += delegate { Fetch(target); };
            refreshTimer.Start();

            dynTimer.Interval = DynamicSeconds * 1000;
            dynTimer.Tick += delegate { Fetch(rotation.Next()); };

            retryTimer.Interval = OfflineRetryMinutes * 60 * 1000;
            retryTimer.Tick += delegate { retryTimer.Stop(); Fetch(target); };

            // сеть вернулась — обновить сразу, не дожидаясь таймера
            NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        }

        void OnNetworkChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            if (!e.IsAvailable) return;
            SafeInvoke(delegate
            {
                if (data == null || data.Stale || error != null) Fetch(target);
                TranslateCities();
            });
        }

        void ShowAbout()
        {
            MessageBox.Show(L.English
                ? "Weather — a desktop widget\nVersion " + Program.Version + "\n\n" +
                  "Data:\n" +
                  "• Weather — Open-Meteo.com, backup — MET Norway (api.met.no)\n" +
                  "• City names — GeoNames.org (via Open-Meteo search)\n" +
                  "All data is licensed under CC BY 4.0.\n\n" +
                  "Program license: MIT"
                : "Погода — виджет для рабочего стола\nВерсия " + Program.Version + "\n\n" +
                  "Данные:\n" +
                  "• Погода — Open-Meteo.com, резерв — MET Norway (api.met.no)\n" +
                  "• Названия городов — GeoNames.org (через поиск Open-Meteo)\n" +
                  "Все данные распространяются по лицензии CC BY 4.0.\n\n" +
                  "Лицензия программы: MIT",
                L.T("О программе", "About"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ---------- Выбор города ----------

        internal void SelectHome(City c)
        {
            custom.Remember(c);
            home = c;
            if (dynamic) { dynamic = false; dynItem.Checked = false; dynTimer.Stop(); }
            SaveSettings();
            Fetch(home);
        }

        void SetDynamic(bool on)
        {
            dynamic = on;
            dynItem.Checked = on;
            dynTimer.Stop();
            if (on) { rotation.Restart(); dynTimer.Start(); Fetch(rotation.Next()); }
            else if (home != null) Fetch(home);
            else
            {
                // динамику выключили, а свой город ещё не выбран — вернуться к выбору города
                target = null;
                data = null;
                error = null;
                ShowCityMenu();
            }
            SaveSettings();
        }

        // ---------- Данные ----------

        void Fetch(City c)
        {
            if (c == null) return;
            target = c;
            loading = true;
            error = null;
            // Другой город — показать его сразу: из кэша (если есть) или «Загрузка…»,
            // а свежие данные подставятся, когда придут. Иначе карточка «застывала» на старом городе.
            bool switching = data == null || !data.City.Same(c);
            ThreadPool.QueueUserWorkItem(delegate
            {
                if (switching)
                {
                    WeatherData old = weather.Cached(c);
                    SafeInvoke(delegate { if (c == target && loading) ShowNow(c, old); });
                }
                WeatherData d = weather.Get(c);   // Open-Meteo и MET Norway наперегонки, иначе кэш
                SafeInvoke(delegate { OnWeather(c, d); });
            });
        }

        // Мгновенно показать выбранный город (данные из кэша или пока без данных)
        void ShowNow(City c, WeatherData cached)
        {
            if (data != null && data.City.Same(c)) return;
            if (data != null) StartCityTransition();
            data = cached;   // null — карточка покажет название города и «Загрузка…»
        }

        void OnWeather(City c, WeatherData d)
        {
            if (c != target) return;   // пока грузилось, выбрали другой город
            loading = false;
            if (d != null)
            {
                if (data != null && !data.City.Same(d.City)) StartCityTransition();
                data = d;
                error = null;
            }
            else if (data == null || !data.City.Same(c)) error = L.T("Нет связи с сервером", "Can't reach the server");   // показать нечего

            // нет свежих данных — повторить через пару минут (в динамике и так скоро следующий город)
            retryTimer.Stop();
            if ((d == null || d.Stale) && !dynamic) retryTimer.Start();
        }

        internal void SafeInvoke(MethodInvoker m)
        {
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke(m); } catch { }
        }
    }
}
