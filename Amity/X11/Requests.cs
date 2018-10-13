namespace Amity.X11
{
	using System;
	using System.Reflection;
	using System.Runtime.InteropServices;
	using System.Drawing;

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct Rect
	{
		public short X;
		public short Y;
		public ushort Width;
		public ushort Height;

		public static explicit operator Rect(Rectangle r)
		{
			checked
			{
				return new Rect
				{
					X = (short)r.X,
					Y = (short)r.Y,
					Width = (ushort)r.Width,
					Height = (ushort)r.Height
				};
			}
		}
	};

	
	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ConnectionRequest
	{
		public byte ByteOrder;
		public ushort MajorVersion;
		public ushort MinorVersion;
		public ushort AuthNameLength;
		public ushort AuthDataLength;
		private ushort _unused;
	}

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


	public enum WindowClass : ushort
	{
		CopyFromPArent,
		InputOutput,
		InputOnly
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct CreateWindow : X11Request<ValuesMask<WindowValues>>
	{
		public byte Opcode => 1;
		private byte _opcode;
		public byte Depth;
		public ushort RequestLength;
		public uint WindowId;
		public uint Parent;
		public Rect Rect;
		public ushort BorderWidth;
		public WindowClass Class;
		public uint Visual;
	}

	[StructLayout(LayoutKind.Sequential, Size = 8)]
	public struct Optional<T> where T : struct
	{
		// Force the bool to take the first 4 bytes,
		// rather than sometimes taking 1 depending on T.
		private int _hasValue;
		public bool HasValue
		{
			get => _hasValue != 0;
			set => _hasValue = value ? 1 : 0;
		}
		public T Value;

		public static implicit operator Optional<T>(T value)
		{
			return new Optional<T>
			{
				HasValue = true,
				Value = value
			};
		}
	}

	public class ValuesMask<T> : IWritable where T : struct
	{
		public int MaxSize => (Marshal.SizeOf<T>() / 2) + sizeof(uint);

		public T Values;

		public unsafe int WriteTo(Span<byte> span)
		{
			var data = MemoryMarshal.Cast<byte, uint>(span);
			const int elementSize = sizeof(uint);
			var count = Marshal.SizeOf<T>() / (elementSize * 2);
			if (count > 32)
			{
				throw new InvalidOperationException("Values struct was too big!");
			}

			// Prepare source data
			Span<uint> src = stackalloc uint[count * 2];
			MemoryMarshal.Write(MemoryMarshal.AsBytes(src), ref Values);

			// Iterate over each Optional<T> value, and if it HasValue
			// then add it to the mask and append its data.
			var dstIdx = 0;
			ref uint mask = ref data[dstIdx++];
			mask = 0;
			for (int i = 0; i < count; i++)
			{
				if (src[i*2 + 0] != 0)
				{
					mask |= (uint)(1 << i);
					data[dstIdx++] = src[i*2 + 1];
				}
			}

			return dstIdx * elementSize;
		}

		public static implicit operator ValuesMask<T>(T values)
		{
			return new ValuesMask<T> { Values = values };
		}
	}

	public abstract class ValuesBase : IWritable
	{
		public delegate void WriteObject(Span<byte> dst, object obj);

		private static void Write<T>(Span<byte> dst, object obj) where T : struct
		{
			var i = (T)obj;
			MemoryMarshal.Write(dst, ref i);
		}

		private static MethodInfo _writeMethod =
			((WriteObject)Write<int>).Method.GetGenericMethodDefinition();
		
		public int MaxSize => (1 + 32)*sizeof(int);

		public int WriteTo(Span<byte> data)
		{
			const int elementSize = 4;
			int totalSize = sizeof(uint);
			uint mask = 0;
			var fields = GetType().GetFields();
			var dst = data.Slice(sizeof(uint));
			for (int i = 0; i < fields.Length; i++)
			{
				var obj = fields[i].GetValue(this);
				if (obj != null)
				{
					mask |= (uint)(1 << i);
					var method = _writeMethod.MakeGenericMethod(obj.GetType());
					var del = (WriteObject)Delegate.CreateDelegate(
						typeof(WriteObject), null, method);
					del(dst, obj);
					dst = dst.Slice(elementSize);
					totalSize += elementSize;
				}
			}
			MemoryMarshal.Write(data, ref mask);
			return totalSize;
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	public struct WindowValues
	{
		public Optional<uint> BackgroundPixmap;
		public Optional<uint> BackgroundPixel;
		public Optional<uint> BorderPixmap;
		public Optional<uint> BorderPixel;
		public Optional<byte> BitGravity;
		public Optional<byte> WinGravity;
		public Optional<BackingStoreType> BackingStore;
		public Optional<uint> BackingPlanes;
		public Optional<uint> BackingPixel;
		public Optional<byte> OverrideRedirect;
		public Optional<bool> SaveUnder;
		public Optional<Event> EventMask;
		public Optional<Event> DoNotPropagateMask;
		public Optional<uint> Colormap;
		public Optional<uint> Cursor;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ChangeWindowAttributes : X11Request<ValuesMask<WindowValues>>
	{
		public byte Opcode => 2;
		public uint Window;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct GetWindowAttributes : X11RequestReply<GetWindowAttributesReply>
	{
		public byte Opcode => 3;
		private uint _unused;
		public uint Window;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct GetWindowAttributesReply : X11Reply
	{
		private byte _opcode;
		public BackingStoreType BackingStore;
		public ushort SequenceNumber;
		private uint _replyLength;
		public uint VisualID;
		public WindowClass Class;
		public byte BitGravity;
		public byte WinGravity;
		public uint BackingPlanes;
		public uint BackingPixel;
		[MarshalAs(UnmanagedType.U1)] public bool SaveUnder;
		[MarshalAs(UnmanagedType.U1)] public bool MapIsInstalled;
		public Visibility MapState;
		[MarshalAs(UnmanagedType.U1)] public bool OverrideRedirect;
		public uint Colormap;
		public Event AllEvents;
		public Event YourEvents;
		public Event DoNotPropagateMask;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct DestroyWindow : X11Request
	{
		public byte Opcode => 4;
		private uint _unused;
		public uint Window;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct DestroySubwindows : X11Request
	{
		public byte Opcode => 5;
		private uint _unused;
		public uint Window;
	}

	public enum SaveSet : byte
	{
		Insert,
		Delete
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ChangeSaveSet : X11Request
	{
		public byte Opcode => 6;
		private byte _opcode;
		public SaveSet SaveSet;
		private ushort _requestLength;
		public uint Window;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ReparentWindow : X11Request
	{
		public byte Opcode => 7;
		private uint _unused;
		public uint Window;
		public uint Parent;
		public short X;
		public short Y;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct MapWindow : X11Request
	{
		public byte Opcode => 8;
		private uint _unused;
		public uint Window;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct MapSubwindows : X11Request
	{
		public byte Opcode => 9;
		private uint _unused;
		public uint Window;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct UnmapWindow : X11Request
	{
		public byte Opcode => 10;
		private uint _unused;
		public uint Window;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct UnmapSubwindows : X11Request
	{
		public byte Opcode => 11;
		private uint _unused;
		public uint Window;
	}

	public enum StackMode : byte
	{
		Above,
		Below,
		TopIf,
		BottomIf,
		Opposite
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	public struct ConfigurationValues
	{
		public Optional<short> X;
		public Optional<short> Y;
		public Optional<ushort> Width;
		public Optional<ushort> Height;
		public Optional<ushort> BorderWidth;
		public Optional<uint> Sibling;
		public Optional<StackMode> StackMode;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ConfigureWindow : X11Request<ValuesMask<ConfigurationValues>>
	{
		public byte Opcode => 12;
		private uint _unused;
		public uint Window;
	}

	public enum CirculateDirection : byte
	{
		RaiseLowest,
		LowerHighest
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct CirculateWindow : X11Request
	{
		public byte Opcode => 13;
		private byte _opcode;
		public CirculateDirection Direction;
		private ushort _requestLength;
		public uint Window;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct GetGeometry : X11Request
	{
		public byte Opcode => 14;
		private uint _unused;
		public uint Drawable;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct QueryTree : X11Request
	{
		public byte Opcode => 15;
		private uint _unused;
		public uint Window;
	}

	public enum GCFunction : byte
	{
		Clear,
		Ang,
		AndReverse,
		Copy,
		AndInverted,
		NoOp,
		Xor,
		Or,
		Nor,
		Equiv,
		Invert,
		OrReverse,
		CopyInverted,
		OrInverted,
		Nand,
		Set
	}

	public enum LineStyle : byte
	{
		Solid,
		OnOffDash,
		DoubleDash
	}

	public enum CapStyle : byte
	{
		NotLAst,
		Butt,
		Round,
		Projecting
	}

	public enum JoinStyle : byte
	{
		Miter,
		Round,
		Bevel
	}

	public enum FillStyle : byte
	{
		Solid,
		Tiled,
		Stippled,
		OpaqueStippled
	}

	public enum FillRule : byte
	{
		EvenOdd,
		Winding
	}

	public enum SubwindowMode : byte
	{
		ClipByChildren,
		IncludeInferiors
	}

	public enum ArcMode : byte
	{
		Chord,
		PieSlice
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	public struct GCValues
	{
		public Optional<GCFunction> Function;
		public Optional<uint> PlaneMask;
		public Optional<Color32> Foreground;
		public Optional<Color32> Background;
		public Optional<ushort> LineWidth;
		public Optional<LineStyle> LineStyle;
		public Optional<CapStyle> CapStyle;
		public Optional<JoinStyle> JoinStyle;
		public Optional<FillStyle> FillStyle;
		public Optional<FillRule> FillRule;
		public Optional<uint> Tile;
		public Optional<uint> Stipple;
		public Optional<short> TileStippleXOrigin;
		public Optional<short> TileStippleYOrigin;
		public Optional<uint> Font;
		public Optional<SubwindowMode> SubwindowMode;
		public Optional<bool> GraphicsExposures;
		public Optional<short> ClipXOrigin;
		public Optional<short> ClipYOrigin;
		public Optional<uint> ClipMask;
		public Optional<ushort> DashOffset;
		public Optional<byte> Dashes;
		public Optional<ArcMode> ArcMode;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct CreateGC : X11Request<ValuesMask<GCValues>>
	{
		public byte Opcode => 55;
		private uint _unused;
		public uint ContextID;
		public uint Drawable;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ClearArea : X11Request
	{
		public byte Opcode => 61;
		private byte _opcode;
		[MarshalAs(UnmanagedType.U1)] public bool Exposures;
		private ushort _requestLength;
		public uint Window;
		public Rect Rect;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct Point
	{
		public ushort X;
		public ushort Y;
	}

	public enum CoordinateMode : byte
	{
		Origin,
		Previous
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct PolyLine : X11RequestWithData<Point>
	{
		public byte Opcode => 66;
		private byte _opcode;
		public CoordinateMode CoordinateMode;
		private ushort _requestLength;
		public uint Drawable;
		public uint GContext;
	}

	public enum ImageFormat : byte
	{
		Bitmap,
		XYPixmap,
		ZPixmap
	}


	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct PutImage : X11RequestWithData<Color32>
	{
		public byte Opcode => 72;
		private byte _opcode;
		public ImageFormat Format;
		private ushort _requestLength;
		public uint Drawable;
		public uint GContext;
		public ushort Width;
		public ushort Height;
		public short DstX;
		public short DstY;
		public byte LeftPad;
		public byte Depth;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct GetImage : X11Request
	{
		public byte Opcode => 73;
		public byte _opcode;
		public ImageFormat Format;
		public ushort _requestLength;
		public uint Drawable;
		public Rect Rect;
		public uint PlaneMask;
	}
}