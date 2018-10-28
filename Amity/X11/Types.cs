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
		public WmState InitialState;
		public Pixmap IconPixmap;
		public Window IconWindow;
		public int IconX;
		public int IconY;
		public Pixmap IconMask;
	}
}