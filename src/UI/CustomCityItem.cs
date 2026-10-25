using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WeatherWidget
{
    // Пункт «своего» города в меню: при наведении справа появляется крестик для удаления
    sealed class CustomCityItem : ToolStripMenuItem
    {
        public event EventHandler RemoveClicked;
        public bool Removable = true;
        bool overX;

        // Пункт, у которого курсор сейчас над крестиком. Меню закрывается раньше, чем
        // срабатывает клик, поэтому по этому полю закрытие отменяется заранее (см. KeepOpenOnRemove).
        public static CustomCityItem Armed;

        public CustomCityItem(string text) : base(text)
        {
            ShortcutKeyDisplayString = "      ";   // запас места справа под крестик
        }

        // Не закрывать меню, когда кликнули по крестику
        public static void KeepOpenOnRemove(ToolStripDropDown d)
        {
            d.Closing += (s, e) =>
            {
                if (Armed != null && e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true;
            };
        }

        Rectangle XRect { get { return new Rectangle(Width - 28, (Height - 18) / 2, 18, 18); } }

        void SetOverX(bool o)
        {
            if (o == overX) return;
            overX = o;
            if (o) Armed = this; else if (Armed == this) Armed = null;
            Invalidate();
        }

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
}
