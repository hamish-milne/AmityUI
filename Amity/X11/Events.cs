namespace Amity.X11
{
	using System;
	using System.Runtime.InteropServices;

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

	public interface X11Event
	{
		byte[] Opcodes { get; }
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct Error : X11Event
	{
		public byte[] Opcodes => new byte[]{0};
		private byte _opcode;
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
	public struct PositionalEvent
	{
		public ushort SequenceNumber;
		public uint Time;
		public uint RootWindow;
		public uint EventWindow;
		public uint ChildWindow;
		public ushort RootX;
		public ushort RootY;
		public ushort EventX;
		public ushort EventY;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct KeyEvent : X11Event
	{
		public byte[] Opcodes => new byte[]{2, 3, 4, 5};
		private byte _opcode;
		public bool IsDown => _opcode == 2 || _opcode == 4;
		public byte Keycode;
		public PositionalEvent Data;
		public KeyMask State;
		[MarshalAs(UnmanagedType.U1)] public bool SameScreen;
	}

	public enum MotionType : byte
	{
		Normal,
		Hint
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct MotionNotify : X11Event
	{
		public byte[] Opcodes => new byte[]{6};
		private byte _opcode;
		public MotionType MotionType;
		public PositionalEvent Data;
		public KeyMask State;
		[MarshalAs(UnmanagedType.U1)] public bool SameScreen;
	}

	public enum WindowDetail : byte
	{
		Ancestor,
		Virtual,
		Inferior,
		Nonlinear,
		NonlinearVirtual,
		Pointer,
		PointerRoot,
		None,
	}

	public enum GrabMode : byte
	{
		Normal,
		Grab,
		Ungrab,
		WhileGrabbed
	}

	[Flags]
	public enum FocusFlags : byte
	{
		Focus = (1 << 0),
		SameScreen = (1 << 1)
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct EnterLeaveNotify : X11Event
	{
		public byte[] Opcodes => new byte[]{7, 8};
		public bool Entered => _opcode == 7;
		private byte _opcode;
		public WindowDetail Detail;
		public PositionalEvent Data;
		public KeyMask State;
		public GrabMode Mode;
		public FocusFlags FocusFlags;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct FocusInOut : X11Event
	{
		public byte[] Opcodes => new byte[]{9, 10};
		public bool Focused => _opcode == 9;
		private byte _opcode;
		public WindowDetail Detail;
		public ushort SequenceNumber;
		public uint EventWindow;
		public GrabMode Mode;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct KeymapNotify : X11Event
	{
		public byte[] Opcodes => new byte[]{11};
		private byte _opcode;
		// TODO: List of keys
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct Expose : X11Event
	{
		public byte[] Opcodes => new byte[]{12};
		private byte _opcode;
		public ushort SequenceNumber;
		public uint Window;
		public Rect Rect;
		public ushort Count;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct GraphicsExposure : X11Event
	{
		public byte[] Opcodes => new byte[]{13};
		private byte _opcode;
		public ushort SequenceNumber;
		public uint Drawable;
		public Rect Rect;
		public ushort MinorOpcode;
		public ushort Count;
		public byte MajorOpcode;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct NoExposure : X11Event
	{
		public byte[] Opcodes => new byte[]{14};
		private byte _opcode;
		public ushort SequenceNumber;
		public uint Drawable;
		public ushort MinorOpcode;
		public byte MajorOpcode;
	}

	public enum Visibility : byte
	{
		Unobscured,
		PartiallyObscured,
		FullyObscured
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct VisibilityNotify : X11Event
	{
		public byte[] Opcodes => new byte[]{15};
		private byte _opcode;
		public ushort SequenceNumber;
		public uint Window;
		public Visibility State;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct CreateNotify : X11Event
	{
		public byte[] Opcodes => new byte[]{16};
		private byte _opcode;
		public ushort SequenceNumber;
		public uint Parent;
		public uint Window;
		public Rect Rect;
		public ushort BorderWidth;
		[MarshalAs(UnmanagedType.U1)] public bool OverrideRedirect;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct DestroyNotify : X11Event
	{
		public byte[] Opcodes => new byte[]{17};
		private byte _opcode;
		public ushort SequenceNumber;
		public uint Event;
		public uint Window;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct UnmapNotify : X11Event
	{
		public byte[] Opcodes => new byte[]{18};
		private byte _opcode;
		public ushort SequenceNumber;
		public uint Event;
		public uint Window;
		[MarshalAs(UnmanagedType.U1)] public bool FromConfigure;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct MapNotify : X11Event
	{
		public byte[] Opcodes => new byte[]{19};
		private byte _opcode;
		public ushort SequenceNumber;
		public uint Event;
		public uint Window;
		[MarshalAs(UnmanagedType.U1)] public bool OverrideRedirect;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct MapRequest : X11Event
	{
		public byte[] Opcodes => new byte[]{20};
		private byte _opcode;
		public ushort SequenceNumber;
		public uint Parent;
		public uint Window;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ReparentNotify : X11Event
	{
		public byte[] Opcodes => new byte[]{21};
		private byte _opcode;
		public ushort SequenceNumber;
		public uint Event;
		public uint Window;
		public uint Parent;
		public short X;
		public short Y;
		[MarshalAs(UnmanagedType.U1)] public bool OverrideRedirect;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ConfigureNotify : X11Event
	{
		public byte[] Opcodes => new byte[]{22};
		private byte _opcode;
		public ushort SequenceNumber;
		public uint Event;
		public uint Window;
		public uint AboveSibling;
		public Rect Rect;
		public ushort BorderWidth;
		[MarshalAs(UnmanagedType.U1)] public bool OverrideRedirect;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ConfigureRequest : X11Event
	{
		public byte[] Opcodes => new byte[]{23};
		private byte _opcode;
		public StackMode StackMode;
		public ushort SequenceNumber;
		public uint Event;
		public uint Window;
		public uint Sibling;
		public Rect Rect;
		public ushort BorderWidth;
		public ushort ValueMask;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct GravityNotify : X11Event
	{
		public byte[] Opcodes => new byte[]{24};
		private byte _opcode;
		public ushort SequenceNumber;
		public uint Event;
		public uint Window;
		public short X;
		public short Y;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ResizeRequestEvent : X11Event
	{
		public byte[] Opcodes => new byte[]{25};
		private byte _opcode;
		public ushort SequenceNumber;
		public uint Window;
		public ushort Width;
		public ushort Height;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct CirculateEvent : X11Event
	{
		public byte[] Opcodes => new byte[]{26, 27};
		public bool IsNotify => _opcode == 26;
		private byte _opcode;
		public ushort SequenceNumber;
		public uint Event;
		public uint Window;
		private uint _unused;
		public ushort Width;
		public ushort Height;
		public bool IsTop;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct PropertyNotify : X11Event
	{
		public byte[] Opcodes => new byte[]{28};
		private byte _opcode;
		public ushort SequenceNumber;
		public uint Window;
		public uint Atom;
		public uint Time;
		public bool IsDeleted;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct SelectionClear : X11Event
	{
		public byte[] Opcodes => new byte[]{29};
		private byte _opcode;
		public ushort SequenceNumber;
		public uint Window;
		public uint Atom;
		public uint Time;
		public bool IsDeleted;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct SelectionRequest : X11Event
	{
		public byte[] Opcodes => new byte[]{30};
		private byte _opcode;
		public ushort SequenceNumber;
		public uint Time;
		public uint Owner;
		public uint Requestor;
		public uint Selection;
		public uint Target;
		public uint Property;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct SelectionNotify : X11Event
	{
		public byte[] Opcodes => new byte[]{31};
		private byte _opcode;
		public ushort SequenceNumber;
		public uint Time;
		public uint Requestor;
		public uint Selection;
		public uint Target;
		public uint Property;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ColormapNotify : X11Event
	{
		public byte[] Opcodes => new byte[]{32};
		private byte _opcode;
		public ushort SequenceNumber;
		public uint Window;
		public uint Colormap;
		[MarshalAs(UnmanagedType.U1)] public bool IsNew;
		[MarshalAs(UnmanagedType.U1)] public bool IsInstalled;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2, Size = 32)]
	public struct ClientMessage : X11Event
	{
		public byte[] Opcodes => new byte[]{33};
		private byte _opcode;
		public byte Format;
		public ushort SequenceNumber;
		public uint Window;
		public uint Type;
	}

	public enum MappingType : byte
	{
		Modifier,
		Keyboard,
		Pointer
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct MappingNotify : X11Event
	{
		public byte[] Opcodes => new byte[]{34};
		private byte _opcode;
		public ushort SequenceNumber;
		public MappingType Request;
		public byte FirstKeycode;
		public byte Count;
	}
}
