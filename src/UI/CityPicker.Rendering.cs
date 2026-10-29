using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace WeatherWidget
{
    // Панель выбора города: отрисовка (поле поиска, строки городов, подсказка вместо списка)
    sealed partial class CityPicker
    {
        readonly Font fSearch = new Font("Segoe UI", 14, GraphicsUnit.Pixel);
        readonly Font fName = new Font("Segoe UI Semibold", 14, GraphicsUnit.Pixel);
        readonly Font fSub = new Font("Segoe UI", 11, GraphicsUnit.Pixel);
        readonly Font fStatus = new Font("Segoe UI", 12, GraphicsUnit.Pixel);
        readonly StringFormat ellipsis = new StringFormat
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        readonly StringFormat centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        // ---------- Отрисовка ----------

        void DisposeGraphics()
        {
            fSearch.Dispose();
            fName.Dispose();
            fSub.Dispose();
            fStatus.Dispose();
            ellipsis.Dispose();
            centered.Dispose();
        }

        void Present()
        {
            if (!IsHandleCreated || !Visible || IsDisposed) return;
            surface.Present(Handle, Width, Height, (byte)fade, Render, dpi);
        }

        // Картинка панели без показа окна — для тестов и README
        internal Bitmap RenderPreview(string previewQuery, List<GeoResult> results)
        {
            query = previewQuery ?? "";
            if (results != null) ShowResults(results); else ShowOwnCities();
            caretOn = true;
            Size = new Size(PanelW + 2 * Shadow, PanelHeight() + 2 * Shadow);
            Bitmap b = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b)) Render(g);
            return b;
        }

        void Render(Graphics g)
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            int panelH = PanelHeight();
            RectangleF panel = new RectangleF(Shadow, Shadow, PanelW, panelH);

            Theme.DrawPanel(g, panel, Radius);
            DrawSearch(g);

            float sepY = ListTop - Pad / 2f - 1;
            using (Pen sep = new Pen(Color.FromArgb(30, 255, 255, 255)))
                g.DrawLine(sep, Shadow + Pad, sepY, Shadow + PanelW - Pad, sepY);

            if (status != null)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(170, 255, 255, 255)))
                    g.DrawString(status, fStatus, b, new RectangleF(Shadow, ListTop, PanelW, StatusH - 6), centered);
                return;
            }
            for (int i = 0; i < rows.Count; i++) DrawRow(g, i);
        }

        void DrawSearch(Graphics g)
        {
            Rectangle box = new Rectangle(Shadow + Pad, Shadow + Pad, PanelW - 2 * Pad, SearchH);
            using (GraphicsPath p = Theme.RoundRect(box, 10))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(26, 255, 255, 255)))
                g.FillPath(b, p);

            // лупа
            float cx = box.X + 15, cy = box.Y + box.Height / 2f - 1;
            using (Pen lens = new Pen(Color.FromArgb(170, 255, 255, 255), 1.6f))
            {
                lens.StartCap = LineCap.Round;
                lens.EndCap = LineCap.Round;
                g.DrawEllipse(lens, cx - 5.5f, cy - 5.5f, 11, 11);
                g.DrawLine(lens, cx + 4, cy + 4, cx + 8, cy + 8);
            }

            float tx = box.X + 30, avail = box.Width - 40;
            float ty = box.Y + (box.Height - fSearch.GetHeight(g)) / 2;
            StringFormat typo = StringFormat.GenericTypographic;
            if (query == "")
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 255, 255, 255)))
                    g.DrawString(L.T("Поиск города", "Search for a city"), fSearch, b, tx, ty, typo);
            }
            GraphicsState st = g.Save();
            g.SetClip(new RectangleF(tx - 1, box.Y, avail + 2, box.Height));
            float tw = query == "" ? 0 : g.MeasureString(query, fSearch, PointF.Empty, typo).Width;
            float shift = Math.Max(0, tw - avail);   // длинный текст сдвигается влево, курсор всегда виден
            if (query != "") g.DrawString(query, fSearch, Brushes.White, tx - shift, ty, typo);
            if (caretOn && ContainsFocus || caretOn && !Visible)
                using (Pen caret = new Pen(Color.FromArgb(220, 255, 255, 255), 1.4f))
                    g.DrawLine(caret, tx + tw - shift + 1, box.Y + 10, tx + tw - shift + 1, box.Bottom - 10);
            g.Restore(st);
        }

        void DrawRow(Graphics g, int i)
        {
            Row r = rows[i];
            Rectangle rc = RowRect(i);
            if (i == hover || i == selected)
                using (GraphicsPath p = Theme.RoundRect(rc, 9))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(i == hover ? 26 : 18, 255, 255, 255)))
                    g.FillPath(b, p);

            if (r.Current)
                using (SolidBrush dot = new SolidBrush(Theme.Accent))
                    g.FillEllipse(dot, rc.X + 10, rc.Y + rc.Height / 2f - 4, 8, 8);

            float textX = rc.X + 26, textW = rc.Width - 26 - (r.Removable ? 34 : 8);
            g.DrawString(r.Title, fName, Brushes.White, new RectangleF(textX, rc.Y + 5, textW, 20), ellipsis);
            if (!string.IsNullOrEmpty(r.Subtitle))
                using (SolidBrush sub = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
                    g.DrawString(r.Subtitle, fSub, sub, new RectangleF(textX, rc.Y + 24, textW, 16), ellipsis);

            if (r.Removable && i == hover)
            {
                Rectangle x = XRect(rc);
                using (SolidBrush b = new SolidBrush(hoverX ? Color.FromArgb(200, 214, 72, 72) : Color.FromArgb(28, 255, 255, 255)))
                    g.FillEllipse(b, x);
                using (Pen p = new Pen(Color.FromArgb(230, 255, 255, 255), 1.6f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    const int m = 8;
                    g.DrawLine(p, x.Left + m, x.Top + m, x.Right - m, x.Bottom - m);
                    g.DrawLine(p, x.Right - m, x.Top + m, x.Left + m, x.Bottom - m);
                }
            }
        }
    }
}
