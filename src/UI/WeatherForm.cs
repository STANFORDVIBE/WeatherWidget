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
        const int W = 240, H0 = 256, H1 = 356;   // базовый размер (масштаб 100%): свёрнутая карточка / с прогнозом
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
        readonly CityRotation rotation = new CityRotation(Cities.World, new Random());

        readonly System.Windows.Forms.Timer refreshTimer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer dynTimer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer retryTimer = new System.Windows.Forms.Timer();   // повтор, пока нет связи
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly ToolStripMenuItem dynItem = new ToolStripMenuItem("Динамическая погода");
        readonly ToolStripMenuItem topMostItem = new ToolStripMenuItem("Поверх всех окон");
        readonly ToolStripMenuItem autoStartItem = new ToolStripMenuItem("Запускать вместе с Windows");

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
            Text = "Погода";
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

            BuildMenus();
            StartTimers();

            FormClosing += delegate { SaveSettings(); };
            Load += delegate
            {
                if (dynamic) { rotation.Restart(); dynTimer.Start(); Fetch(rotation.Next()); }
                else Fetch(home);
            };
            // город ещё не выбран — сразу предложить выбрать
            Shown += delegate { if (home == null && !dynamic) ShowCityMenu(); };
        }

        void ApplySettings(Settings s)
        {
            if (s.Home != null) home = s.Home;
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
            foreach (City c in custom.Items) s.Custom.Remember(c);
            s.Save(settingsPath);
        }

        void BuildMenus()
        {
            menu.Items.Add("Выбрать город…", null, delegate { ShowCityMenu(); });
            dynItem.Checked = dynamic;
            dynItem.Click += delegate { SetDynamic(!dynamic); };
            menu.Items.Add(dynItem);
            menu.Items.Add("Обновить", null, delegate { Fetch(target); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Прозрачность…", null, delegate { ShowOpacityPanel(); });
            topMostItem.Checked = TopMost;
            topMostItem.Click += delegate { TopMost = !TopMost; topMostItem.Checked = TopMost; SaveSettings(); };
            menu.Items.Add(topMostItem);
            autoStartItem.Checked = AutoStart.IsEnabled();
            autoStartItem.Click += delegate { AutoStart.Set(!autoStartItem.Checked); autoStartItem.Checked = AutoStart.IsEnabled(); };
            menu.Items.Add(autoStartItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("О программе", null, delegate { ShowAbout(); });
            menu.Items.Add("Выход", null, delegate { Close(); });
            ContextMenuStrip = menu;
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
            SafeInvoke(delegate { if (data == null || data.Stale || error != null) Fetch(target); });
        }

        void ShowAbout()
        {
            MessageBox.Show(
                "Погода — виджет для рабочего стола\nВерсия " + Program.Version + "\n\n" +
                "Данные:\n" +
                "• Погода — Open-Meteo.com, резерв — MET Norway (api.met.no)\n" +
                "• Названия городов — GeoNames.org (через поиск Open-Meteo)\n" +
                "Все данные распространяются по лицензии CC BY 4.0.\n\n" +
                "Лицензия программы: MIT",
                "О программе", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            else if (data == null || !data.City.Same(c)) error = "Нет связи с сервером";   // показать нечего

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
