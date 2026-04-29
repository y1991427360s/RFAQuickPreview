using System.IO;
using System.Runtime.InteropServices;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingBrushes = System.Drawing.Brushes;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingImageFormat = System.Drawing.Imaging.ImageFormat;
using DrawingPen = System.Drawing.Pen;
using DrawingPointF = System.Drawing.PointF;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSizeF = System.Drawing.SizeF;
using DrawingStringAlignment = System.Drawing.StringAlignment;
using DrawingStringFormat = System.Drawing.StringFormat;
using DrawingTextRenderingHint = System.Drawing.Text.TextRenderingHint;

namespace RFAQuickPreview.Desktop.Services;

public sealed class ShellThumbnailProvider
{
    private const uint SIIGBF_RESIZETOFIT = 0x00;

    public bool TryCreateThumbnail(string filePath, string outputPngPath, int size = 512)
    {
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            var shellItemId = typeof(IShellItemImageFactory).GUID;
            ThrowIfFailed(SHCreateItemFromParsingName(filePath, IntPtr.Zero, ref shellItemId, out var factory));
            ThrowIfFailed(factory.GetImage(new SIZE(size, size), SIIGBF_RESIZETOFIT, out hBitmap));

            if (hBitmap == IntPtr.Zero)
            {
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPngPath)!);
            using var bitmap = DrawingBitmap.FromHbitmap(hBitmap);
            bitmap.Save(outputPngPath, DrawingImageFormat.Png);
            File.SetLastWriteTimeUtc(outputPngPath, File.GetLastWriteTimeUtc(filePath));
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
            {
                DeleteObject(hBitmap);
            }
        }
    }

    public void CreatePlaceholder(string outputPngPath, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPngPath)!);
        using var bitmap = new DrawingBitmap(512, 512);
        using (var graphics = DrawingGraphics.FromImage(bitmap))
        {
            graphics.Clear(DrawingColor.FromArgb(238, 241, 245));
            graphics.TextRenderingHint = DrawingTextRenderingHint.ClearTypeGridFit;
            using var borderPen = new DrawingPen(DrawingColor.FromArgb(195, 202, 213), 2);
            graphics.DrawRectangle(borderPen, new DrawingRectangle(24, 24, 464, 464));

            using var font = new DrawingFont("Segoe UI", 34);
            using var brush = new System.Drawing.SolidBrush(DrawingColor.FromArgb(86, 96, 112));
            using var format = new DrawingStringFormat
            {
                Alignment = DrawingStringAlignment.Center,
                LineAlignment = DrawingStringAlignment.Center
            };
            graphics.DrawString(text, font, brush, new System.Drawing.RectangleF(new DrawingPointF(46, 136), new DrawingSizeF(420, 240)), format);
        }

        bitmap.Save(outputPngPath, DrawingImageFormat.Png);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        string pszPath,
        IntPtr pbc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, uint flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct SIZE
    {
        public readonly int cx;
        public readonly int cy;

        public SIZE(int cx, int cy)
        {
            this.cx = cx;
            this.cy = cy;
        }
    }

    private static void ThrowIfFailed(int hResult)
    {
        if (hResult < 0)
        {
            Marshal.ThrowExceptionForHR(hResult);
        }
    }
}
