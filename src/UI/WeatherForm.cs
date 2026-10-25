using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
        const int RefreshMinutes = 15, DynamicSeconds = 45;

        readonly string settingsPath;
        City home = Cities.Favorites[0];   // город, выбранный пользователем
        City target;                       // город, который сейчас загружается/показывается
        WeatherData data;
        string error;
        bool dynamic;
        readonly CustomCities custom;
        readonly CityRotation rotation = new CityRotation(Cities.World, new Random());

        readonly System.Windows.Forms.Timer refreshTimer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer dynTimer = new System.Windows.Forms.Timer();
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly ContextMenuStrip cityPopup = new ContextMenuStrip();   // по клику на название города
        readonly ToolStripMenuItem cityMenu = new ToolStripMenuItem("Город");
        readonly ToolStripMenuItem dynItem = new ToolStripMenuItem("Динамическая погода");
        readonly ToolStripMenuItem topMostItem = new ToolStripMenuItem("Поверх всех окон");
        readonly ToolStripMenuItem autoStartItem = new ToolStripMenuItem("Запускать вместе с Windows");
        readonly List<CityMenu> cityMenus = new List<CityMenu>();

        // для CityMenu
        internal City Home { get { return home; } }
        internal bool Dynamic { get { return dynamic; } }
        internal CustomCities Custom { get { return custom; } }

        public WeatherForm() : this(Settings.DefaultPath) { }

        public WeatherForm(string settingsPath)
        {
            this.settingsPath = settingsPath;
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
        }

        void ApplySettings(Settings s)
        {
            if (s.Home != null) home = s.Home;
            dynamic = s.Dynamic;
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
            foreach (City c in custom.Items) s.Custom.Remember(c);
            s.Save(settingsPath);
        }

        void BuildMenus()
        {
            cityMenus.Add(new CityMenu(this, cityMenu.DropDown));
            cityMenus.Add(new CityMenu(this, cityPopup));
            CustomCityItem.KeepOpenOnRemove(menu);   // крестик в подменю не должен закрывать и главное меню

            menu.Items.Add(cityMenu);
            dynItem.Checked = dynamic;
            dynItem.Click += delegate { SetDynamic(!dynamic); };
            menu.Items.Add(dynItem);
            menu.Items.Add("Обновить", null, delegate { Fetch(target); });
            menu.Items.Add(new ToolStripSeparator());
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
        }

        void ShowAbout()
        {
            MessageBox.Show(
                "Погода — виджет для рабочего стола\nВерсия " + Program.Version + "\n\n" +
                "Данные о погоде: Open-Meteo.com (лицензия CC BY 4.0)",
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
            else Fetch(home);
            SaveSettings();
        }

        internal void CloseMenus()
        {
            menu.Close();
            cityPopup.Close();
        }

        // ---------- Данные ----------

        void Fetch(City c)
        {
            if (c == null) return;
            target = c;
            string url = OpenMeteo.ForecastUrl(c);
            ThreadPool.QueueUserWorkItem(delegate
            {
                WeatherData d = null;
                for (int attempt = 0; attempt < 3 && d == null; attempt++)   // сеть иногда сбоит — пробуем ещё раз
                {
                    try { d = OpenMeteo.ParseForecast(c, OpenMeteo.Download(url)); }
                    catch { Thread.Sleep(1500); }
                }
                SafeInvoke(delegate { OnWeather(c, d); });
            });
        }

        void OnWeather(City c, WeatherData d)
        {
            if (c != target) return;   // пока грузилось, выбрали другой город
            if (d != null)
            {
                if (data != null && !data.City.Same(d.City)) StartCityTransition();
                data = d;
                error = null;
            }
            else if (!dynamic) error = "Нет связи с сервером";   // в динамике просто ждём следующий город
        }

        internal void SafeInvoke(MethodInvoker m)
        {
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke(m); } catch { }
        }
    }
}
