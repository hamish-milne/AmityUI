using System;
using System.Threading.Tasks;
using System.Drawing;
//using SkiaSharp;
using System.Runtime.InteropServices;
using PointF = SixLabors.Primitives.PointF;
using System.Linq;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Amity
{
    class Program
    {
        static unsafe void Main(string[] args)
        {
            X11Window.EndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 6000);
            Console.WriteLine("Hello World!");
            var window = new X11Window();
            window.Draw += () =>
            {
                var client = window.ClientArea;
                if (client.Width * client.Height == 0) { return; }
                var memory = new Bgra32[client.Width * client.Height];
                using (var image = Image.WrapMemory<Bgra32>(memory, client.Width, client.Height))
                {
                    image.Mutate(a => a.DrawText("ImageSharp",
                        new Font(SystemFonts.Families.First(), 24f, FontStyle.Regular),
                        NamedColors<Bgra32>.Red,
                        new PointF(0, 0)));
                }
                using (var dc = window.GetDrawingContext())
                {
                    //MemoryMarshal.Cast<Bgra32, Color32>(memory.AsSpan()).AlphaPremultiply();
                    dc.Image(MemoryMarshal.Cast<Bgra32, Color32>(memory.AsSpan()),
                        client.Size, new Point(0, 0));
                }
                //MemoryMarshal.Cast<Rgba32, Color32>(memory.AsSpan()).CopyTo(window.Buffer);

                /*var info = new SKImageInfo(client.Width, client.Height);
                fixed (Color32* ptr = window.Buffer)
                using (var surface = SKSurface.Create(info, (IntPtr)ptr, client.Width*4))
                {
                    // the the canvas and properties
                    var canvas = surface.Canvas;

                    // make sure the canvas is blank
                    canvas.Clear(SKColors.White);

                    // draw some text
                    var paint = new SKPaint
                    {
                        Color = SKColors.Black,
                        IsAntialias = true,
                        Style = SKPaintStyle.Fill,
                        TextAlign = SKTextAlign.Center,
                        TextSize = 24
                    };
                    var coord = new SKPoint(info.Width / 2, (info.Height + paint.TextSize) / 2);
                    canvas.DrawText("SkiaSharp", coord, paint);
                }*/

                // TEMP
                /*Parallel.For(0, window.Buffer.Length, i =>
                {
                    var t = (i / 400) % (256 * 3);
                    var r = t < 256 ? t : (t < 512 ? 512 - t : 0);
                    var g = t < 256 ? 0 : (t < 512 ? t - 256 : 768 - t);
                    var b = t < 256 ? 256 - t : (t < 512 ? 0 : t - 512);
                    window.Buffer[i] = new Color32
                    {
                        R = (byte)r,
                        G = (byte)g,
                        B = (byte)b,
                        A = 255,
                    };
                });*/
            };
            /*window.Draw += () =>
            {
                using (var dc = window.GetDrawingContext())
                {
                    dc.Pen = Color.Red;
                    dc.Line(new Point(0, 0), new Point(200, 300));
                    dc.Brush = Color.AliceBlue;
                    dc.Rectangle(new Rectangle(new Point(50, 200), new Size(300, 200)));
                }
            };*/
            window.MouseMove += pos =>
            {
                
                //window.Buffer[pos.X + pos.Y*window.ClientArea.Width] = Color.Green;
                //window.Invalidate();
            };
            window.Show(new Rectangle(0, 0, 200, 100));
        }
    }
}
