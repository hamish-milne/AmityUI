namespace Amity.X11
{
	using System;
	using System.Net;
	using System.Net.Sockets;
	using System.Text;
	using System.Runtime.InteropServices;

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
		}

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

		public void Write<T>(in T obj) where T : struct
		{
			var size = Marshal.SizeOf<T>();
			Array.Resize(ref _wBuffer, size + _wBufferIdx);
			var rObj = obj;
			MemoryMarshal.Write(_wBuffer.AsSpan(_wBufferIdx), ref rObj);
			_wBufferIdx += size;
		}

		public void Write(IWritable obj)
		{
			const int maxSize = 4 + 4*32;
			Array.Resize(ref _wBuffer, maxSize + _wBufferIdx);
			_wBufferIdx += obj.WriteTo(_wBuffer.AsSpan(_wBufferIdx));
		}

		public void Send(bool hasRequestLength)
		{
			if (hasRequestLength)
			{
				MemoryMarshal.Cast<byte, ushort>(_wBuffer.AsSpan())
					[1] = (ushort)(_wBufferIdx / 4);
			}
			_socket.Send(_wBuffer, _wBufferIdx, SocketFlags.None);
			_wBufferIdx = 0;
		}
		
		public void Dispose()
		{
			_socket.Dispose();
		}

		private static int Pad(int n) => (4 - (n % 4)) % 4;

		public void MessageLoop(Action<byte[]> onRead)
		{
			var buffer = new byte[32];
			while (true) // TODO: Loop exit
			{
				_socket.Receive(buffer);
				onRead.Invoke(buffer);
			}
		}
	}
}