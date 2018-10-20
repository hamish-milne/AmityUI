namespace Amity
{
	using System;
	using System.Net;
	using System.Text.RegularExpressions;
	using System.Linq;
	using System.Drawing;
	using X11;
	using Point = System.Drawing.Point;
	using System.Collections.Generic;

	// From https://www.x.org/docs/XProtocol/proto.pdf

	public class X11Window : IWindow
	{
		// TODO: Check for IP connection as well
		public static bool IsSupported => GetServer(out var _, out var _);

		public static IWindow Factory(bool force)
		{
			return force || IsSupported ? new X11Window() : null;
		}

		private static readonly Regex _serverPattern =
			new Regex(@"^([\w\.]*)(/unix)?:(\d+)(?:\.(\d+))?$");

		public static bool GetServer(out EndPoint endpoint, out int screen)
		{
			var match = _serverPattern.Match(
				Environment.GetEnvironmentVariable("DISPLAY") ?? "");
			if (!match.Success)
			{
				screen = 0;
				endpoint = new IPEndPoint(IPAddress.Loopback, 6000);
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

		public Point MousePosition
		{
			get => throw new NotImplementedException();
			set => throw new NotImplementedException();
		}

		public Rectangle WindowArea
		{
			get => throw new NotImplementedException();
			set => throw new NotImplementedException();
		}

		private Rectangle _cachedRect;

		public Rectangle ClientArea
		{
			get
			{
				return _cachedRect;
			}
			set
			{
				_connection.Request(new X11.ConfigureWindow
				{
					Window = _wId
				}, new X11.ConfigurationValues
				{
					X = (short)value.X,
					Y = (short)value.Y,
					Width = (ushort)value.Width,
					Height = (ushort)value.Height
				});
			}
		}

		public event Action<Point> MouseMove;
		public event Action<int> MouseDown;
		public event Action<int> KeyDown;
		public event Action<int> KeyUp;
		public event Action Resize;
		public event Action Draw;

		private readonly X11.Transport _connection;

		public static EndPoint EndPoint { get; set; }

		public X11Window()
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
			_connection.ListenTo<X11.DestroyNotify>(e => _connection.DoQuitLoop());
			_connection.ListenTo<X11.Error>(e => throw new Exception(e.ToString()));
			_screen = _connection.Screens[0].Item1;

			var c = _connection;
			_wId = (Window)c.ClaimID();
			c.Request(new X11.CreateWindow
			{
				//Rect = (X11.Rect)rect,
				Visual = _screen.RootVisual,
				Parent = _screen.Root,
				Depth = _screen.RootDepth,
				WindowId = _wId,
				Class = X11.WindowClass.InputOutput,
				Rect = (Rect)new Rectangle(0, 0, 100, 100)
			},
			new X11.WindowValues
			{
				EventMask = X11.Event.KeyPress
					| X11.Event.KeyRelease
					//| X11.Event.PointerMotion
					| X11.Event.ResizeRedirect
					| X11.Event.StructureNotify
					| X11.Event.Exposure
					| X11.Event.VisibilityChange,
				BackgroundPixel = 0xFFFFFFFF,
			});
			var props = new ICCMProperties(c, _wId);
			props.WM_NAME = "My window!";
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
			_cachedRect.Width = e.Width;
			_cachedRect.Height = e.Height;
			Resize?.Invoke();
		}

		private void HandleResize(X11.ConfigureNotify e)
		{
			_cachedRect = e.Rect;
			Resize?.Invoke();
		}

		private void HandleMouseMove(X11.MotionNotify e)
		{
			MouseMove?.Invoke(new Point(e.Data.EventX, e.Data.EventY));
		}

		private static X11.Screen _screen;

		public bool IsVisible
		{
			get => throw new NotImplementedException();
			set
			{
				_connection.Request(new X11.MapWindow
				{
					Window = _wId
				});
				Resize?.Invoke();
			}
		}

		public void Run()
		{
			_connection.MessageLoop();
		}

		private Window _wId;

		public IDrawingContext CreateDrawingContext()
		{
			return new DrawingContext(_connection, _wId);
		}

		public IDrawingContext CreateBitmap(Size size)
		{
			return new DrawingContext(_connection, size, _wId);
		}

		public void Invalidate()
		{
		}

		private class DrawingContext : IDrawingContext
		{
			public Color? Brush { get; set; }
			public Color? Pen { get; set; }
			public Color? TextColor { get; set; }

			private Color _cachedForeground = Color.White;

			private X11.Transport _c;
			private GContext _gc;
			private Drawable _drawable;
			private bool _isWindow;

			public DrawingContext(X11.Transport c, Window window)
			{
				_c = c;
				_c.Request(new CreateGC
				{
					Drawable = _drawable = (Drawable)window,
					ContextID = _gc = (GContext)c.ClaimID()
				},
				new GCValues { Foreground = (Color32)_cachedForeground });
				_isWindow = true;
			}

			public DrawingContext(X11.Transport c, Size size, Window window)
			{
				_c = c;
				_c.Request(new CreatePixmap
				{
					Drawable = (Drawable)window,
					Depth = 24,
					Width = (ushort)size.Width,
					Height = (ushort)size.Height,
					PixmapID = (Pixmap)(_drawable = (Drawable)c.ClaimID())
				});
				_c.Request(new CreateGC
				{
					Drawable = _drawable,
					ContextID = _gc = (GContext)c.ClaimID()
				},
				new GCValues { Foreground = (Color32)_cachedForeground });
			}

			private bool SetColor(Color? color)
			{
				if (!color.HasValue) { return false; }
				if (_cachedForeground != color)
				{
					_cachedForeground = color.Value;
					_c.Request(new ChangeGC
					{
						ContextID = _gc,
					}, new GCValues
					{
						Foreground = (Color32)_cachedForeground,
						Background = (Color32)Color.Black // TODO: use this properly
					});
				}
				return true;
			}

			private ArcFillMode _cachedFillMode;
			private void SetArcFill(ArcFillMode fillMode)
			{
				if (_cachedFillMode != fillMode && fillMode != ArcFillMode.None)
				{
					_c.Request(new ChangeGC
					{
						ContextID = _gc
					}, new GCValues
					{
						ArcMode = (ArcMode)(fillMode - 1)
					});
					_cachedFillMode = fillMode;
				}
			}

			public ReadOnlySpan<string> Fonts => throw new NotImplementedException();

			public void Arc(Rectangle rect, float angleA, float angleB,
				ArcFillMode fillMode)
			{
				Span<Arc> arcs = stackalloc Arc[]
				{
					new Arc(rect, angleA, angleB)
				};
				if (fillMode != ArcFillMode.None && SetColor(Brush))
				{
					SetArcFill(fillMode);
					_c.Request(new PolyFillArc
						{
							Drawable = _drawable,
							GContext = _gc
						}, arcs);
				}
				if (SetColor(Pen))
				{
					_c.Request(new PolyArc
					{
						Drawable = _drawable,
						GContext = _gc
					}, arcs);
				}
			}

			public void Polygon(ReadOnlySpan<Point> points)
			{
				throw new NotImplementedException();
			}

			public void Dispose()
			{
				_c.Request(new FreeGC
				{
					GContext = _gc
				});
				_c.ReleaseID((uint)_gc);
				if (!_isWindow)
				{
					_c.Request(new FreePixmap
					{
						Pixmap = (Pixmap)_drawable
					});
					_c.ReleaseID((uint)_drawable);
				}
			}

			public void Image(Span<Color32> data, Size size, Point destination)
			{
				// There's a request size limit, so we need to upload the image
				// a section at a time
				var maxLinesPerRequest = _c.Info.MaxRequestLength / (size.Width * 4);
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
					_c.Request(new X11.PutImage
					{
						Format = X11.ImageFormat.ZPixmap,
						Depth = 24,
						Drawable = _drawable,
						GContext = _gc,
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
				if (SetColor(Pen))
				{
					Span<X11.Point> points = stackalloc X11.Point[2];
					points[0] = new X11.Point{X = (ushort)a.X, Y = (ushort)a.Y};
					points[1] = new X11.Point{X = (ushort)b.X, Y = (ushort)b.Y};
					_c.Request(new PolyLine
					{
						CoordinateMode = CoordinateMode.Origin,
						Drawable = _drawable,
						GContext = _gc,
					},
					points);
				}
			}

			public void Rectangle(Rectangle rect)
			{
				Span<Rect> rects = stackalloc Rect[1];
				rects[0] = (Rect)rect;
				if (SetColor(Brush))
				{
					_c.Request(new PolyFillRectangle
					{
						Drawable = _drawable,
						GContext = _gc,
					}, rects);
				}
				if (SetColor(Pen))
				{
					_c.Request(new PolyRectangle
					{
						CoordinateMode = CoordinateMode.Origin,
						Drawable = _drawable,
						GContext = _gc,
					}, rects);
				}
			}

			public void Text(Point position, string font, string text)
			{
				if (SetColor(TextColor))
				{
					// TODO: Support newlines, wrapping etc.?
					_c.Request(new ImageText16
					{
						Drawable = _drawable,
						GContext = _gc,
						X = (short)position.X,
						Y = (short)position.Y,
					},
					text);
				}
			}

			public void CopyTo(Rectangle srcRect, Point dstPos, IDrawingContext dst)
			{
				var dstC = (DrawingContext)dst;
				_c.Request(new CopyArea
				{
					SrcDrawable = _drawable,
					DstDrawable = dstC._drawable,
					GContext = dstC._gc,
					Dst = new Rect {
						X = (short)dstPos.X,
						Y = (short)dstPos.Y,
						Width = (ushort)srcRect.Width,
						Height = (ushort)srcRect.Height
					},
					SrcX = (short)srcRect.X,
					SrcY = (short)srcRect.Y
				});
			}
		}
	}
}