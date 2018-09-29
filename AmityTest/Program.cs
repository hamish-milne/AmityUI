using System;
using System.Threading.Tasks;
using System.Drawing;
using SkiaSharp;

namespace Amity
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var window = new WindowBase();
            window.Paint += () =>
            {
                /*var client = window.ClientArea;
                var info = new SKImageInfo(client.Width, client.Height);
                using (var surface = SKSurface.Create(info, window.BufferPtr, client.Width*4))
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
            window.Draw += () =>
            {
                using (var dc = window.GetDrawingContext())
                {
                    dc.Pen = Color.Red;
                    dc.Line(new Point(0, 0), new Point(200, 300));
                    dc.Brush = Color.AliceBlue;
                    dc.Rectangle(new Rectangle(new Point(50, 200), new Size(300, 200)));
                }
            };
            window.MouseMove += pos =>
            {
                window.Buffer[pos.X + pos.Y*window.ClientArea.Width] = Color.Green;
                window.Invalidate();
            };
            window.Show();
        }
    }
}
