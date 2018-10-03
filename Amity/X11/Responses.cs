namespace Amity.X11
{
	using System;
	using System.Runtime.InteropServices;


	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ConnectionResponse
	{
		public byte Code;
		public byte ReasonLengthBytes;
		public ushort MajorVersion;
		public ushort MinorVersion;
		public ushort DataLengthWords;
	}

	public enum ByteOrder : byte
	{
		LSBFirst = 0,
		MSBFirst = 1,
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct ConnectionSuccess
	{
		public uint ReleaseNumber;
		public uint ResourceIdBase;
		public uint ResourceIdMask;
		public uint MotionBufferSize;
		public ushort VendorLength;
		public ushort MaxRequestLength;
		public byte ScreenCount;
		public byte FormatCount;
		public ByteOrder ImageByteOrder;
		public ByteOrder BitmapBitOrder;
		public byte BitmapScanlineUnit;
		public byte BitmapScanlinePad;
		public byte MinKeycode;
		public byte MaxKeycode;
		private uint _unused;
	}

	[StructLayout(LayoutKind.Sequential, Size = 8)]
	public struct Format
	{
		public byte Depth;
		public byte BitsPerPixel;
		public byte ScanlinePad;
	}

	public enum BackingStoreType : byte
	{
		Never = 0,
		WhenMapped = 1,
		Always = 2
	}

	[Flags]
	public enum Event : uint
	{
		KeyPress = (1 << 0),
		KeyRelease = (1 << 1),
		ButtonPress = (1 << 2),
		ButtonRelease = (1 << 3),
		EnterWindow = (1 << 4),
		LeaveWindow = (1 << 5),
		PointerMotion = (1 << 6),
		PointerMotionHint = (1 << 7),
		Button1Motion = (1 << 8),
		Button2Motion = (1 << 9),
		Button3Motion = (1 << 10),
		Button4Motion = (1 << 11),
		Button5Motion = (1 << 12),
		ButtonMotion = (1 << 13),
		KeymapState = (1 << 14),
		Exposure = (1 << 15),
		VisibilityChange = (1 << 16),
		StructureNotify = (1 << 17),
		ResizeRedirect = (1 << 18),
		SubstructureNotify = (1 << 19),
		SubstructureRedirect = (1 << 20),
		FocusChange = (1 << 21),
		PropertyChange = (1 << 22),
		ColormapChange = (1 << 23),
		OwnerGrabButton = (1 << 24),
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct Screen
	{
		public uint Root;
		public uint DefaultColormap;
		public Color32 WhitePixel;
		public Color32 BlackPixel;
		public uint CurrentInputMasks;
		public ushort Width;
		public ushort Height;
		public ushort WidthMM;
		public ushort HeightMM;
		public ushort MinMaps;
		public ushort MaxMaps;
		public uint RootVisual;
		public BackingStoreType BackingStores;
		public byte SaveUnders;
		public byte RootDepth;
		public byte DepthCount;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct Depth
	{
		public byte DepthValue;
		public ushort VisualsCount;
		private uint _unused;
	}

	public enum ColorType : byte
	{
		StaticGray,
		GrayScale,
		StaticColor,
		PseudoColor,
		TrueColor,
		DirectColor
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct VisualType
	{
		public uint VisualId;
		public ColorType Class;
		public byte BitsPerRgbValue;
		public ushort ColormapEntries;
		public uint RedMask;
		public uint GreenMask;
		public uint BlueMask;
		private uint _unused;
	}

	public enum ErrorCode : byte
	{
		Request,
		Value,
		Window,
		Pixmap,
		Atom,
		Cursor,
		Font,
		Match,
		Drawable,
		Access,
		Alloc,
		Colormap,
		GContext,
		IDChoice,
		Name,
		Length,
		Implementation,
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct Error
	{
		public ErrorCode Code;
		public ushort SequenceNumber;
		public uint Data;
		public ushort MinorOpcode;
		public byte MajorOpcode;

		public override string ToString()
			=> $"X11 {Code} Error at frame {SequenceNumber}, which used "
			+ $"opcode {MajorOpcode}:{MinorOpcode}. The value in question was {Data}.";
	}

	[Flags]
	public enum KeyMask : ushort
	{
		Shift = (1 << 0),
		Lock = (1 << 1),
		Control = (1 << 2),
		Mod1 = (1 << 3),
		Mod2 = (1 << 4),
		Mod3 = (1 << 5),
		Mod4 = (1 << 6),
		Mod5 = (1 << 7),
		Button1 = (1 << 8),
		Button2 = (1 << 9),
		Button3 = (1 << 10),
		Button4 = (1 << 11),
		Button5 = (1 << 12),
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct KeyEvent
	{
		private byte _opcode;
		public bool IsDown => _opcode == 2 || _opcode == 4;
		public byte Keycode;
		public ushort SequenceNumber;
		public uint Time;
		public uint RootWindow;
		public uint EventWindow;
		public uint ChildWindow;
		public ushort RootX;
		public ushort RootY;
		public ushort EventX;
		public ushort EventY;
		public KeyMask State;
		[MarshalAs(UnmanagedType.U1)] public bool SameScreen;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ResizeRequestEvent
	{
		private ushort _unused;
		public uint Window;
		public ushort Width;
		public ushort Height;
	}
}