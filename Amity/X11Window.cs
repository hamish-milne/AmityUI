namespace Amity
{
	using System;
	using System.Net;
	using System.Text.RegularExpressions;
	using System.Linq;
	using System.Drawing;
	using X11;
	using Point = System.Drawing.Point;

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

		public Rectangle WindowArea => throw new NotImplementedException();

		public Rectangle ClientArea => new Rectangle(0, 0, _width, _height);

		public event Action<Point> MouseMove;
		public event Action<int> MouseDown;
		public event Action<int> KeyDown;
		public event Action<int> KeyUp;
		public event Action Resize;
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
				_connection.ListenTo<X11.Expose>(HandleExpose);
				_connection.ListenTo<X11.MotionNotify>(HandleMouseMove);
				_currentID = _connection.Info.ResourceIdBase;
				_screen = _connection.Screens[0].Item1;
			}
			return _connection;
		}

		private void HandleExpose(X11.Expose e)
		{
			Draw?.Invoke();
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
					| X11.Event.StructureNotify
					| X11.Event.Exposure,
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
				Foreground = (Color32)Color.Magenta,
			});
			CreateBuffer((ushort)rect.Width, (ushort)rect.Height);
			c.MessageLoop();
		}

		private uint _wId, _gc;
		private ushort _width;
		private ushort _height;

		private void CreateBuffer(ushort width, ushort height)
		{
			_width = width;
			_height = height;
			Resize?.Invoke();
			Draw?.Invoke();
		}

		public IDrawingContext GetDrawingContext()
		{
			return new DrawingContext(this);
		}

		public void Invalidate()
		{
		}

		private class DrawingContext : IDrawingContext
		{
			public Color? Brush { get; set; }
			public Color? Pen { get; set; }

			private X11Window _parent;

			public DrawingContext(X11Window parent)
			{
				_parent = parent;
			}

			public ReadOnlySpan<string> Fonts => throw new NotImplementedException();

			public void ArcChord(Rectangle rect, float angleA, float angleB)
			{
				throw new NotImplementedException();
			}

			public void ArcSlice(Rectangle rect, float angleA, float angleB)
			{
				throw new NotImplementedException();
			}

			public void BeginPolygon()
			{
				throw new NotImplementedException();
			}

			public void Dispose()
			{
			}

			public void EndPolygon(bool forceClose)
			{
				throw new NotImplementedException();
			}

			public void Image(Span<Color32> data, Size size, Point destination)
			{
				var c = _parent.Connect();
				// There's a request size limit, so we need to upload the image
				// a section at a time
				var maxLinesPerRequest = c.Info.MaxRequestLength / (size.Width * 4);
				if (maxLinesPerRequest <= 0)
				{
					// TODO: Better support for this case?
					throw new Exception("Screen size is too big!");
				}
				for (int y = 0; y < size.Height; y += maxLinesPerRequest)
				{
					var thisHeight = maxLinesPerRequest;
					if (size.Height - y < thisHeight) {
						thisHeight = size.Height - y;
					}
					c.Request(new X11.PutImage
					{
						Format = X11.ImageFormat.ZPixmap,
						Depth = 24,
						Drawable = _parent._wId,
						GContext = _parent._gc,
						Width = (ushort)size.Width,
						Height = (ushort)thisHeight,
						DstX = (short)destination.X,
						DstY = (short)(destination.Y + y),
					},
					data.Slice(y * size.Width, thisHeight * size.Width));
				}
			}

			public void Pixel(Point p)
			{
				throw new NotImplementedException();
			}

			public void Line(Point a, Point b)
			{
				var c = _parent.Connect();
				Span<X11.Point> points = stackalloc X11.Point[2];
				points[0] = new X11.Point{X = (ushort)a.X, Y = (ushort)a.Y};
				points[1] = new X11.Point{X = (ushort)b.X, Y = (ushort)b.Y};
				c.Request(new PolyLine
				{
					CoordinateMode = CoordinateMode.Origin,
					Drawable = _parent._wId,
					GContext = _parent._gc,
				},
				points);
			}

			public void PushPoint(Point next)
			{
				throw new NotImplementedException();
			}

			public void Rectangle(Rectangle rect)
			{
				throw new NotImplementedException();
			}

			public void Text(Point position, string font, string text)
			{
				throw new NotImplementedException();
			}
		}
	}
}