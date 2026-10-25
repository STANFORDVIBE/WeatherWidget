using System;
using System.Drawing;
using System.Windows.Forms;

namespace WeatherWidget
{
    // Всплывающая подсказка в стиле системной. Своя, потому что стандартный ToolTip
    // WinForms не показывается, пока окно виджета не активно (а виджет обычно не активен).
    sealed class TipWindow : Form
    {
        const int Pad = 8;
        string title = "", body = "";
        readonly Font fTitle = new Font("Segoe UI", 9f, FontStyle.Bold);
        readonly Font fBody = new Font("Segoe UI", 9f);

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
                cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST | NativeMethods.WS_EX_NOACTIVATE;
                cp.ClassStyle |= NativeMethods.CS_DROPSHADOW;   // тень, как у системной подсказки
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

        bool shown;

        // Показ и скрытие — напрямую через Windows, а не Form.Show(): WinForms при показе формы
        // передаёт ей фокус, и открытая панель выбора города теряла его и закрывалась.
        void ShowNoActivate()
        {
            NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, Left, Top, Width, Height, NativeMethods.SWP_NOACTIVATE);
            NativeMethods.ShowWindow(Handle, NativeMethods.SW_SHOWNOACTIVATE);
            Invalidate();
            shown = true;
        }

        public void HideTip()
        {
            if (!shown) return;
            NativeMethods.ShowWindow(Handle, NativeMethods.SW_HIDE);
            shown = false;
        }

        Size SetText(string t, string b)
        {
            title = t;
            body = b;
            Size ts = title == "" ? Size.Empty : TextRenderer.MeasureText(title, fTitle);
            Size bs = TextRenderer.MeasureText(body, fBody);
            return new Size(Math.Max(ts.Width, bs.Width) + Pad * 2, bs.Height + Pad * 2 + (title == "" ? 0 : ts.Height + 4));
        }

        // Картинка подсказки без показа окна — для README
        internal Bitmap RenderPreview(string t, string b)
        {
            Size = SetText(t, b);
            Bitmap bmp = new Bitmap(Width, Height);
            DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
            return bmp;
        }

        // Показать подсказку у точки экрана (обычно чуть ниже и правее курсора)
        public void ShowAt(string t, string b, Point at)
        {
            Size size = SetText(t, b);
            int w = size.Width, h = size.Height;

            // не вылезать за край экрана
            Rectangle wa = Screen.FromPoint(at).WorkingArea;
            int x = Math.Max(wa.Left, Math.Min(at.X, wa.Right - w));
            int y = at.Y + h <= wa.Bottom ? at.Y : at.Y - h - 30;
            y = Math.Max(wa.Top, y);
            Bounds = new Rectangle(x, y, w, h);
            ShowNoActivate();
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
}
