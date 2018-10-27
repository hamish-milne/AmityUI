namespace Amity
{
	using System;
	using System.Drawing;
	using System.Runtime.InteropServices;

	public interface IWindow
	{
		event Action<Point> MouseMove;
		event Action<int> MouseDown;
		event Action<int> KeyDown;
		event Action<int> KeyUp;
		event Action Resize;
		event Action Draw;
		Point MousePosition { get; set; }
		Rectangle WindowArea { get; set; }
		Rectangle ClientArea { get; set; }
		bool IsVisible { get; set; }
		void Run();
		IDrawingContext CreateDrawingContext();
		IDrawingContext CreateBitmap(Size size);
		void Invalidate();
		ReadOnlySpan<IFontFamily> Fonts { get; }
	}

	public enum FontSlant
	{
		Roman,
		Italic,
		Oblique
	}

	public enum FontWeight
	{
		ExtraLight = -3,
		Light = -2,
		SemiLight = -1,
		Medium = 0,
		DemiBold = 1,
		Bold = 2,
		Black = 3,
	}

	public interface IFontFamily
	{
		string Name { get; }
		ReadOnlySpan<int> FixedSizes { get; }
		bool Scalable { get; }
		IFont GetFont(float size, FontSlant slant, FontWeight weight);
	}

	// TODO: User-side glyph support?
	public interface IFont : IDisposable
	{
		IFontFamily Family { get; }
		float Size { get; }
		Rectangle MeasureText(string text);
	}

	public enum ArcFillMode
	{
		None,
		Chord,
		Slice
	}

	public interface IDrawingContext : IDisposable
	{
		Color? Brush { get; set; }
		Color? Pen { get; set; }
		Color? TextColor { get; set; }
		float LineWidth { get; set; }
		ArcFillMode ArcFillMode { get; set; }
		IFont Font { get; set; }

		void Polygon(ReadOnlySpan<Point> points);
		void Line(ReadOnlySpan<Point> points);
		void Rectangle(ReadOnlySpan<Rectangle> rects);
		void Arc(Rectangle rect, float angleA, float angleB);
		void Text(Point position, string text);
		void Image(Span<Color32> data, Size size, Point destination);
		void CopyTo(Rectangle srcRect, Point dstPos, IDrawingContext dst);
	}

	public static class Extensions
	{
		public static void Line(this IDrawingContext context, Point a, Point b)
		{
			Span<Point> points = stackalloc Point[] { a, b };
			context.Line(points);
		}

		public static void Rectangle(this IDrawingContext context, Rectangle rect)
		{
			Span<Rectangle> rects = stackalloc Rectangle[] { rect };
			context.Rectangle(rects);
		}

		public static IFontFamily Font(this IWindow window, string name)
		{
			foreach (var f in window.Fonts)
			{
				if (f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					return f;
				}
			}
			return null;
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct Color32
	{
		public byte B;
		public byte G;
		public byte R;
		public byte A;

		public static implicit operator Color(Color32 input)
			=> Color.FromArgb(input.A, input.R, input.G, input.B);
		
		public static implicit operator Color32(Color input)
			=> new Color32 { R = input.R, G = input.G, B = input.B, A = input.A };
	}
}