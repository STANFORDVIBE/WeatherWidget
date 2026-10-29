using System;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WeatherWidget
{
    // Окно: прозрачность, изменение размера за углы, перетаскивание, клики и подсказки
    sealed partial class WeatherForm
    {
        enum TipKind { None, City, Temp }

        readonly TipWindow tip = new TipWindow();
        TipKind tipShown = TipKind.None;
        float scale = 1f;            // масштаб карточки (ширина окна / W)
        bool fixingSize;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_LAYERED;   // попиксельная прозрачность, см. LayeredSurface
                return cp;
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            Present();   // первый кадр — сразу при показе
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
            surface.Dispose();
            tip.Dispose();
        }

        void UpdateScale()
        {
            scale = ClientSize.Width / (float)W;
        }

        // ---------- Размер ----------

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // Высота окна всегда соответствует раскрытой карточке (ширина × H1/W),
            // иначе раскрытый прогноз обрежется. Поправляем, если размер пришёл другим.
            // Только у показанного окна: во время создания Windows на миг сообщает размер
            // за вычетом рамок, и поправка «закрепила» бы неверную ширину.
            int want = (int)Math.Round(ClientSize.Width * (double)H1 / W);
            if (Visible && !fixingSize && ClientSize.Width > 0 && ClientSize.Height != want)
            {
                fixingSize = true;
                ClientSize = new Size(ClientSize.Width, want);
                fixingSize = false;
            }
            UpdateScale();
            Present();
        }

        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            SaveSettings();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_NCHITTEST)
            {
                // углы карточки работают как ручки изменения размера (Windows сама покажет курсор ↘↙)
                base.WndProc(ref m);
                if (m.Result.ToInt32() == NativeMethods.HTCLIENT)
                {
                    long lp = m.LParam.ToInt64();
                    Point p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
                    int z = Math.Max(14, (int)(18 * scale));
                    int cardBottom = (int)(BaseH * scale);   // низ карточки (ниже окно прозрачное)
                    bool l = p.X < z, r = p.X >= ClientSize.Width - z;
                    bool t = p.Y < z, b = p.Y >= cardBottom - z && p.Y < cardBottom;
                    if (t && l) m.Result = (IntPtr)NativeMethods.HTTOPLEFT;
                    else if (t && r) m.Result = (IntPtr)NativeMethods.HTTOPRIGHT;
                    else if (b && l) m.Result = (IntPtr)NativeMethods.HTBOTTOMLEFT;
                    else if (b && r) m.Result = (IntPtr)NativeMethods.HTBOTTOMRIGHT;
                }
                return;
            }
            if (m.Msg == NativeMethods.WM_SIZING)
            {
                // сохраняем пропорции и ограничиваем масштаб
                NativeMethods.RECT rc = (NativeMethods.RECT)Marshal.PtrToStructure(m.LParam, typeof(NativeMethods.RECT));
                int edge = m.WParam.ToInt32();
                int w = rc.Right - rc.Left;
                w = Math.Max((int)(W * MinScale), Math.Min((int)(W * MaxScale), w));
                int h = (int)Math.Round(w * (double)H1 / W);
                bool left = edge == NativeMethods.WMSZ_LEFT || edge == NativeMethods.WMSZ_TOPLEFT || edge == NativeMethods.WMSZ_BOTTOMLEFT;
                bool top = edge == NativeMethods.WMSZ_TOP || edge == NativeMethods.WMSZ_TOPLEFT || edge == NativeMethods.WMSZ_TOPRIGHT;
                if (left) rc.Left = rc.Right - w; else rc.Right = rc.Left + w;
                if (top) rc.Top = rc.Bottom - h; else rc.Bottom = rc.Top + h;
                Marshal.StructureToPtr(rc, m.LParam, false);
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }

        // ---------- Мышь ----------

        // координаты мыши -> координаты базового макета (ширина 240)
        Point ToBase(Point p) { return new Point((int)(p.X / scale), (int)(p.Y / scale)); }

        bool BtnHit(Point bp)
        {
            Rectangle r = btnRect;
            r.Inflate(5, 5);   // кнопка маленькая — зона нажатия чуть больше
            return data != null && r.Contains(bp);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Point bp = ToBase(e.Location);
            if (bp.Y > BaseH) return;   // ниже карточки — пусто
            HideTip();
            if (BtnHit(bp)) { ToggleForecast(); return; }
            if (cityHit.Contains(bp)) { ShowCityMenu(); return; }
            if (e.Clicks == 2) { SetDynamic(!dynamic); return; }   // двойной клик — вкл/выкл «динамическую погоду»

            NativeMethods.ReleaseCapture();   // перетаскивание окна
            NativeMethods.SendMessage(Handle, NativeMethods.WM_NCLBUTTONDOWN, NativeMethods.HTCAPTION, 0);
            SaveSettings();
        }

        OpacityPanel opacityPanel;

        // Панель «Прозрачность фона» под карточкой
        void ShowOpacityPanel()
        {
            HideTip();
            if (opacityPanel != null) { opacityPanel.Activate(); return; }
            opacityPanel = new OpacityPanel(this);
            opacityPanel.FormClosed += delegate { opacityPanel = null; };
            Point top = PointToScreen(new Point(ClientSize.Width / 2, 0));
            Point bottom = PointToScreen(new Point(ClientSize.Width / 2, (int)(BaseH * scale)));
            opacityPanel.ShowNear(top.X, top.Y, bottom.Y);
        }

        CityPicker picker;
        DateTime pickerClosedUtc = DateTime.MinValue;

        // Панель выбора города под названием города; повторный клик по названию её закрывает
        void ShowCityMenu()
        {
            HideTip();
            if (picker != null) { picker.Close(); return; }
            // клик по названию сначала закрыл панель (она теряет фокус) — не открывать её снова
            if ((DateTime.UtcNow - pickerClosedUtc).TotalMilliseconds < 300) return;

            picker = new CityPicker(this);
            picker.FormClosed += delegate { picker = null; pickerClosedUtc = DateTime.UtcNow; };
            Point top = PointToScreen(new Point(ClientSize.Width / 2, (int)(cityRect.Top * scale)));
            Point bottom = PointToScreen(new Point(ClientSize.Width / 2, (int)(cityRect.Bottom * scale)));
            picker.ShowNear(top.X, top.Y, bottom.Y);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point bp = ToBase(e.Location);
            btnHover = BtnHit(bp);
            Cursor = cityHit.Contains(bp) || btnHover ? Cursors.Hand : Cursors.Default;

            TipKind want = TipKind.None;
            if (picker != null) { HideTip(); return; }   // пока открыт выбор города — без подсказок
            if (data != null && cityHit.Contains(bp)) want = TipKind.City;
            else if (data != null && tempRect.Contains(bp)) want = TipKind.Temp;
            if (want == tipShown) return;

            HideTip();
            Point at = PointToScreen(new Point(e.X + 12, e.Y + 20));
            if (want == TipKind.City)
                tip.ShowAt("", L.T("Страна: ", "Country: ") + (data.City.Country == "" ? L.T("неизвестна", "unknown") : data.City.Country), at);
            else if (want == TipKind.Temp)
                tip.ShowAt(L.T("Как ощущается", "How it feels"), Advice.TipText(data) + Advice.SourceNote(data, DateTime.UtcNow), at);
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
            if (tipShown != TipKind.None) { tip.HideTip(); tipShown = TipKind.None; }
        }
    }
}
