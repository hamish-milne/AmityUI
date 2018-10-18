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
		// TODO: Make all these settable
		Point MousePosition { get; }
		Rectangle WindowArea { get; }
		Rectangle ClientArea { get; }
		void Show(Rectangle rect); // TODO: Don't take rect here. Property instead?
		IDrawingContext CreateDrawingContext();
		IDrawingContext CreateBitmap(Size size);
		void Invalidate();
	}

	public interface IDrawingContext : IDisposable
	{
		Color? Brush { get; set; }
		Color? Pen { get; set; }
		void Polygon(ReadOnlySpan<Point> points);
		void Line(Point a, Point b);
		void Rectangle(Rectangle rect);
		void ArcChord(Rectangle rect, float angleA, float angleB);
		void ArcSlice(Rectangle rect, float angleA, float angleB);
		// TODO: Font object
		// TODO: User-side glyph support?
		void Text(Point position, string font, string text);
		ReadOnlySpan<string> Fonts { get; }
		void Image(Span<Color32> data, Size size, Point destination);
		void CopyTo(Rectangle srcRect, Point dstPos, IDrawingContext dst);
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

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct Color24
	{
		public byte B;
		public byte G;
		public byte R;

		public static implicit operator Color(Color24 input)
			=> Color.FromArgb(input.R, input.G, input.B);
		
		public static implicit operator Color24(Color input)
			=> new Color24 { R = input.R, G = input.G, B = input.B };
	}
}