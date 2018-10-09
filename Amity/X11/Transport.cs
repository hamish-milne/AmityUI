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

	public interface X11Request<TWritable> : X11RequestBase
		where TWritable : IWritable
	{
	}

	public interface X11RequestReply<TReply> : X11RequestBase
		where TReply : struct, X11Reply
	{
	}

	public interface X11RequestReply<TWritable, TReply> : X11RequestBase
		where TWritable : IWritable
		where TReply : struct, X11Reply
	{
	}

	public interface X11Reply
	{
	}

	public interface IWritable
	{
		int MaxSize { get; }
		int WriteTo(Span<byte> span);
	}

	public class Transport : IDisposable
	{
		private readonly Socket _socket;

		public Transport(EndPoint endPoint)
		{
			_socket = new Socket(endPoint.AddressFamily,
				SocketType.Stream, ProtocolType.IP);
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

		public T Read<T>() where T : struct
		{
			var size = Marshal.SizeOf<T>();
			if (_rBuffer == null || _rBuffer.Length < size)
			{
				_rBuffer = new byte[size];
			}
			_socket.Receive(_rBuffer, 0, size, SocketFlags.None);
			return MemoryMarshal.Read<T>(_rBuffer.AsSpan(0, size));
		}

		public string ReadString(int len)
		{
			var size = len + Pad(len);
			if (_rBuffer == null || _rBuffer.Length < size)
			{
				_rBuffer = new byte[size];
			}
			_socket.Receive(_rBuffer, size, SocketFlags.None);
			return Encoding.UTF8.GetString(_rBuffer, 0, len);
		}

		private void Write<T>(in T obj) where T : struct
		{
			var size = Marshal.SizeOf<T>();
			Array.Resize(ref _wBuffer, size + _wBufferIdx);
			var rObj = obj;
			MemoryMarshal.Write(_wBuffer.AsSpan(_wBufferIdx), ref rObj);
			_wBufferIdx += size;
		}

		private void Write(IWritable obj)
		{
			Array.Resize(ref _wBuffer, obj.MaxSize + _wBufferIdx);
			_wBufferIdx += obj.WriteTo(_wBuffer.AsSpan(_wBufferIdx));
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

		public void Request<T>(in T data) where T : struct, X11Request
		{
			Write(data);
			_wBuffer[0] = data.Opcode;
			Send(true);
		}
		
		public void Request<T, T1>(in T data, T1 extra)
			where T : struct, X11Request<T1>
			where T1 : IWritable
		{
			Write(data);
			_wBuffer[0] = data.Opcode;
			Write(extra);
			Send(true);
		}

		public void Request<T, TReply>(in T data, out TReply reply)
			where T : struct, X11RequestReply<TReply>
			where TReply : struct, X11Reply
		{
			Write(data);
			_wBuffer[0] = data.Opcode;
			Send(true);
			reply = ReadReply<TReply>();
		}
		
		public void Request<T, T1, TReply>(in T data, T1 extra, out TReply reply)
			where T : struct, X11RequestReply<T1, TReply>
			where TReply : struct, X11Reply
			where T1 : IWritable
		{
			Write(data);
			_wBuffer[0] = data.Opcode;
			Write(extra);
			Send(true);
			reply = ReadReply<TReply>();
		}
		
		public void Dispose()
		{
			_socket.Dispose();
		}

		private static int Pad(int n) => (4 - (n % 4)) % 4;

		private volatile bool _loop;

		public void DoQuitLoop() => _loop = false;

		private readonly byte[] _msgBuffer = new byte[32];

		public void MessageLoop()
		{
			_loop = true;
			var span = _msgBuffer.AsSpan();
			while (_loop)
			{
				while (_socket.Available < _msgBuffer.Length) ;
				_socket.Receive(_msgBuffer);
				HandleEvent();
			}
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

		public T ReadReply<T>() where T : struct, X11Reply
		{
			while (true)
			{
				_socket.Receive(_msgBuffer);
				if (_msgBuffer[0] == 1)
				{
					return MemoryMarshal.Read<T>(_msgBuffer.AsSpan());
				} else {
					HandleEvent();
				}
			}
		}

		private readonly List<EventBase> _events = new List<EventBase>();

		private abstract class EventBase
		{
			public abstract bool HasOpcode(byte opcode);
			public abstract void Read(Span<byte> data);
		}

		private class Event<T> : EventBase where T : struct, X11Event
		{
			// NB: This only works because X11 has <64 events (34, at time of writing)
			private static ulong _opcodes = default(T).Opcodes
				.Aggregate((ulong)0, (u, b) => u | (ulong)(1 << b));
			public override bool HasOpcode(byte b) => (_opcodes & (ulong)(1 << b)) != 0;

			public Action<T> OnEvent;

			public override void Read(Span<byte> data)
			{
				OnEvent?.Invoke(MemoryMarshal.Read<T>(data));
			}
		}

		public void ListenTo<T>(Action<T> action) where T : struct, X11Event
		{
			var e = _events.OfType<Event<T>>().SingleOrDefault();
			if (e == null)
			{
				_events.Add(e = new Event<T>());
			}
			e.OnEvent += action;
		}
	}
}