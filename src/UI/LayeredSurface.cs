using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WeatherWidget
{
    // Буфер для окна с попиксельной прозрачностью (layered window).
    // Кадр рисуется средствами GDI+ прямо в память 32-битного DIB с альфа-каналом
    // и отдаётся Windows через UpdateLayeredWindow — поэтому края карточки сглажены,
    // а всё вне карточки прозрачно и пропускает клики.
    sealed class LayeredSurface : IDisposable
    {
        IntPtr memDC, dib, oldObj;
        Bitmap bitmap;
        int width, height;

        // scale — масштаб экрана: render рисует в базовых координатах, кадр — в физических пикселях
        public void Present(IntPtr hwnd, int w, int h, byte alpha, Action<Graphics> render, float scale = 1f)
        {
            if (w <= 0 || h <= 0) return;
            Ensure(w, h);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                if (scale != 1f) g.ScaleTransform(scale, scale);
                render(g);
            }

            NativeMethods.SIZE size = new NativeMethods.SIZE { CX = w, CY = h };
            NativeMethods.POINT src = new NativeMethods.POINT();
            NativeMethods.BLENDFUNCTION blend = new NativeMethods.BLENDFUNCTION
            {
                BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = NativeMethods.AC_SRC_ALPHA
            };
            NativeMethods.UpdateLayeredWindow(hwnd, IntPtr.Zero, IntPtr.Zero, ref size, memDC, ref src, 0, ref blend, NativeMethods.ULW_ALPHA);
        }

        void Ensure(int w, int h)
        {
            if (bitmap != null && w == width && h == height) return;
            Free();
            NativeMethods.BITMAPINFOHEADER bi = new NativeMethods.BITMAPINFOHEADER();
            bi.biSize = Marshal.SizeOf(typeof(NativeMethods.BITMAPINFOHEADER));
            bi.biWidth = w;
            bi.biHeight = -h;   // строки сверху вниз, как у Bitmap
            bi.biPlanes = 1;
            bi.biBitCount = 32;
            memDC = NativeMethods.CreateCompatibleDC(IntPtr.Zero);
            IntPtr bits;
            dib = NativeMethods.CreateDIBSection(memDC, ref bi, 0, out bits, IntPtr.Zero, 0);
            oldObj = NativeMethods.SelectObject(memDC, dib);
            bitmap = new Bitmap(w, h, w * 4, PixelFormat.Format32bppPArgb, bits);
            width = w;
            height = h;
        }

        void Free()
        {
            if (bitmap != null) { bitmap.Dispose(); bitmap = null; }
            if (memDC != IntPtr.Zero)
            {
                NativeMethods.SelectObject(memDC, oldObj);
                NativeMethods.DeleteObject(dib);
                NativeMethods.DeleteDC(memDC);
                memDC = IntPtr.Zero;
            }
        }

        public void Dispose() { Free(); }
    }
}
