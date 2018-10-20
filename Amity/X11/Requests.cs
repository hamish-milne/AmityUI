namespace Amity.X11
{
	using System;
	using System.Runtime.InteropServices;
	using System.Drawing;
	using System.Text;
	using System.Collections.Generic;
	using static System.Runtime.InteropServices.UnmanagedType;

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

		public static implicit operator Rectangle(Rect r)
		{
			checked
			{
				return new Rectangle
				{
					X = r.X,
					Y = r.Y,
					Width = r.Width,
					Height = r.Height
				};
			}
		}
	};

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct Arc
	{
		public Rect Rect;
		public short Angle1, Angle2;

		public Arc(Rectangle position, float angle1, float angle2)
		{
			Rect = (Rect)position;
			Angle1 = (short)Math.Round((angle1 % 360) * 64);
			Angle2 = (short)Math.Round((angle2 % 360) * 64);
		}
	}
	
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
		public Window Root;
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
		public Visual RootVisual;
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
		public Visual VisualId;
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
	public struct CreateWindow : X11DataRequest<WindowValues>
	{
		public byte Opcode => 1;
		private byte _opcode;
		public byte Depth;
		public ushort RequestLength;
		public Window WindowId;
		public Window Parent;
		public Rect Rect;
		public ushort BorderWidth;
		public WindowClass Class;
		public Visual Visual;

		public int GetMaxSize(in WindowValues data)
			=> ValuesMask.MaxSize<WindowValues>();
		public int WriteTo(in WindowValues data, Span<byte> output, Span<byte> rData) =>
			ValuesMask.WriteTo(data, output);
	}

	[StructLayout(LayoutKind.Sequential, Size = 8)]
	public struct Optional<T> where T : unmanaged
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

	public static class ValuesMask
	{
		public static int MaxSize<T>()  where T : struct =>
			(Marshal.SizeOf<T>() / 2) + sizeof(uint);


		// NB: This only works for arguments! If you pass in a field ref here
		// it *will* crash the program.
		private static unsafe ReadOnlySpan<T> StructToBytes<T>(in T value)
			where T : unmanaged
		{
			fixed (T* ptr = &value)
				return new ReadOnlySpan<T>(ptr, 1);
		}

		public static unsafe int WriteTo<T>(in T values, Span<byte> span)
			where T : struct
		{
			var data = MemoryMarshal.Cast<byte, uint>(span);
			const int elementSize = sizeof(uint);
			var count = Marshal.SizeOf<T>() / (elementSize * 2);
			if (count > 32)
			{
				throw new InvalidOperationException("Values struct was too big!");
			}

			// Prepare source data:
	
			// This doesn't work, because Optional<T> is considered 
			// non-blittable because it's generic? :wat:
			//var src = MemoryMarshal.Cast<T, uint>(StructToBytes(values));
			// So instead we need to copy twice..
			Span<uint> src = stackalloc uint[count * 2];
			{
				T rValues = values;
				MemoryMarshal.Write(MemoryMarshal.AsBytes(src), ref rValues);
			}

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
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	public struct WindowValues
	{
		public Optional<Pixmap> BackgroundPixmap;
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
		public Optional<Colormap> Colormap;
		public Optional<uint> Cursor;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ChangeWindowAttributes : X11DataRequest<WindowValues>
	{
		public byte Opcode => 2;
		public Window Window;

		public int GetMaxSize(in WindowValues data)
			=> ValuesMask.MaxSize<WindowValues>();
		public int WriteTo(in WindowValues data, Span<byte> output, Span<byte> rData) =>
			ValuesMask.WriteTo(data, output);
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct GetWindowAttributes : X11RequestReply<GetWindowAttributes.Reply>
	{
		public byte Opcode => 3;
		private uint _unused;
		public Window Window;

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		public struct Reply : X11Reply
		{
			private byte _opcode;
			public BackingStoreType BackingStore;
			public ushort SequenceNumber;
			private uint _replyLength;
			public Visual VisualID;
			public WindowClass Class;
			public byte BitGravity;
			public byte WinGravity;
			public uint BackingPlanes;
			public uint BackingPixel;
			[MarshalAs(U1)] public bool SaveUnder;
			[MarshalAs(U1)] public bool MapIsInstalled;
			public Visibility MapState;
			[MarshalAs(U1)] public bool OverrideRedirect;
			public Colormap Colormap;
			public Event AllEvents;
			public Event YourEvents;
			public Event DoNotPropagateMask;
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct DestroyWindow : X11Request
	{
		public byte Opcode => 4;
		private uint _unused;
		public Window Window;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct DestroySubwindows : X11Request
	{
		public byte Opcode => 5;
		private uint _unused;
		public Window Window;
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
		public Window Window;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ReparentWindow : X11Request
	{
		public byte Opcode => 7;
		private uint _unused;
		public Window Window;
		public Window Parent;
		public short X;
		public short Y;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct MapWindow : X11Request
	{
		public byte Opcode => 8;
		private uint _unused;
		public Window Window;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct MapSubwindows : X11Request
	{
		public byte Opcode => 9;
		private uint _unused;
		public Window Window;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct UnmapWindow : X11Request
	{
		public byte Opcode => 10;
		private uint _unused;
		public Window Window;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct UnmapSubwindows : X11Request
	{
		public byte Opcode => 11;
		private uint _unused;
		public Window Window;
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
		public Optional<Window> Sibling;
		public Optional<StackMode> StackMode;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ConfigureWindow : X11DataRequest<ConfigurationValues>
	{
		public byte Opcode => 12;
		private uint _unused;
		public Window Window;

		public int GetMaxSize(in ConfigurationValues data)
			=> ValuesMask.MaxSize<ConfigurationValues>();
		public int WriteTo(in ConfigurationValues data, Span<byte> output, Span<byte> rData) =>
			ValuesMask.WriteTo(data, output);
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
		public Window Window;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct GetGeometry : X11RequestReply<GetGeometry.Reply>
	{
		public byte Opcode => 14;
		private uint _unused;
		public Drawable Drawable;

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		public struct Reply : X11Reply
		{
			private byte _unused;
			public byte Depth;
			public ushort SequenceNumber;
			private uint _replyLength;
			public Window Root;
			public Rect Rect;
			public ushort BorderWidth;
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct QueryTree : X11RequestReply<QueryTree.Reply>
	{
		public byte Opcode => 15;
		private uint _unused;
		public Window Window;

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		public struct Reply : X11SpanReply<Window>
		{
			private ushort _unused;
			public ushort SequenceNumber;
			private uint _replyLength;
			public Window Root;
			public Window Parent;
			private ushort _childrenLength;
		}
	}

	public static partial class Util
	{
		public static readonly Encoding Encoding
			= Encoding.GetEncoding("ISO-8859-1");

		public static unsafe int WriteOut(
			this string str, Span<byte> output)
		{
			fixed (char* cPtr = str)
			fixed (byte* ptr = output)
			{
				return Encoding.GetBytes(
					cPtr, str.Length, ptr, output.Length);
			}
		}

		public static unsafe string GetString(
			this Span<byte> span
		)
		{
			fixed (byte* ptr = span)
				return Encoding.GetString(ptr, span.Length);
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct InternAtom : X11DataRequestReply<string, InternAtom.Reply>
	{
		public byte Opcode => 16;
		private byte _opcode;
		[MarshalAs(U1)] public bool OnlyIfExists;
		private ushort _requestLength;

		public int GetMaxSize(in string data) =>
			4 + Util.Encoding.GetMaxByteCount(data?.Length ?? 0);
		public int WriteTo(in string data, Span<byte> output, Span<byte> rData)
		{
			var str = data ?? string.Empty;
			var byteCount = data.WriteOut(output.Slice(4));
			MemoryMarshal.Cast<byte, ushort>(output)[0] = (ushort)byteCount;
			return 4 + byteCount;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		public struct Reply : X11Reply
		{
			private ushort _unused;
			public ushort SequenceNumber;
			private uint _replyLength;
			public uint Atom;
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct GetAtomName : X11RequestReply<GetAtomName.Reply>
	{
		public byte Opcode => 17;
		private uint _unused;
		public uint Atom;

		[StructLayout(LayoutKind.Sequential, Pack = 2, Size = 32)]
		public struct Reply : X11Reply<string>
		{
			private uint _unused;
			private uint _replyLength;
			private ushort _nameLength;

			public string Read(Span<byte> data) =>
				data.Slice(0, _nameLength).GetString();
		}
	}

	public enum PropertyMode : byte
	{
		Replace,
		Prepend,
		Append
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ChangeProperty : X11DataRequest<Memory<byte>>
	{
		public byte Opcode => 18;
		private byte _opcode;
		public PropertyMode Mode;
		private ushort _requestLength;
		public Window Window;
		public uint Property;
		public uint Type;
		public byte Format;
		private ushort _unused;
		
		public int GetMaxSize(in Memory<byte> data) => data.Length + 4;
		public int WriteTo(in Memory<byte> data, Span<byte> output, Span<byte> rData)
		{
			switch (Format)
			{
				case 8:
				case 16:
				case 32:
					break;
				default:
					throw new ArgumentException(
						$"Format unit {Format} not supported");
			}
			MemoryMarshal.Cast<byte, uint>(output)[0] =
				(uint)(data.Length / (Format / 8));
			data.Span.CopyTo(output.Slice(4));
			return data.Length + 4;
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct DeleteProperty : X11Request
	{
		public byte Opcode => 19;
		private uint _unused;
		public Window Window;
		public uint Property;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct GetProperty : X11RequestReply<GetProperty.Reply>
	{
		public byte Opcode => 20;
		private byte _opcode;
		[MarshalAs(U1)] public bool Delete;
		private ushort _requestLength;
		public Window Window;
		public uint Property;
		public uint Type;
		public uint Offset;
		public uint Length;

		[StructLayout(LayoutKind.Sequential, Pack = 2, Size = 32)]
		public struct Reply : X11SpanReply<byte>
		{
			private byte _unused;
			public byte Format;
			public ushort SequenceNumber;
			private uint _replyLength;
			public uint Type;
			public uint BytesAfter;
			public uint ValueLength;
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ListProperties : X11RequestReply<ListProperties.Reply>
	{
		public byte Opcode => 21;
		private uint _unused;
		public Window Window;

		[StructLayout(LayoutKind.Sequential, Pack = 2, Size = 32)]
		public struct Reply : X11SpanReply<uint>
		{
			private ushort _unused;
			public ushort SequenceNumber;
			private uint _replyLength;
			private ushort _atomsCount;
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct SetSelectionOwner : X11Request
	{
		public byte Opcode => 22;
		private uint _unused;
		public uint Owner;
		public uint Selection;
		public uint Time;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct GetSelectionOwner : X11RequestReply<GetSelectionOwner.Reply>
	{
		public byte Opcode => 23;
		private uint _unused;
		public uint Selection;

		[StructLayout(LayoutKind.Sequential, Pack = 2, Size = 32)]
		public struct Reply : X11Reply
		{
			private ushort _unused;
			public ushort SequenceNumber;
			private uint _replyLength;
			public Window Window;
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ConvertSelection : X11Request
	{
		public byte Opcode => 24;
		private uint _unused;
		public uint Requestor;
		public uint Selection;
		public uint Target;
		public uint Property;
		public uint Time;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct SendEvent : X11Request
	{
		public byte Opcode => 25;
		private byte _opcode;
		[MarshalAs(U1)] public bool Propagate;
		private ushort _requestLength;
		public uint Destination;
		public Event EventMask;
		public X11AbstractEvent EventData;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct GrabPointer : X11RequestReply<GrabPointer.Reply>
	{
		public byte Opcode => 26;
		private byte _opcode;
		[MarshalAs(U1)] public bool OwnerEvents;
		private ushort _requestLength;
		public uint GrabWindow;
		public ushort EventMask;
		[MarshalAs(U1)] public bool PointerAsync;
		[MarshalAs(U1)] public bool KeyboardAsync;
		public uint ConfineTo;
		public uint Cursor;
		public uint Time;

		public enum Status : byte
		{
			Success,
			AlreadyGrabbed,
			InvalidTime,
			NotViewable,
			Frozen
		}

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		public struct Reply : X11Reply
		{
			private byte _unused;
			public Status Status;
			public ushort SequenceNumber;
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct UngrabPointer : X11Request
	{
		public byte Opcode => 27;
		private uint _unused;
		public uint Time;
	}

	[StructLayout(LayoutKind.Sequential, Size = 4)]
	public struct GrabServer : X11Request
	{
		public byte Opcode => 36;
	}

	[StructLayout(LayoutKind.Sequential, Size = 4)]
	public struct UngrabServer : X11Request
	{
		public byte Opcode => 37;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct OpenFont : X11DataRequest<string>
	{
		public byte Opcode => 45;
		private uint _unused;
		public uint FontID;
		private ushort _nameLength;

		public int GetMaxSize(in string data) =>
			Encoding.UTF8.GetMaxByteCount(data?.Length ?? 0);

		public int WriteTo(in string data, Span<byte> output, Span<byte> rData)
		{
			var count = data.WriteOut(output);
			MemoryMarshal.Cast<byte, ushort>(rData)[4] = (ushort)count;
			return count;
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct CloseFont : X11Request
	{
		public byte Opcode => 46;
		private uint _unused;
		public uint FontID;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ListFonts : X11DataRequestReply<string, ListFonts.Reply>
	{
		public byte Opcode => 49;
		private uint _unused;
		public ushort MaxNames;
		private ushort _patternLength;

		public int GetMaxSize(in string data) =>
			Encoding.UTF8.GetMaxByteCount(data?.Length ?? 0);

		public int WriteTo(in string data, Span<byte> output, Span<byte> rData)
		{
			var count = data.WriteOut(output);
			MemoryMarshal.Cast<byte, ushort>(rData)[3] = (ushort)count;
			return count;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		public struct Reply : X11Reply<List<string>>
		{
			private ushort _unused;
			public ushort SequenceNumber;
			private uint _replyLength;
			private ushort _namesCount;

			public List<string> Read(Span<byte> data)
			{
				var ret = new List<string>();
				for (int i = 0; (data.Length - i) >= 4; i++)
				{
					var span = data.Slice(i+1, data[i]);
					ret.Add(span.GetString());
					i += data[i];
				}
				return ret;
			}
		}

	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct CreatePixmap : X11Request
	{
		public byte Opcode => 53;
		private byte _opcode;
		public byte Depth;
		private ushort _requestLength;
		public Pixmap PixmapID;
		public Drawable Drawable;
		public ushort Width;
		public ushort Height;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct FreePixmap : X11Request
	{
		public byte Opcode => 54;
		private uint _unused;
		public Pixmap Pixmap;
	};

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
	public struct CreateGC : X11DataRequest<GCValues>
	{
		public byte Opcode => 55;
		private uint _unused;
		public GContext ContextID;
		public Drawable Drawable;

		public int GetMaxSize(in GCValues data)
			=> ValuesMask.MaxSize<GCValues>();
		public int WriteTo(in GCValues data, Span<byte> output, Span<byte> rData) =>
			ValuesMask.WriteTo(data, output);
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ChangeGC : X11DataRequest<GCValues>
	{
		public byte Opcode => 56;
		private uint _unused;
		public GContext ContextID;

		public int GetMaxSize(in GCValues data)
			=> ValuesMask.MaxSize<GCValues>();
		public int WriteTo(in GCValues data, Span<byte> output, Span<byte> rData) =>
			ValuesMask.WriteTo(data, output);
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct FreeGC : X11Request
	{
		public byte Opcode => 60;
		private uint _unused;
		public GContext GContext;
	};


	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ClearArea : X11Request
	{
		public byte Opcode => 61;
		private byte _opcode;
		[MarshalAs(U1)] public bool Exposures;
		private ushort _requestLength;
		public Window Window;
		public Rect Rect;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct CopyArea : X11Request
	{
		public byte Opcode => 62;
		private uint _unused;
		public Drawable SrcDrawable;
		public Drawable DstDrawable;
		public GContext GContext;
		public short SrcX;
		public short SrcY;
		public Rect Dst;
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
	public struct PolyPoint : X11SpanRequest<Point>
	{
		public byte Opcode => 64;
		private byte _opcode;
		public CoordinateMode CoordinateMode;
		private ushort _requestLength;
		public Drawable Drawable;
		public GContext GContext;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct PolyLine : X11SpanRequest<Point>
	{
		public byte Opcode => 65;
		private byte _opcode;
		public CoordinateMode CoordinateMode;
		private ushort _requestLength;
		public Drawable Drawable;
		public GContext GContext;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct Segment
	{
		public Point A, B;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct PolySegment : X11SpanRequest<Segment>
	{
		public byte Opcode => 66;
		private byte _opcode;
		public CoordinateMode CoordinateMode;
		private ushort _requestLength;
		public Drawable Drawable;
		public GContext GContext;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct PolyRectangle : X11SpanRequest<Rect>
	{
		public byte Opcode => 67;
		private byte _opcode;
		public CoordinateMode CoordinateMode;
		private ushort _requestLength;
		public Drawable Drawable;
		public GContext GContext;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct PolyArc : X11SpanRequest<Arc>
	{
		public byte Opcode => 68;
		private uint _unused;
		public Drawable Drawable;
		public GContext GContext;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct PolyFillRectangle : X11SpanRequest<Rect>
	{
		public byte Opcode => 70;
		private byte _opcode;
		private byte _unused;
		private ushort _requestLength;
		public Drawable Drawable;
		public GContext GContext;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct PolyFillArc : X11SpanRequest<Arc>
	{
		public byte Opcode => 71;
		private uint _unused;
		public Drawable Drawable;
		public GContext GContext;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct PolyText8 : X11DataRequest<string>
	{
		public byte Opcode => 74;
		private uint _unused;
		public Drawable Drawable;
		public GContext GContext;
		public short X;
		public short Y;

		public int GetMaxSize(in string data) => data == null ? 0 :
			Encoding.UTF8.GetMaxByteCount(data.Length) + (2 * (1 + data.Length/254));

		private static unsafe bool WriteSegment(
			ref ReadOnlySpan<char> data, ref int count, ref Span<byte> output)
		{
			var maxLen = Encoding.UTF8.GetMaxCharCount(254);
			var len = data.Length <= maxLen ? data.Length : maxLen;
			if (len == 0) { return true; }
			fixed (char* inPtr = data)
			fixed (byte* outPtr = output.Slice(2))
			{
				var c = Encoding.UTF8.GetBytes(inPtr, len, outPtr, output.Length);
				output[0] = (byte)c;
				output[1] = 0; // Delta
				count += 2;
				count += c;
				output = output.Slice(2 + c);
			}
			data = data.Slice(len);
			return false;
		}

		public int WriteTo(in string data, Span<byte> output, Span<byte> rData)
		{
			int count = 0;
			var str = data.AsSpan();
			while (!WriteSegment(ref str, ref count, ref output)) { }
			return count;
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct PolyText16 : X11DataRequest<string>
	{
		public byte Opcode => 74;
		private uint _unused;
		public Drawable Drawable;
		public GContext GContext;
		public short X;
		public short Y;

		public int GetMaxSize(in string data) => data == null ? 0 :
			data.Length*sizeof(char) + (2 * (1 + data.Length/254));

		private static unsafe bool WriteSegment(
			ref ReadOnlySpan<char> data, ref int count, ref Span<byte> output)
		{
			var maxLen = Encoding.UTF8.GetMaxCharCount(254);
			var len = data.Length <= maxLen ? data.Length : maxLen;
			if (len == 0) { return true; }
			fixed (char* inPtr = data)
			fixed (byte* outPtr = output.Slice(2))
			{
				var c = Encoding.UTF8.GetBytes(inPtr, len, outPtr, output.Length);
				output[0] = (byte)c;
				output[1] = 0; // Delta
				count += 2;
				count += c;
				output = output.Slice(2 + c);
			}
			data = data.Slice(len);
			return false;
		}

		public int WriteTo(in string data, Span<byte> output, Span<byte> rData)
		{
			int count = 0;
			var str = data.AsSpan();
			while (!WriteSegment(ref str, ref count, ref output)) { }
			return count;
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ImageText8 : X11DataRequest<string>
	{
		public byte Opcode => 76;
		private uint _unused;
		public Drawable Drawable;
		public GContext GContext;
		public short X;
		public short Y;

		public int GetMaxSize(in string data) =>
			Encoding.UTF8.GetMaxByteCount(data?.Length ?? 0);
		public int WriteTo(in string data, Span<byte> output, Span<byte> rData)
		{
			var count = data.WriteOut(output);
			if (count > byte.MaxValue)
			{
				throw new Exception("String is too long!");
			}
			rData[1] = (byte)count;
			return count;
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ImageText16 : X11DataRequest<string>
	{
		public byte Opcode => 77;
		private uint _unused;
		public Drawable Drawable;
		public GContext GContext;
		public short X;
		public short Y;

		public int GetMaxSize(in string data) =>
			(data?.Length ?? 0) * sizeof(char);
		public int WriteTo(in string data, Span<byte> output, Span<byte> rData)
		{
			var src = MemoryMarshal.Cast<char, byte>(data.AsSpan());
			// We need to reverse the endianness:
			for (int i = 0; i < src.Length; i += sizeof(char))
			{
				output[i] = src[i+1];
				output[i+1] = src[i];
			}
			var count = data.Length;
			if (count > byte.MaxValue)
			{
				throw new Exception("String is too long!");
			}
			rData[1] = (byte)count;
			return src.Length;
		}
	}

	public enum ImageFormat : byte
	{
		Bitmap,
		XYPixmap,
		ZPixmap
	}

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct PutImage : X11SpanRequest<Color32>
	{
		public byte Opcode => 72;
		private byte _opcode;
		public ImageFormat Format;
		private ushort _requestLength;
		public Drawable Drawable;
		public GContext GContext;
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
		private byte _opcode;
		public ImageFormat Format;
		public ushort _requestLength;
		public Drawable Drawable;
		public Rect Rect;
		public uint PlaneMask;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ListExtensions : X11RequestReply<ListExtensions.Reply>
	{
		public byte Opcode => 99;
		private uint _unused;

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		public struct Reply : X11Reply<List<string>>
		{
			private byte _unused;
			private byte _namesCount;
			public ushort SequenceNumber;
			private uint _replyLength;

			public int ExpectedLength => (int)_replyLength * sizeof(uint);

			public List<string> Read(Span<byte> data)
			{
				var ret = new List<string>();
				for (int i = 0; (data.Length - i) >= 4; i++)
				{
					var span = data.Slice(i+1, data[i]);
					ret.Add(span.GetString());
					i += data[i];
				}
				return ret;
			}
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct Bell : X11Request
	{
		public byte Opcode => 104;
		private byte _opcode;
		public sbyte Percent;
		private ushort _requestLength;
	}

	[StructLayout(LayoutKind.Sequential, Size = 4)]
	public struct NoOperation : X11Request
	{
		public byte Opcode => 127;
	}
}