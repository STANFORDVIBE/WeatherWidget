using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace WeatherWidget
{
    // Меню выбора города: поиск по миру, стандартные города России и «свои» города.
    // Одинаковое меню живёт в двух местах — по клику на название и в меню правой кнопки.
    sealed class CityMenu
    {
        readonly WeatherForm form;
        readonly ToolStripDropDown dd;
        readonly ToolStripTextBox box = new ToolStripTextBox();
        readonly List<ToolStripItem> results = new List<ToolStripItem>();
        readonly List<ToolStripMenuItem> favItems = new List<ToolStripMenuItem>();
        readonly List<ToolStripItem> customItems = new List<ToolStripItem>();
        readonly System.Windows.Forms.Timer debounce = new System.Windows.Forms.Timer();

        public CityMenu(WeatherForm f, ToolStripDropDown d)
        {
            form = f;
            dd = d;
            box.AutoSize = false;
            box.Width = 220;
            box.TextBox.HandleCreated += delegate
            {
                NativeMethods.SendMessage(box.TextBox.Handle, NativeMethods.EM_SETCUEBANNER, (IntPtr)1, "Поиск города");
            };
            box.TextChanged += delegate { debounce.Stop(); debounce.Start(); };
            box.KeyDown += OnKey;
            debounce.Interval = 450;   // искать, когда пользователь перестал печатать
            debounce.Tick += delegate { debounce.Stop(); Search(); };

            dd.Items.Add(box);
            dd.Items.Add(new ToolStripSeparator());
            foreach (City c in Cities.Favorites)
            {
                ToolStripMenuItem it = new ToolStripMenuItem(c.Name);
                it.Tag = c;
                it.Click += OnPick;
                favItems.Add(it);
                dd.Items.Add(it);
            }
            CustomCityItem.KeepOpenOnRemove(dd);
            dd.Opening += delegate { RebuildCustom(); };
            dd.Opened += delegate
            {
                box.Text = "";
                ClearResults();
                foreach (ToolStripMenuItem it in favItems) it.Checked = IsCurrent((City)it.Tag);
                box.Focus();
            };
        }

        bool IsCurrent(City c) { return !form.Dynamic && c.Same(form.Home); }

        // «Свои» города — отдельным блоком под стандартными
        void RebuildCustom()
        {
            foreach (ToolStripItem it in customItems) { dd.Items.Remove(it); it.Dispose(); }
            customItems.Clear();
            if (form.Custom.Count == 0) return;
            ToolStripSeparator sep = new ToolStripSeparator();
            customItems.Add(sep);
            dd.Items.Add(sep);
            foreach (City c in form.Custom.Items)
            {
                CustomCityItem it = new CustomCityItem(c.MenuLabel);
                it.Tag = c;
                it.Click += OnPick;
                it.RemoveClicked += OnRemove;
                it.Checked = IsCurrent(c);
                it.Removable = !c.Same(form.Home);   // выбранный город удалить нельзя
                customItems.Add(it);
                dd.Items.Add(it);
            }
        }

        void OnRemove(object sender, EventArgs e)
        {
            City c = (City)((ToolStripItem)sender).Tag;
            form.BeginInvoke((MethodInvoker)delegate   // после завершения клика: убрать город и перестроить список
            {
                form.Custom.Remove(c);
                form.SaveSettings();
                RebuildCustom();
            });
        }

        void OnPick(object sender, EventArgs e)
        {
            form.SelectHome((City)((ToolStripItem)sender).Tag);
        }

        // Enter в поиске — выбрать первый найденный город
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

            string url = OpenMeteo.SearchUrl(q);
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<GeoResult> found = null;
                try { found = OpenMeteo.ParseSearch(Http.Download(url)); }
                catch { }

                form.SafeInvoke(delegate
                {
                    if (box.Text.Trim() != q) return;   // пользователь уже ввёл другой запрос
                    List<ToolStripItem> items = new List<ToolStripItem>();
                    if (found == null) items.Add(Status("Нет связи с сервером"));
                    else if (found.Count == 0) items.Add(Status("Ничего не найдено"));
                    else
                        foreach (GeoResult g in found)
                        {
                            ToolStripMenuItem it = new ToolStripMenuItem(g.Label);
                            it.Tag = g.City;
                            it.Click += OnPick;
                            items.Add(it);
                        }
                    SetResults(items);
                });
            });
        }
    }
}
