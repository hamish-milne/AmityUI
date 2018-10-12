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
		event Action Paint;
		event Action Draw;
		Point MousePosition { get; }
		IntPtr BufferPtr { get; }
		Span<Color32> Buffer { get; }
		Rectangle WindowArea { get; }
		Rectangle ClientArea { get; }
		void Show(Rectangle rect);
		IDrawingContext GetDrawingContext();
		void Invalidate();
	}

	public interface IDrawingContext : IDisposable
	{
		Color? Brush { get; set; }
		Color? Pen { get; set; }
		void BeginPolygon();
		void PushPoint(Point next);
		void EndPolygon(bool forceClose);
		void Line(Point a, Point b);
		void Rectangle(Rectangle rect);
		void ArcChord(Rectangle rect, float angleA, float angleB);
		void ArcSlice(Rectangle rect, float angleA, float angleB);
		void Text(Point position, string font, string text);
		ReadOnlySpan<string> Fonts { get; }
		void Image(Span<byte> data, Size size, Point destination);
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct Color32
	{
		public byte R;
		public byte G;
		public byte B;
		public byte A;

		public static implicit operator Color(Color32 input)
			=> Color.FromArgb(input.A, input.R, input.G, input.B);
		
		public static implicit operator Color32(Color input)
			=> new Color32 { R = input.R, G = input.G, B = input.B, A = input.A };
	}
}