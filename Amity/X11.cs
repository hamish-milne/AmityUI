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

	// From https://www.x.org/docs/XProtocol/proto.pdf
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

		private static Socket _socket;
		private static BinaryReader _reader;
		private static BinaryWriter _writer;

		[MethodImpl(MethodImplOptions.Synchronized)]
		private static void Connect()
		{
			if (_socket?.Connected == true)
			{
				return;
			}
			_reader?.Dispose();
			_writer?.Dispose();
			if (GetServer(out var endPoint, out var screen))
			{
				_socket = new Socket(endPoint.AddressFamily,
					SocketType.Stream, ProtocolType.IP);
				_socket.Connect(endPoint);
			}
			if (_socket?.Connected != true)
			{
				throw new Exception("Connection failed");
			}
			var stream = new NetworkStream(_socket, false);
			_reader = new BinaryReader(stream, Encoding.UTF8);
			_writer = new BinaryWriter(stream, Encoding.UTF8);

			// Initialize connection
			_writer.Write((byte)154); // Little endian byte order
			_writer.Write((byte)0); // unused
			_writer.Write((ushort)0); // Major version
			_writer.Write((ushort)0); // Minor version
			_writer.Write((ushort)0); // No auth name
			_writer.Write((ushort)0); // No auth data
			_writer.Flush();

			// Response
			var code = _reader.ReadByte();
			if (code == 0)
			{

			}
		}

		private static int Pad(int n) => (4 - (n % 4)) % 4;


		[MethodImpl(MethodImplOptions.Synchronized)]
		private static void Send(byte opcode, ReadOnlySpan<byte> data)
		{
			_writer.Write(opcode);
			_writer.Write((ushort)data.Length);
			foreach (var b in data)
			{
				_writer.Write(b);
			}
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