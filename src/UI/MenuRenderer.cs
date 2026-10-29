using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WeatherWidget
{
    // Меню по правой кнопке и у значка в трее — в стиле панелей виджета (Theme.DrawPanel):
    // тёмно-синий фон, белый текст, мягкая подсветка пункта, бирюзовые галочки.
    // Стандартное меню WinForms светлое и выглядело чужим рядом с карточкой.
    sealed class MenuRenderer : ToolStripProfessionalRenderer
    {
        static readonly Color Back = Color.FromArgb(30, 42, 62);
        static readonly Color Hover = Color.FromArgb(50, 64, 88);
        static readonly Color Border = Color.FromArgb(64, 76, 96);
        static readonly Color Line = Color.FromArgb(52, 64, 84);
        static readonly Color Text = Color.FromArgb(235, 240, 246);
        static readonly Color Dim = Color.FromArgb(120, 132, 150);

        public MenuRenderer() : base(new Colors()) { RoundedEdges = false; }

        // Применить к меню и всем его подменю
        public static void Apply(ContextMenuStrip menu)
        {
            MenuRenderer r = new MenuRenderer();
            menu.Renderer = r;
            ApplyTo(menu.Items, r);
        }

        static void ApplyTo(ToolStripItemCollection items, MenuRenderer r)
        {
            foreach (ToolStripItem i in items)
            {
                i.ForeColor = Text;
                i.Padding = new Padding(i.Padding.Left, 3, i.Padding.Right, 3);   // пункты чуть свободнее
                ToolStripMenuItem m = i as ToolStripMenuItem;
                if (m != null && m.HasDropDownItems)
                {
                    m.DropDown.Renderer = r;
                    ApplyTo(m.DropDownItems, r);
                }
            }
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(Back)) e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            Rectangle r = new Rectangle(Point.Empty, e.ToolStrip.Size);
            r.Width--;
            r.Height--;
            using (Pen p = new Pen(Border)) e.Graphics.DrawRectangle(p, r);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }   // поле под галочки — того же цвета, что фон

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            Rectangle r = new Rectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath p = Theme.RoundRect(r, 5))
            using (SolidBrush b = new SolidBrush(Hover))
                e.Graphics.FillPath(b, p);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Text : Dim;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item.Enabled ? Text : Dim;
            base.OnRenderArrow(e);
        }

        // Галочка — бирюзовая, без рамки вокруг
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle r = e.ImageRectangle;
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, s = r.Height / 16f;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen p = new Pen(Theme.Accent, 2f * s))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                e.Graphics.DrawLines(p, new PointF[] { new PointF(cx - 5 * s, cy), new PointF(cx - 1.5f * s, cy + 3.5f * s), new PointF(cx + 5 * s, cy - 4 * s) });
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (Pen p = new Pen(Line)) e.Graphics.DrawLine(p, 10, y, e.Item.Width - 10, y);
        }

        // Цвета, которые базовый класс берёт сам (например, фон подменю при открытии)
        sealed class Colors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Back; } }
            public override Color ImageMarginGradientBegin { get { return Back; } }
            public override Color ImageMarginGradientMiddle { get { return Back; } }
            public override Color ImageMarginGradientEnd { get { return Back; } }
            public override Color MenuBorder { get { return Border; } }
            public override Color MenuItemBorder { get { return Hover; } }
            public override Color MenuItemSelected { get { return Hover; } }
            public override Color SeparatorDark { get { return Line; } }
            public override Color SeparatorLight { get { return Back; } }
        }
    }
}
