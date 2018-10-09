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

		public Span<Color32> Buffer => _buffer.AsSpan();

		public Rectangle WindowArea => throw new NotImplementedException();

		public Rectangle ClientArea => new Rectangle(0, 0, _width, _height);

		public event Action<Point> MouseMove;
		public event Action<int> MouseDown;
		public event Action<int> KeyDown;
		public event Action<int> KeyUp;
		public event Action Paint;
		public event Action Draw;

		private X11.Transport _connection;

		public static EndPoint EndPoint { get; set; }

		private X11.Transport Connect()
		{
			if (_connection == null)
			{
				if (EndPoint == null)
				{
					GetServer(out var endPoint, out var _);
					EndPoint = endPoint;
				}
				_connection = new X11.Transport(EndPoint);
				_connection.ListenTo<X11.KeyEvent>(HandleKeyEvent);
				_connection.ListenTo<X11.ResizeRequestEvent>(HandleResize);
				_connection.ListenTo<X11.ConfigureNotify>(HandleResize);
				//_connection.ListenTo<X11.MotionNotify>(HandleMouseMove);
				_currentID = _connection.Info.ResourceIdBase;
				_screen = _connection.Screens[0].Item1;
			}
			return _connection;
		}

		private void HandleKeyEvent(X11.KeyEvent e)
		{
			if (e.IsDown)
			{
				KeyDown?.Invoke(e.Keycode);
			} else {
				KeyUp?.Invoke(e.Keycode);
			}
		}

		private void HandleResize(X11.ResizeRequestEvent e)
		{
			CreateBuffer(e.Width, e.Height);
		}

		private void HandleResize(X11.ConfigureNotify e)
		{
			CreateBuffer(e.Rect.Width, e.Rect.Height);
		}

		private void HandleMouseMove(X11.MotionNotify e)
		{
			MouseMove?.Invoke(new Point(e.Data.EventX, e.Data.EventY));
		}

		private static uint _currentID;
		private static X11.Screen _screen;

		public void Show(Rectangle rect)
		{
			var c = Connect();

			_wId = ++_currentID;
			c.Request(new X11.CreateWindow
			{
				Rect = (X11.Rect)rect,
				Visual = _screen.RootVisual,
				Parent = _screen.Root,
				Depth = _screen.RootDepth,
				WindowId = _wId,
				Class = X11.WindowClass.InputOutput,
			},
			new X11.WindowValues
			{
				EventMask = X11.Event.KeyPress
					| X11.Event.KeyRelease
					| X11.Event.PointerMotion
					| X11.Event.ResizeRedirect
					| X11.Event.StructureNotify,
				BackgroundPixel = 0x00FFFFFF,
			});
			c.Request(new X11.MapWindow
			{
				Window = _wId
			});
			c.Request(new X11.ClearArea
			{
				Window = _wId,
				Rect = (X11.Rect)rect,
			});
			_gc = ++_currentID;
			c.Request(new X11.CreateGC
			{
				ContextID = _gc,
				Drawable = _wId,
			},
			new X11.GCValues
			{
			});
			CreateBuffer((ushort)rect.Width, (ushort)rect.Height);
			c.MessageLoop();
		}

		private uint _wId, _gc;
		private Color32[] _buffer;
		private ushort _width;
		private ushort _height;

		private void CreateBuffer(ushort width, ushort height)
		{
			_buffer = new Color32[width * height];
			_width = width;
			_height = height;
			Paint?.Invoke();
			Invalidate();
		}

		public IDrawingContext GetDrawingContext()
		{
			throw new NotImplementedException();
		}

		public void Invalidate()
		{
			var c = Connect();
			// There's a request size limit, so we need to upload the image
			// a section at a time
			var maxLinesPerRequest = c.Info.MaxRequestLength / (_width * 4);
			if (maxLinesPerRequest <= 0)
			{
				// TODO: Better support for this case?
				throw new Exception("Screen size is too big!");
			}
			for (int y = 0; y < _height; y += maxLinesPerRequest)
			{
				var thisHeight = maxLinesPerRequest;
				if (_height - y < thisHeight) {
					thisHeight = _height - y;
				}
				c.Request(new X11.PutImage
				{
					Format = X11.ImageFormat.ZPixmap,
					Depth = 24,
					Drawable = _wId,
					GContext = _gc,
					Width = _width,
					Height = (ushort)thisHeight,
					DstX = 0,
					DstY = (short)y,
				},
				new X11.Image
				{
					ImageData = _buffer.AsMemory(y * _width, thisHeight * _width)
				});
			}
		}
	}
}