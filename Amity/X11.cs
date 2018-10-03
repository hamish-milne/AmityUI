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
	using System.Collections.Generic;
	using System.Runtime.InteropServices;
	using System.Reflection;

	// From https://www.x.org/docs/XProtocol/proto.pdf
	public class X11Connection : IDisposable
	{
		private Socket _socket;

		public void Connect(EndPoint endPoint)
		{
			_socket = new Socket(endPoint.AddressFamily,
				SocketType.Stream, ProtocolType.IP);
			_socket.Connect(endPoint);
			if (_socket?.Connected != true)
			{
				throw new Exception("Connection failed");
			}
			var _stream = new NetworkStream(_socket, false);

			// Initialize connection
			Write(new ConnectionRequest
			{
				ByteOrder = 0x6C,
				MajorVersion = 11
			});
			Send(false);

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
					_info = response2;
					break;
			}
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		public uint CreateWindow(Rectangle rect)
		{
			var data = new CreateWindowData
			{
				Opcode = 1,
				X = (ushort)rect.X,
				Y = (ushort)rect.Y,
				Width = (ushort)rect.Width,
				Height = (ushort)rect.Height,
				Visual = _screens.First().Item1.RootVisual,
				Parent = _screens.First().Item1.Root,
				Depth = _screens.First().Item1.RootDepth,
				WindowId = _info.ResourceIdBase + 1,
				Class = WindowClass.InputOutput,
			};
			Write(data);
			Write(new WindowValues
			{
				EventMask = Event.KeyPress | Event.KeyRelease,
			});
			Send(true);
			Write(new MapWindowData
			{
				Opcode = 8,
				Window = data.WindowId
			});
			Send(true);
			return data.WindowId;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		struct ConnectionRequest
		{
			public byte ByteOrder;
			public ushort MajorVersion;
			public ushort MinorVersion;
			public ushort AuthNameLength;
			public ushort AuthDataLength;
			private ushort _unused;
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

		[StructLayout(LayoutKind.Sequential, Size = 8)]
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

		enum WindowClass : ushort
		{
			CopyFromPArent,
			InputOutput,
			InputOnly
		}

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		struct CreateWindowData
		{
			public byte Opcode;
			public byte Depth;
			public ushort RequestLength;
			public uint WindowId;
			public uint Parent;
			public ushort X;
			public ushort Y;
			public ushort Width;
			public ushort Height;
			public ushort BorderWidth;
			public WindowClass Class;
			public uint Visual;
		}

		abstract class ValuesBase
		{
			public delegate void WriteObject(Span<byte> dst, object obj);

			private static void Write<T>(Span<byte> dst, object obj) where T : struct
			{
				var i = (T)obj;
				MemoryMarshal.Write(dst, ref i);
			}

			private static MethodInfo _writeMethod =
				((WriteObject)Write<int>).Method.GetGenericMethodDefinition();

			public int Write(Span<byte> data)
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
						var del = (WriteObject)Delegate.CreateDelegate(typeof(WriteObject), null, method);
						del(dst, obj);
						dst = data.Slice(elementSize);
						totalSize += elementSize;
					}
				}
				MemoryMarshal.Write(data, ref mask);
				return totalSize;
			}
		}

		class WindowValues : ValuesBase
		{
			public uint? BackgroundPixmap;
			public uint? BackgroundPixel;
			public uint? BorderPixmap;
			public uint? BorderPixel;
			public byte? BitGravity;
			public byte? WinGravity;
			public BackingStoreType? BackingStore;
			public uint? BackingPlanes;
			public uint? BackingPixel;
			public byte? OverrideRedirect;
			public bool? SaveUnder;
			public Event? EventMask;
			public Event? DoNotPropagateMask;
			public uint? Colormap;
			public uint? Cursor;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		private struct MapWindowData
		{
			public byte Opcode;
			public ushort RequestLength;
			public uint Window;
		}

		private byte[] _rBuffer;
		private byte[] _wBuffer;
		private int _wBufferIdx;

		private T Read<T>() where T : struct
		{
			var size = Marshal.SizeOf<T>();
			if (_rBuffer == null || _rBuffer.Length < size)
			{
				_rBuffer = new byte[size];
			}
			_socket.Receive(_rBuffer, 0, size, SocketFlags.None);
			return MemoryMarshal.Read<T>(_rBuffer.AsSpan(0, size));
		}

		private void Write<T>(in T obj) where T : struct
		{
			var size = Marshal.SizeOf<T>();
			Array.Resize(ref _wBuffer, size + _wBufferIdx);
			var rObj = obj;
			MemoryMarshal.Write(_wBuffer.AsSpan(_wBufferIdx), ref rObj);
			_wBufferIdx += size;
		}

		private void Write(ValuesBase obj)
		{
			const int maxSize = 4 + 4*32;
			Array.Resize(ref _wBuffer, maxSize + _wBufferIdx);
			_wBufferIdx += obj.Write(_wBuffer.AsSpan(_wBufferIdx));
		}

		private void Send(bool hasRequestLength)
		{
			if (hasRequestLength)
			{
				MemoryMarshal.Cast<byte, ushort>(_wBuffer.AsSpan())
					[1] = (ushort)(_wBufferIdx / 4);
			}
			_socket.Send(_wBuffer, _wBufferIdx, SocketFlags.None);
			_wBufferIdx = 0;
		}

		private string ReadString(int len, bool pad)
		{
			var size = len + Pad(len);
			if (_rBuffer == null || _rBuffer.Length < size)
			{
				_rBuffer = new byte[size];
			}
			_socket.Receive(_rBuffer, size, SocketFlags.None);
			return Encoding.UTF8.GetString(_rBuffer, 0, len);
		}

		private ConnectionSuccess _info;
		private List<Format> _formats = new List<Format>();
		private List<(Screen, List<(Depth, List<VisualType>)>)> _screens
			= new List<(Screen, List<(Depth, List<VisualType>)>)>();

		public void Dispose()
		{
			_socket?.Dispose();
		}

		private static int Pad(int n) => (4 - (n % 4)) % 4;

		public void MessageLoop()
		{
			var buffer = new byte[32];
			var span = buffer.AsSpan();
			while (true) // TODO: Loop exit
			{
				System.Threading.Thread.Sleep(1000);
				_socket.Receive(buffer);
				bool handled = false;
				foreach (var e in _events)
				{
					if (e.Opcodes.Contains(buffer[0]))
					{
						e.Read(span);
						handled = true;
						break;
					}
				}
				if (!handled)
				{
					//throw new Exception($"Unknown response code {buffer[0]}");
				}
			}
		}

		private readonly EventBase[] _events =
		{
			new Event<Error>{Opcodes = {0}},
			new Event<ResizeRequestEvent>{Opcodes = {3}},
			new Event<KeyEvent>{Opcodes = {2, 3, 4, 5}},
		};

		private abstract class EventBase
		{
			public HashSet<byte> Opcodes { get; } = new HashSet<byte>();
			public abstract void Read(Span<byte> data);
		}

		private class Event<T> : EventBase where T : struct
		{
			public Action<T> OnEvent;

			public override void Read(Span<byte> data)
			{
				OnEvent?.Invoke(MemoryMarshal.Read<T>(data));
			}
		}

		public void ListenTo<T>(Action<T> action) where T : struct
		{
			_events.OfType<Event<T>>().Single().OnEvent += action;
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

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct Boolean
		{
			private byte _data;

			public static implicit operator bool(Boolean b) => b._data != 0;
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
			public Boolean SameScreen;
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

		private static X11Connection _connection;

		public static EndPoint EndPoint { get; set; }

		private static readonly Dictionary<uint, X11> _windows
			= new Dictionary<uint, X11>();

		private static X11Connection Connect()
		{
			if (_connection == null)
			{
				_connection = new X11Connection();
				if (EndPoint == null)
				{
					GetServer(out var endPoint, out var _);
					EndPoint = endPoint;
				}
				_connection.ListenTo<X11Connection.KeyEvent>(HandleKeyEvent);
				_connection.Connect(EndPoint);
			}
			return _connection;
		}

		private static void HandleKeyEvent(X11Connection.KeyEvent e)
		{
			if (_windows.TryGetValue(e.RootWindow, out var window))
			{
				if (e.IsDown)
				{
					window.KeyDown?.Invoke(e.Keycode);
				} else {
					window.KeyUp?.Invoke(e.Keycode);
				}
			}
		}

		public void Show(Rectangle rect)
		{
			var c = Connect();
			c.CreateWindow(rect);
			c.MessageLoop();
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