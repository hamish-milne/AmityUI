namespace Amity
{
	using System;
	using System.Drawing;
	using System.Numerics;
	using System.Runtime.InteropServices;

	public abstract class Window
	{
		public abstract event Action<Vector2> MouseMove;
		public abstract event Action<int> MouseDown;
		public abstract event Action<int> KeyDown;
		public abstract event Action<int> KeyUp;
		public abstract event Action Paint;
		public abstract Span<Color32> Buffer { get; }
		public abstract Rectangle WindowArea { get; }
		public abstract Rectangle ClientArea { get; }
		public abstract void Show();
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