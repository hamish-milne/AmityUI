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
		int ExpectedLength { get; }
		TData Read(Span<byte> data);
	}

	public interface X11RequestData<TData>
	{
		int MaxSize { get; }
		int WriteTo(in TData data, Span<byte> output, Span<byte> rData);
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

		private void WriteIndirect<TWriter, TData>(in TWriter writer, TData data)
			where TWriter : struct, X11RequestData<TData>
		{
			Grow(ref _wBuffer, writer.MaxSize + _wBufferIdx, _wBufferIdx > 0);
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
			Send(true);
		}

		public void Request<T, TReply>(in T data, out TReply reply)
			where T : unmanaged, X11RequestReply<TReply>
			where TReply : unmanaged, X11Reply
		{
			Write(data);
			_wBuffer[0] = data.Opcode;
			Send(true);
			reply = ReadReply<TReply>();
		}

		public void Request<T, TReply, TReplyData>(in T data, out TReply reply, out TReplyData replyData)
			where T : unmanaged, X11RequestReply<TReply>
			where TReply : unmanaged, X11Reply<TReplyData>
		{
			Write(data);
			_wBuffer[0] = data.Opcode;
			Send(true);
			(reply, replyData) = ReadReply<TReply, TReplyData>();
		}
		
		public void Request<T, TData, TReply>(in T data, TData extra, out TReply reply)
			where T : unmanaged, X11DataRequestReply<TData, TReply>
			where TReply : unmanaged, X11Reply
		{
			Write(data);
			_wBuffer[0] = data.Opcode;
			WriteIndirect(data, extra);
			Send(true);
			reply = ReadReply<TReply>();
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

		private T ReadReply<T>() where T : unmanaged
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

		private (T, TData) ReadReply<T, TData>()
			where T : unmanaged, X11Reply<TData>
		{
			var reply = ReadReply<T>();
			var len = reply.ExpectedLength;
			Grow(ref _rBuffer, len, false);
			_socket.Receive(_rBuffer, len, SocketFlags.None);
			var data = reply.Read(_rBuffer.AsSpan(0, len));
			return (reply, data);
		}

		private readonly List<EventBase> _events = new List<EventBase>();

		private abstract class EventBase
		{
			public abstract bool HasOpcode(byte opcode);
			public abstract void Read(Span<byte> data);
		}

		private class Event<T> : EventBase where T : unmanaged, X11Event
		{
			// NB: This only works because X11 has <64 events (34, at time of writing)
			private static readonly ulong _opcodes = default(T).Opcodes
				.Aggregate((ulong)0, (u, b) => u | (ulong)(1 << b));
			public override bool HasOpcode(byte b) => (_opcodes & (ulong)(1 << b)) != 0;

			public Action<T> OnEvent;

			public override void Read(Span<byte> data)
			{
				OnEvent?.Invoke(MemoryMarshal.Read<T>(data));
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
	}
}