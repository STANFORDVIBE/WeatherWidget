using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WeatherWidget
{
    // Всплывающая подсказка в стиле виджета: тёмная панель со сглаженными углами и тенью,
    // бирюзовый заголовок, белый текст. Своя, а не ToolTip: стандартная подсказка WinForms
    // не показывается, пока окно виджета не активно (а виджет обычно не активен).
    // Окно никогда не получает фокус и пропускает мышь насквозь.
    sealed class TipWindow : Form
    {
        const int Shadow = Theme.PanelShadow;
        const int Pad = 12, Gap = 6, Radius = 12, MaxTextW = 340;

        readonly LayeredSurface surface = new LayeredSurface();
        readonly Font fTitle = new Font("Segoe UI Semibold", 13, GraphicsUnit.Pixel);
        readonly Font fBody = new Font("Segoe UI", 12, GraphicsUnit.Pixel);
        string title = "", body = "";
        SizeF titleSize, bodySize;
        Size box;                // размер в базовых координатах (вместе с полем под тень)
        float dpi = 1f;          // масштаб экрана, где показана подсказка
        bool shown;

        public TipWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW |
                              NativeMethods.WS_EX_TOPMOST | NativeMethods.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            // подсказка «прозрачна» для мыши — курсор над ней не уводит мышь с виджета
            if (m.Msg == NativeMethods.WM_NCHITTEST) { m.Result = (IntPtr)NativeMethods.HTTRANSPARENT; return; }
            if (m.Msg == NativeMethods.WM_MOUSEACTIVATE) { m.Result = (IntPtr)NativeMethods.MA_NOACTIVATE; return; }
            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { surface.Dispose(); fTitle.Dispose(); fBody.Dispose(); }
            base.Dispose(disposing);
        }

        // Размер окна под текст (вместе с полем под тень)
        Size SetText(string t, string b)
        {
            title = t ?? "";
            body = b ?? "";
            using (Bitmap tmp = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(tmp))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                titleSize = title == "" ? SizeF.Empty : g.MeasureString(title, fTitle, MaxTextW);
                bodySize = g.MeasureString(body, fBody, MaxTextW);   // длинные строки переносятся
            }
            int w = (int)Math.Ceiling(Math.Max(titleSize.Width, bodySize.Width)) + 2 * Pad;
            int h = (int)Math.Ceiling(bodySize.Height + (title == "" ? 0 : titleSize.Height + Gap)) + 2 * Pad;
            box = new Size(w + 2 * Shadow, h + 2 * Shadow);
            return box;
        }

        // Показать подсказку так, чтобы её угол был у точки экрана (обычно чуть ниже и правее курсора).
        // Показ — напрямую через Windows, а не Form.Show(): WinForms при показе передаёт форме фокус,
        // и открытая панель выбора города теряла его и закрывалась.
        public void ShowAt(string t, string b, Point at)
        {
            dpi = Dpi.AtPoint(at);
            SetText(t, b);
            Size size = new Size(Dpi.Px(box.Width, dpi), Dpi.Px(box.Height, dpi));
            int shadow = Dpi.Px(Shadow, dpi), panelW = size.Width - 2 * shadow, panelH = size.Height - 2 * shadow;

            // не вылезать за край экрана; не хватает места снизу — показать над курсором
            Rectangle wa = Screen.FromPoint(at).WorkingArea;
            int px = Math.Max(wa.Left, Math.Min(at.X, wa.Right - panelW));
            int py = at.Y + panelH <= wa.Bottom ? at.Y : at.Y - panelH - Dpi.Px(30, dpi);
            py = Math.Max(wa.Top, py);
            Bounds = new Rectangle(px - shadow, py - shadow, size.Width, size.Height);

            surface.Present(Handle, Width, Height, 255, Render, dpi);
            NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, Left, Top, Width, Height, NativeMethods.SWP_NOACTIVATE);
            NativeMethods.ShowWindow(Handle, NativeMethods.SW_SHOWNOACTIVATE);
            shown = true;
        }

        public void HideTip()
        {
            if (!shown) return;
            NativeMethods.ShowWindow(Handle, NativeMethods.SW_HIDE);
            shown = false;
        }

        // Картинка подсказки без показа окна — для README и тестов
        internal Bitmap RenderPreview(string t, string b)
        {
            Size = SetText(t, b);
            Bitmap bmp = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp)) Render(g);
            return bmp;
        }

        void Render(Graphics g)
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            Theme.DrawPanel(g, new RectangleF(Shadow, Shadow, box.Width - 2 * Shadow, box.Height - 2 * Shadow), Radius);

            float x = Shadow + Pad, y = Shadow + Pad;
            if (title != "")
            {
                using (SolidBrush accent = new SolidBrush(Theme.Accent))
                    g.DrawString(title, fTitle, accent, new RectangleF(x, y, MaxTextW, titleSize.Height + 1));
                y += titleSize.Height + Gap;
            }
            using (SolidBrush text = new SolidBrush(Color.FromArgb(232, 255, 255, 255)))
                g.DrawString(body, fBody, text, new RectangleF(x, y, MaxTextW, bodySize.Height + 1));
        }
    }
}
