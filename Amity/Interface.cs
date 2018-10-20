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
		void Polygon(ReadOnlySpan<Point> points);
		void Line(Point a, Point b);
		void Rectangle(Rectangle rect);
		void Arc(Rectangle rect, float angleA, float angleB,
			ArcFillMode fillMode = ArcFillMode.None);
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
}