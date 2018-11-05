namespace Amity.X11
{
	using System;
	using System.Net;
	using System.Net.Sockets;
	using System.Linq;
	using System.Text;
	using System.Runtime.InteropServices;
	using System.Collections.Generic;

	public interface X11RequestBase
	{
		byte Opcode { get; }
	}

	public interface X11Request : X11RequestBase
	{
	}

	public interface X11SpanRequest<TSpan> : X11RequestBase
		where TSpan : unmanaged
	{
		void PostWrite(Span<byte> data);
	}

	public interface X11DataRequest<TData> : X11RequestBase, X11RequestData<TData>
	{
	}

	public interface X11RequestReply<TReply> : X11RequestBase
		where TReply : struct
	{
	}

	public interface X11DataRequestReply<TData, TReply> : X11RequestBase, X11RequestData<TData>
		where TReply : struct
	{
	}

	public interface X11Reply
	{
	}

	public interface X11Reply<TData>
	{
		TData Read(Span<byte> data);
	}

	public interface X11SpanReply<TData> where TData : unmanaged
	{
	}

	public interface X11RequestData<TData>
	{
		int GetMaxSize(in TData data);
		int WriteTo(in TData data, Span<byte> output, Span<byte> rData);
	}

	public class Transport : IDisposable
	{
		private readonly Socket _socket;

		public Transport(EndPoint endPoint)
		{
			_socket = new Socket(endPoint.AddressFamily,
				SocketType.Stream, ProtocolType.IP)
				{
					ReceiveTimeout = 1000
				};
			_socket.Connect(endPoint);
			if (_socket?.Connected != true)
			{
				throw new Exception("Connection failed");
			}

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
					throw new Exception(ReadString(response.ReasonLengthBytes));
				case 1:
					Info = Read<ConnectionSuccess>();
					var vendor = ReadString(Info.VendorLength);
					Formats.Clear();
					for (var i = 0; i < Info.FormatCount; i++)
					{
						Formats.Add(Read<Format>());
					}
					Screens.Clear();
					for (var i = 0; i < Info.ScreenCount; i++)
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
						Screens.Add((screen, depths));
					}
					break;
			}

		}

		public ConnectionSuccess Info { get; }
		public List<Format> Formats { get; } = new List<Format>();
		public List<(Screen, List<(Depth, List<VisualType>)>)> Screens { get; }
			= new List<(Screen, List<(Depth, List<VisualType>)>)>();

		private byte[] _rBuffer;
		private byte[] _wBuffer;
		private int _wBufferIdx;

		private static void Grow<T>(ref T[] array, int sizeAtLeast, bool keepData)
		{
			const int padTo = 8;
			sizeAtLeast += padTo - (sizeAtLeast % padTo);
			if (keepData)
			{
				Array.Resize(ref array, sizeAtLeast);
			} else if (array == null || array.Length < sizeAtLeast)
			{
				array = new T[sizeAtLeast];
			}
		}

		public T Read<T>() where T : unmanaged
		{
			var size = Marshal.SizeOf<T>();
			Grow(ref _rBuffer, size, false);
			_socket.Receive(_rBuffer, 0, size, SocketFlags.None);
			return MemoryMarshal.Read<T>(_rBuffer.AsSpan(0, size));
		}

		public string ReadString(int len)
		{
			var size = len + Pad(len);
			Grow(ref _rBuffer, size, false);
			_socket.Receive(_rBuffer, size, SocketFlags.None);
			return Encoding.UTF8.GetString(_rBuffer, 0, len);
		}

		private void Write<T>(in T obj) where T : unmanaged
		{
			var size = Marshal.SizeOf<T>();
			Grow(ref _wBuffer, size + _wBufferIdx, _wBufferIdx > 0);
			var rObj = obj;
			MemoryMarshal.Write(_wBuffer.AsSpan(_wBufferIdx), ref rObj);
			_wBufferIdx += size;
		}

		private void WriteIndirect<TWriter, TData>(in TWriter writer, in TData data)
			where TWriter : struct, X11RequestData<TData>
		{
			Grow(ref _wBuffer, writer.GetMaxSize(data) + _wBufferIdx, _wBufferIdx > 0);
			_wBufferIdx += writer.WriteTo(data, _wBuffer.AsSpan(_wBufferIdx), _wBuffer);
		}

		private void Send(bool hasRequestLength)
		{
			if (hasRequestLength)
			{
				_wBufferIdx += Pad(_wBufferIdx);
				MemoryMarshal.Cast<byte, ushort>(_wBuffer.AsSpan())
					[1] = (ushort)(_wBufferIdx / 4);
			}
			_socket.Send(_wBuffer, _wBufferIdx, SocketFlags.None);
			_wBufferIdx = 0;
		}

		public void Request<T>(in T data) where T : unmanaged, X11Request
		{
			Write(data);
			_wBuffer[0] = data.Opcode;
			Send(true);
		}
		
		public void Request<T, TData>(in T data, TData extra)
			where T : unmanaged, X11DataRequest<TData>
		{
			Write(data);
			_wBuffer[0] = data.Opcode;
			WriteIndirect(data, extra);
			Send(true);
		}

		public void Request<T, TSpan>(in T data, Span<TSpan> extra)
			where T : unmanaged, X11SpanRequest<TSpan>
			where TSpan : unmanaged
		{
			Write(data);
			_wBuffer[0] = data.Opcode;
			var startIdx = _wBufferIdx;
			_wBufferIdx += extra.Length * Marshal.SizeOf<TSpan>();
			Grow(ref _wBuffer, _wBufferIdx, _wBufferIdx > 0);
			MemoryMarshal.Cast<TSpan, byte>(extra).CopyTo(_wBuffer.AsSpan(startIdx));
			data.PostWrite(_wBuffer.AsSpan(0, _wBufferIdx));
			Send(true);
		}

		public void Request<T, TReply>(in T data, out TReply reply)
			where T : unmanaged, X11RequestReply<TReply>
			where TReply : unmanaged, X11Reply
		{
			Write(data);
			_wBuffer[0] = data.Opcode;
			Send(true);
			reply = ReadReply<TReply>(out var _);
			CommitEvents();
		}

		public void Request<T, TReply, TReplyData>(in T data, out TReply reply, out TReplyData replyData)
			where T : unmanaged, X11RequestReply<TReply>
			where TReply : unmanaged, X11Reply<TReplyData>
		{
			Write(data);
			_wBuffer[0] = data.Opcode;
			Send(true);
			(reply, replyData) = ReadReply<TReply, TReplyData>();
			CommitEvents();
		}

		public void Request<T, TReply, TReplyData>(in T data, out TReply reply, out Span<TReplyData> replyData)
			where T : unmanaged, X11RequestReply<TReply>
			where TReply : unmanaged, X11SpanReply<TReplyData>
			where TReplyData : unmanaged
		{
			Write(data);
			_wBuffer[0] = data.Opcode;
			Send(true);
			reply = ReadReply<TReply>(out var length);
			Grow(ref _rBuffer, length, false);
			if (length > 0)
				_socket.Receive(_rBuffer, length, SocketFlags.None);
			replyData = MemoryMarshal.Cast<byte, TReplyData>(_rBuffer.AsSpan(0, length));
			CommitEvents();
		}
		
		public void Request<T, TData, TReply>(in T data, TData extra, out TReply reply)
			where T : unmanaged, X11DataRequestReply<TData, TReply>
			where TReply : unmanaged, X11Reply
		{
			Write(data);
			_wBuffer[0] = data.Opcode;
			WriteIndirect(data, extra);
			Send(true);
			reply = ReadReply<TReply>(out var _);
			CommitEvents();
		}
		
		public void Request<T, TData, TReply, TReplyData>(
			in T data, TData extra, out TReply reply, out TReplyData replyData)
			where T : unmanaged, X11DataRequestReply<TData, TReply>
			where TReply : unmanaged, X11Reply<TReplyData>
		{
			Write(data);
			_wBuffer[0] = data.Opcode;
			WriteIndirect(data, extra);
			Send(true);
			(reply, replyData) = ReadReply<TReply, TReplyData>();
			CommitEvents();
		}
		
		public void Dispose()
		{
			_socket.Dispose();
		}

		private static int Pad(int n) => (4 - (n % 4)) % 4;

		private volatile bool _loop;

		public void DoQuitLoop() => _loop = false;

		private readonly byte[] _msgBuffer = new byte[32];

		// https://stackoverflow.com/a/2556369
		private static bool ActuallyConnected(Socket socket) =>
			!(socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0);

		public void MessageLoop()
		{
			_loop = true;
			var span = _msgBuffer.AsSpan();
			while (_loop && ActuallyConnected(_socket))
			{
				CommitEvents();
				// When the X button is clicked, it sends a KillClient request
				// which immediately de-selects all window events, including
				// DestroyNotify, and the server immediately closes.
				while (ActuallyConnected(_socket) && _socket.Available >= _msgBuffer.Length)
				{
					_socket.Receive(_msgBuffer);
					HandleEvent();
				}
			}
		}

		private void CommitEvents()
		{
			foreach (var e in _events)
				e.Commit();
		}

		private bool HandleEvent()
		{
			foreach (var e in _events)
			{
				if (e.HasOpcode(_msgBuffer[0]))
				{
					e.Read(_msgBuffer.AsSpan());
					return true;
				}
			}
			return false;
		}

		private T ReadReply<T>(out int replyLength) where T : unmanaged
		{
			while (true)
			{
				var buf = _msgBuffer;
				if (Marshal.SizeOf<T>() > _msgBuffer.Length)
				{
					buf = new byte[Marshal.SizeOf<T>()];
				}
				_socket.Receive(buf);
				if (buf[0] == 1)
				{
					replyLength = MemoryMarshal.Cast<byte, int>(buf)[1] * sizeof(uint);
					replyLength -= (buf.Length - _msgBuffer.Length) / sizeof(uint);
					return MemoryMarshal.Read<T>(buf);
				} else {
					HandleEvent();
				}
			}
		}

		private (T, TData) ReadReply<T, TData>()
			where T : unmanaged, X11Reply<TData>
		{
			var reply = ReadReply<T>(out var len);
			Grow(ref _rBuffer, len, false);
			if (len > 0)
				_socket.Receive(_rBuffer, len, SocketFlags.None);
			var data = reply.Read(_rBuffer.AsSpan(0, len));
			return (reply, data);
		}

		private readonly List<EventBase> _events = new List<EventBase>();

		private abstract class EventBase
		{
			public abstract bool HasOpcode(byte opcode);
			public abstract void Read(Span<byte> data);
			public abstract void Commit();
		}

		private class Event<T> : EventBase where T : unmanaged, X11Event
		{
			// NB: This only works because X11 has <64 events (34, at time of writing)
			private static readonly ulong _opcodes = default(T).Opcodes
				.Aggregate((ulong)0, (u, b) => u | (ulong)(1 << b));
			public override bool HasOpcode(byte b) => (_opcodes & (ulong)(1 << b)) != 0;

			public Action<T> OnEvent;

			// TODO: Allow some events to be 'immediate'?

			private T? _eventData;

			public override void Read(Span<byte> data)
			{
				_eventData = MemoryMarshal.Read<T>(data);
			}

			public override void Commit()
			{
				if (_eventData.HasValue)
				{
					var ed = _eventData.Value;
					_eventData = null;
					OnEvent?.Invoke(ed);
				}
			}
		}

		public void ListenTo<T>(Action<T> action) where T : unmanaged, X11Event
		{
			var e = _events.OfType<Event<T>>().SingleOrDefault();
			if (e == null)
			{
				_events.Add(e = new Event<T>());
			}
			e.OnEvent += action;
		}

		private readonly HashSet<uint> _ids = new HashSet<uint>();

		private uint _currentID;

		public uint ClaimID()
		{
			bool hasLooped = false;
			do {
				_currentID++;
				// TODO: Add a case for non-contiguous ResourceIdMask?
				if (_currentID >= Info.ResourceIdMask)
				{
					_currentID = 0;
					if (hasLooped)
					{
						throw new Exception("Out of IDs!");
					}
					hasLooped = true;
				}
			} while (!_ids.Add(_currentID));
			return _currentID + Info.ResourceIdBase;
		}

		public void ReleaseID(uint id)
		{
			_ids.Remove(id);
		}

		private readonly Dictionary<string, uint> _atoms =
			new Dictionary<string, uint>
		{
			{"PRIMARY",              1},
			{"SECONDARY",            2},
			{"ARC",                  3},
			{"ATOM",                 4},
			{"BITMAP",               5},
			{"CARDINAL",             6},
			{"COLORMAP",             7},
			{"CURSOR",               8},
			{"CUT_BUFFER0",          9},
			{"CUT_BUFFER1",          10},
			{"CUT_BUFFER2",          11},
			{"CUT_BUFFER3",          12},
			{"CUT_BUFFER4",          13},
			{"CUT_BUFFER5",          14},
			{"CUT_BUFFER6",          15},
			{"CUT_BUFFER7",          16},
			{"DRAWABLE",             17},
			{"FONT",                 18},
			{"INTEGER",              19},
			{"PIXMAP",               20},
			{"POINT",                21},
			{"RECTANGLE",            22},
			{"RESOURCE_MANAGER",     23},
			{"RGB_COLOR_MAP",        24},
			{"RGB_BEST_MAP",         25},
			{"RGB_BLUE_MAP",         26},
			{"RGB_DEFAULT_MAP",      27},
			{"RGB_GRAY_MAP",         28},
			{"RGB_GREEN_MAP",        29},
			{"RGB_RED_MAP",          30},
			{"STRING",               31},
			{"VISUALID",             32},
			{"WINDOW",               33},
			{"WM_COMMAND",           34},
			{"WM_HINTS",             35},
			{"WM_CLIENT_MACHINE",    36},
			{"WM_ICON_NAME",         37},
			{"WM_ICON_SIZE",         38},
			{"WM_NAME",              39},
			{"WM_NORMAL_HINTS",      40},
			{"WM_SIZE_HINTS",        41},
			{"WM_ZOOM_HINTS",        42},
			{"MIN_SPACE",            43},
			{"NORM_SPACE",           44},
			{"MAX_SPACE",            45},
			{"END_SPACE",            46},
			{"SUPERSCRIPT_X",        47},
			{"SUPERSCRIPT_Y",        48},
			{"SUBSCRIPT_X",          49},
			{"SUBSCRIPT_Y",          50},
			{"UNDERLINE_POSITION",   51},
			{"UNDERLINE_THICKNESS",  52},
			{"STRIKEOUT_ASCENT",     53},
			{"STRIKEOUT_DESCENT",    54},
			{"ITALIC_ANGLE",         55},
			{"X_HEIGHT",             56},
			{"QUAD_WIDTH",           57},
			{"WEIGHT",               58},
			{"POINT_SIZE",           59},
			{"RESOLUTION",           60},
			{"COPYRIGHT",            61},
			{"NOTICE",               62},
			{"FONT_NAME",            63},
			{"FAMILY_NAME",          64},
			{"FULL_NAME",            65},
			{"CAP_HEIGHT",           66},
			{"WM_CLASS",             67},
			{"WM_TRANSIENT_FOR",     68}
		};

		public Atom GetAtom(string name)
		{
			if (!_atoms.TryGetValue(name, out var ret))
			{
				Request(new InternAtom { }, name, out InternAtom.Reply reply);
				_atoms.Add(name, ret = (uint)reply.Atom);
			}
			return (Atom)ret;
		}
	}
}