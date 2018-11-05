namespace Amity.X11
{
	using System;
	using System.Runtime.InteropServices;

	public enum Window : uint { }
	public enum Atom : uint { }
	public enum Pixmap : uint { }
	public enum GContext : uint { }
	public enum Visual : uint { }
	public enum Colormap : uint { }
	public enum Drawable : uint { } // TODO: implicit cast from Pixmap and Window
	public enum Font : uint { }

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct IconSize
	{
		public uint MinWidth;
		public uint MinHeight;
		public uint MaxWidth;
		public uint MaxHeight;
		public uint WidthInc;
		public uint HeightInc;
	}

	public enum State : uint
	{
		Withdrawn = 0,
		Normal = 1,
		Iconic = 3
	}

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct WmState
	{
		public State State;
		public Window Icon;
	}

	[Flags]
	public enum HintFlags : uint
	{
		Input = (1 << 0),
		State = (1 << 1),
		IconPixmap = (1 << 2),
		IconWindow = (1 << 3),
		IconPosition = (1 << 4),
		IconMask = (1 << 5),
		WindowGroup = (1 << 6),
		[Obsolete] Message = (1 << 7),
		Urgency = (1 << 8)
	}

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct WmHints
	{
		public HintFlags Flags;
		public uint Input; // TODO: Flags here?
		public State InitialState;
		public Pixmap IconPixmap;
		public Window IconWindow;
		public int IconX;
		public int IconY;
		public Pixmap IconMask;
	}

	[Flags]
	public enum SizeHintsFlags : uint
	{
		USPosition = (1 << 0),
		USSize = (1 << 1),
		PPosition = (1 << 2),
		PSize = (1 << 3),
		PMinSize = (1 << 4),
		PMaxSize = (1 << 5),
		PResizeInc = (1 << 6),
		PAspect = (1 << 7),
		PBaseSize = (1 << 8),
		PWinGravity = (1 << 9)
	}

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct AspectRatio
	{
		public int Numerator;
		public int Denominator;
	}

	public enum Gravity : uint
	{
		Unmap,
		NorthWest,
		North,
		NorthEast,
		West,
		Center,
		East,
		SouthWest,
		South,
		SouthEast
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct WmSizeHints
	{
		[FieldOffset(0)] public SizeHintsFlags Flags;
		[FieldOffset(20)] public int MinWidth;
		[FieldOffset(24)] public int MinHeight;
		[FieldOffset(28)] public int MaxWidth;
		[FieldOffset(32)] public int MaxHeight;
		[FieldOffset(36)] public int WidthInc;
		[FieldOffset(40)] public int HeightInc;
		[FieldOffset(44)] public AspectRatio MinAspect;
		[FieldOffset(52)] public AspectRatio MaxAspect;
		[FieldOffset(60)] public int BaseWidth;
		[FieldOffset(64)] public int BaseHeight;
		[FieldOffset(68)] public Gravity WinGravity;
	}
}