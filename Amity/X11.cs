namespace Amity
{
	using System;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Net;
	using System.Net.Sockets;
	using System.Drawing;
	using System.Runtime.CompilerServices;
	using System.IO;
	using System.Collections.Generic;
	using System.Runtime.InteropServices;

	// From https://www.x.org/docs/XProtocol/proto.pdf
	public class X11Connection : IDisposable
	{
		private Socket _socket;
		private BinaryReader _reader;
		private BinaryWriter _writer;

		private void Connect(EndPoint endPoint)
		{
			_socket = new Socket(endPoint.AddressFamily,
				SocketType.Stream, ProtocolType.IP);
			_socket.Connect(endPoint);
			if (_socket?.Connected != true)
			{
				throw new Exception("Connection failed");
			}
			var _stream = new NetworkStream(_socket, false);
			_reader = new BinaryReader(_stream, Encoding.UTF8);
			_writer = new BinaryWriter(_stream, Encoding.UTF8);

			// Initialize connection
			Write(new ConnectionRequest
			{
				ByteOrder = 154,
				MajorVersion = 11
			});
			_writer.Flush();

			// Response
			var response = Read<ConnectionResponse>();
			switch (response.Code)
			{
				case 0:
				case 2:
					throw new Exception(ReadString(response.ReasonLengthBytes, true));
				case 1:
					var response2 = Read<ConnectionSuccess>();
					var vendor = ReadString(response2.VendorLength, true);
					_formats.Clear();
					for (var i = 0; i < response2.FormatCount; i++)
					{
						_formats.Add(Read<Format>());
					}
					_screens.Clear();
					for (var i = 0; i < response2.ScreenCount; i++)
					{
						var screen = Read<Screen>();
						var depths = new List<(Depth, List<VisualType>)>();
						for (var j = 0; j < screen.DepthCount; j++)
						{
							var depth = Read<Depth>();
							var visualTypes = new List<VisualType>();
							for (var k = 0; k < depth.VisualsCount; k++)
							{
								visualTypes.Add(Read<VisualType>());
							}
							depths.Add((depth, visualTypes));
						}
						_screens.Add((screen, depths));
					}
					break;
			}
		}

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		struct ConnectionRequest
		{
			public byte ByteOrder;
			public ushort MajorVersion;
			public ushort MinorVersion;
			public ushort AuthNameLength;
			public ushort AuthDataLength;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		struct ConnectionResponse
		{
			public byte Code;
			public byte ReasonLengthBytes;
			public ushort MajorVersion;
			public ushort MinorVersion;
			public ushort DataLengthWords;
		}

		enum ByteOrder : byte
		{
			LSBFirst = 0,
			MSBFirst = 1,
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		struct ConnectionSuccess
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

		[StructLayout(LayoutKind.Sequential, Pack = 8)]
		struct Format
		{
			public byte Depth;
			public byte BitsPerPixel;
			public byte ScanlinePad;
		}

		enum BackingStoreType : byte
		{
			Never = 0,
			WhenMapped = 1,
			Always = 2
		}

		[Flags]
		enum Event : uint
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
		struct Screen
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
		struct Depth
		{
			public byte DepthValue;
			public ushort VisualsCount;
			private uint _unused;
		}

		enum ColorType : byte
		{
			StaticGray,
			GrayScale,
			StaticColor,
			PseudoColor,
			TrueColor,
			DirectColor
		}

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		struct VisualType
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

		class ValuesMask
		{
			public uint BackgroundPixmap;
			public uint BackgroundPixel;
			public uint BorderPixmap;
			public uint BorderPixel;
			public byte BitGravity;
			public byte WinGravity;
			public BackingStoreType BackingStore;
			public uint BackingPlanes;
			public uint BackingPixel;
			public byte OverrideRedirect;
			public bool SaveUnder;
			public Event EventMask;
			public Event DoNotPropagateMask;
			public uint Colormap;
			public uint Cursor;

			public void Read(uint mask, BinaryReader reader)
			{
				if ((mask & (1 << 0)) != 0) {
					BackgroundPixmap = reader.ReadUInt32();
				}
				if ((mask & (1 << 1)) != 0) {
					BackgroundPixel = reader.ReadUInt32();
				}
				if ((mask & (1 << 2)) != 0) {
					BorderPixmap = reader.ReadUInt32();
				}
				if ((mask & (1 << 3)) != 0) {
					BorderPixel = reader.ReadUInt32();
				}
				if ((mask & (1 << 4)) != 0) {
					BitGravity = reader.ReadByte();
				}
				if ((mask & (1 << 5)) != 0) {
					WinGravity = reader.ReadByte();
				}
				if ((mask & (1 << 6)) != 0) {
					BackingStore = (BackingStoreType)reader.ReadByte();
				}
				if ((mask & (1 << 7)) != 0) {
					BackingPlanes = reader.ReadUInt32();
				}
				if ((mask & (1 << 8)) != 0) {
					BackingPixel = reader.ReadUInt32();
				}
				if ((mask & (1 << 9)) != 0) {
					OverrideRedirect = reader.ReadByte();
				}
				if ((mask & (1 << 10)) != 0) {
					SaveUnder = reader.ReadByte() != 0;
				}
				if ((mask & (1 << 11)) != 0) {
					EventMask = (Event)reader.ReadUInt32();
				}
				if ((mask & (1 << 12)) != 0) {
					DoNotPropagateMask = (Event)reader.ReadUInt32();
				}
				if ((mask & (1 << 13)) != 0) {
					Colormap = reader.ReadUInt32();
				}
				if ((mask & (1 << 15)) != 0) {
					Cursor = reader.ReadUInt32();
				}
			}
		}

		private byte[] _buffer;

		private unsafe T Read<T>() where T : struct
		{
			var size = Marshal.SizeOf<T>();
			if (_buffer == null || _buffer.Length < size)
			{
				_buffer = new byte[size];
			}
			_reader.Read(_buffer, 0, size);
			fixed (byte* ptr = _buffer)
				return Marshal.PtrToStructure<T>((IntPtr)ptr);
		}

		private unsafe void Write<T>(T obj) where T : struct
		{
			var size = Marshal.SizeOf<T>();
			if (_buffer == null || _buffer.Length < size)
			{
				_buffer = new byte[size];
			}
			fixed (byte* ptr = _buffer)
				Marshal.StructureToPtr(obj, (IntPtr)ptr, false);
			_writer.Write(_buffer, 0, size);
		}

		private string ReadString(int len, bool pad)
		{
			var size = len + Pad(len);
			if (_buffer == null || _buffer.Length < size)
			{
				_buffer = new byte[size];
			}
			_reader.Read(_buffer, 0, size);
			return Encoding.UTF8.GetString(_buffer, 0, len);
		}

		private List<Format> _formats = new List<Format>();
		private List<(Screen, List<(Depth, List<VisualType>)>)> _screens
			= new List<(Screen, List<(Depth, List<VisualType>)>)>();

		public void Dispose()
		{
			_reader?.Dispose();
			_writer?.Dispose();
			_socket?.Dispose();
		}

		private static int Pad(int n) => (4 - (n % 4)) % 4;
	}

	public class X11 : IWindowAPI
	{
		public bool IsSupported() => GetServer(out var _, out var _);

		private static readonly Regex _serverPattern =
			new Regex(@"^([\w\.]*)(/unix)?:(\d+)(?:\.(\d+))?$");

		public static bool GetServer(out EndPoint endpoint, out int screen)
		{
			var match = _serverPattern.Match(Environment.GetEnvironmentVariable("DISPLAY"));
			if (!match.Success)
			{
				screen = 0;
				endpoint = null;
				return false;
			}
			var host = match.Groups[1].Value;
			var display = int.Parse(match.Groups[3].Value);
			screen = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;
			if (match.Groups[1].Value == "" || match.Groups[2].Value != "")
			{
				var file = $"/tmp/.X11-unix/X{display}";
				endpoint = new UnixEndPoint(file);
			} else {
				var ip = Dns.GetHostAddresses(host).First();
				endpoint = new IPEndPoint(ip, 6000 + display);
			}
			return true;
		}

		public Point MousePosition => throw new NotImplementedException();

		public IntPtr BufferPtr => throw new NotImplementedException();

		public Span<Color32> Buffer => throw new NotImplementedException();

		public Rectangle WindowArea => throw new NotImplementedException();

		public Rectangle ClientArea => throw new NotImplementedException();

		public event Action<Point> MouseMove;
		public event Action<int> MouseDown;
		public event Action<int> KeyDown;
		public event Action<int> KeyUp;
		public event Action Paint;
		public event Action Draw;


		public void Show()
		{
			throw new NotImplementedException();
		}

		public IDrawingContext GetDrawingContext()
		{
			throw new NotImplementedException();
		}

		public void Invalidate()
		{
			throw new NotImplementedException();
		}
	}
}