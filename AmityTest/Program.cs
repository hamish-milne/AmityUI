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
            WindowBase.Register(X11Window.Factory);
            //WindowBase.Register(Win32.Factory);

            var window = new WindowBase();

            Pixel[] memory = null;
            Image<Pixel> image = null;

            IDrawingContext buffer = null;
            IDrawingContext dc = null;

            window.ClientArea = new Rectangle(0, 0, 800, 400);


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
                // image.Mutate(a => a.DrawText("ImageSharp",
                // new Font(SystemFonts.Families.First(f => f.Name == "Calibri"),
                //     24, FontStyle.Regular),
                // NamedColors<Pixel>.Red,
                // new PointF(0, 0)));

                var client = window.ClientArea;
                {
                    // dc.Image(MemoryMarshal.Cast<Pixel, Color32>(memory.AsSpan()),
                    //     client.Size, new Point(0, 0));
                    // dc.Pen = Color.Magenta;
                    buffer.Pen = Color.Red;
                    buffer.Brush = Color.CadetBlue;
                    buffer.TextColor = Color.Aquamarine;
                    buffer.Rectangle(new Rectangle(10, 60, 70, 40));
                    buffer.Line(new Point(0, 0), new Point(200, 300));
                    buffer.Text(new Point(0, 25), null, "Α α, Β β, Γ γ, Δ δ, Ε ε, Ζ ζ, Η η, Θ θ, Ι ι, Κ κ, Λ λ, Μ μ, Ν ν, Ξ ξ, Ο ο, Π π, Ρ ρ, Σ σ/ς, Τ τ, Υ υ, Φ φ, Χ χ, Ψ ψ, and Ω ω.");
                    buffer.Text(new Point(0, 50), null, "ASCII: ABCDabcd1234:@~?><!\"£$%^&*()\\ ミクがかわいい ¿No lo es? 💖 내가 어느 것을 더 좋아하는지 확실하지 않다. ");
                    buffer.Line(new Point(0, 50), new Point(100, 50));
                    buffer.Arc(new Rectangle(100, 100, 200, 100), 0, 200, ArcFillMode.Chord);
                    buffer.CopyTo(new Rectangle(new Point(0, 0), client.Size), new Point(0, 0), dc);
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
            window.IsVisible = true;
            window.Run();
        }
    }
}
