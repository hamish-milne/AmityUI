namespace Amity
{
	using System;
	using System.Net;
	using System.Text.RegularExpressions;
	using System.Linq;
	using System.Drawing;
	using System.Collections.Generic;

	// From https://www.x.org/docs/XProtocol/proto.pdf

	public class X11Window : IWindowAPI
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

		private static X11.Protocol _connection;

		public static EndPoint EndPoint { get; set; }

		private static readonly Dictionary<uint, X11Window> _windows
			= new Dictionary<uint, X11Window>();

		private static X11.Protocol Connect()
		{
			if (_connection == null)
			{
				if (EndPoint == null)
				{
					GetServer(out var endPoint, out var _);
					EndPoint = endPoint;
				}
				_connection = new X11.Protocol(EndPoint);
				_connection.ListenTo<X11.KeyEvent>(HandleKeyEvent);
				_connection.Initialize();
			}
			return _connection;
		}

		private static void HandleKeyEvent(X11.KeyEvent e)
		{
			if (_windows.TryGetValue(e.EventWindow, out var window))
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
			_windows[c.CreateWindow(rect)] = this;
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