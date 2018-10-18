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
using Pixel = SixLabors.ImageSharp.PixelFormats.Bgr32;

namespace Amity
{
    class Program
    {
        static unsafe void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var window = new X11Window();

            Pixel[] memory = null;
            Image<Pixel> image = null;

            IDrawingContext buffer = null;
            IDrawingContext dc = null;


            window.Resize += () =>
            {
                var client = window.ClientArea;
                dc = dc ?? window.CreateDrawingContext();
                if (client.Width * client.Height == 0) { return; }
                memory = new Pixel[client.Width * client.Height];
                image = Image.WrapMemory<Pixel>(memory, client.Width, client.Height);
                buffer?.Dispose();
                buffer = window.CreateBitmap(client.Size);
            };
            
            window.Draw += () =>
            {
                image.Mutate(a => a.DrawText("ImageSharp",
                new Font(SystemFonts.Families.First(f => f.Name == "Calibri"),
                    24, FontStyle.Regular),
                NamedColors<Pixel>.Red,
                new PointF(0, 0)));

                var client = window.ClientArea;
                {
                    // dc.Image(MemoryMarshal.Cast<Pixel, Color32>(memory.AsSpan()),
                    //     client.Size, new Point(0, 0));
                    // dc.Pen = Color.Magenta;
                    buffer.Pen = Color.Red;
                    buffer.Brush = Color.CadetBlue;
                    buffer.Rectangle(new Rectangle(10, 60, 70, 40));
                    buffer.Line(new Point(0, 0), new Point(200, 300));
                    buffer.Text(new Point(0, 50), null, "Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley of type and scrambled it to make a type specimen book. It has survived not only five centuries, but also the leap into electronic typesetting, remaining essentially unchanged. It was popularised in the 1960s with the release of Letraset sheets containing Lorem Ipsum passages, and more recently with desktop publishing software like Aldus PageMaker including versions of Lorem Ipsum.");
                    buffer.CopyTo(client, new Point(0, 0), dc);
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
            window.Show(new Rectangle(0, 0, 800, 400));
        }
    }
}
