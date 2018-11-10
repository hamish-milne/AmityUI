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

		private Point _mousePosition;

		public Point MousePosition
		{
			get => _mousePosition;
			set => throw new NotImplementedException();
		}

		public Rectangle WindowArea
		{
			get
			{
				return _cachedRect;
			}
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

		public WmClient WmClient { get; }
		public WmRoot WmRoot { get; }
		public NetWM NetWM { get; }

		private DrawingContext _icon;
		private DrawingContext _iconMask;

		public IDrawingContext Icon
		{
			get
			{
				if (_icon == null || _icon.IsDisposed)
				{
					_icon?.Dispose();
					_iconMask?.Dispose();
					
					var iconSize = WmRoot.WM_ICON_SIZE;
					_icon = new DrawingContext(_connection,
						new Size((int)iconSize.MaxWidth, (int)iconSize.MaxHeight),
						_screen.Root, 32);
					_iconMask =
						new DrawingContext(_connection, _icon.Size, _screen.Root, 1);
				}
				return _icon;
			}
		}

		public void FlushIcon()
		{
			if (_icon?.IsDisposed == false)
			{
				_icon.CopyPlane(0x80_00_00_00,
					new Rectangle(new Point(0, 0), _icon.Size),
					new Point(0, 0), _iconMask);
				// TODO: Also set NetWM icons here
				WmClient.WM_HINTS = new WmHints // TODO: Use Append here
				{
					Flags = HintFlags.IconPixmap | HintFlags.IconMask,
					IconPixmap = (Pixmap)_icon.Drawable,
					IconMask = (Pixmap)_iconMask.Drawable
				};
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
		private IFontFamily[] _fonts;
		public ReadOnlySpan<IFontFamily> Fonts
		{
			get
			{
				if (_fonts == null) { LoadFonts(); }
				return _fonts;
			}
		}

		private void LoadFonts()
		{
			_connection.Request(new ListFonts { MaxNames = 65535 }, "*-iso10646-?",
				out ListFonts.Reply _, out List<string> fonts);
			var weightValues = fonts.Select(n => new XLFD(n).WeightName).Distinct().ToArray();
			var widthValues = fonts.Select(n => new XLFD(n).SetWidthName).Distinct().ToArray();
			_fonts = fonts.Select(n => new XLFD(n))
				.GroupBy(f => f.FamilyName)
				.Select(g =>  g.All(f => f.PointSize == 0) ?
					new XScalableFont(_connection, g.ToArray()) :
					(IFontFamily)new XBitmapFont(_connection, g.ToArray())
				)
				.ToArray();
		}

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
					//| X11.Event.PropertyChange,
				BackgroundPixel = 0xFFFFFFFF,
			});
			WmClient = new WmClient(c, _wId);
			WmRoot = new WmRoot(c, _screen.Root);
			NetWM = new NetWM(c, _wId);
			WmClient.WM_NAME = "💖 My window! 💖";
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
			if (_cachedRect.Width == e.Width && _cachedRect.Height == e.Height)
			{
				// TODO: Window move event
				return;
			}
			_cachedRect.Width = e.Width;
			_cachedRect.Height = e.Height;
			Resize?.Invoke();
		}

		private void HandleResize(X11.ConfigureNotify e)
		{
			if (_cachedRect.Width == e.Rect.Width && _cachedRect.Height == e.Rect.Height)
			{
				// TODO: Window move event
				return;
			}
			_cachedRect = e.Rect;
			Resize?.Invoke();
		}

		private void HandleMouseMove(X11.MotionNotify e)
		{
			_mousePosition = new Point(e.Data.EventX, e.Data.EventY);
			MouseMove?.Invoke(_mousePosition);
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

		public unsafe void Run()
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
			public Size Size { get; }
			public HAlign TextHAlign { get; set; }
			public VAlign TextVAlign { get; set; }

			private IFont _font;
			public IFont Font
			{
				get => _font;
				set
				{
					var xFont = value as XFont;
					if (xFont != null)
						_c.Request(
							new ChangeGC { ContextID = _gc },
							new GCValues { Font = xFont.GetID() }
						);
					_font = xFont;
				}
			}

			private float _lineWidth = 0;
			public float LineWidth
			{
				get => _lineWidth;
				set
				{
					_c.Request(
						new ChangeGC { ContextID = _gc },
						new GCValues { LineWidth = (ushort)value }
					);
					_lineWidth = value;
				}
			}

			private Color _cachedForeground = Color.White;

			private X11.Transport _c;
			private GContext _gc;
			private Drawable _drawable;
			public Drawable Drawable => _drawable;
			private bool _isWindow;

			public DrawingContext(X11.Transport c, Window window)
			{
				_c = c;
				_c.Request(new CreateGC
				{
					Drawable = _drawable = (Drawable)window,
					ContextID = _gc = (GContext)c.ClaimID()
				},
				new GCValues { Foreground = (Color32)_cachedForeground, GraphicsExposures = false, Background = (Color32)Color.Empty });
				_isWindow = true;
			}

			public DrawingContext(X11.Transport c, Size size, Window window, byte depth = 24)
			{
				Size = size;
				_c = c;
				_c.Request(new CreatePixmap
				{
					Drawable = (Drawable)window,
					Depth = depth,
					Width = (ushort)size.Width,
					Height = (ushort)size.Height,
					PixmapID = (Pixmap)(_drawable = (Drawable)c.ClaimID())
				});
				_c.Request(new CreateGC
				{
					Drawable = _drawable,
					ContextID = _gc = (GContext)c.ClaimID()
				},
				new GCValues { Foreground = (Color32)_cachedForeground, GraphicsExposures = false, Background = (Color32)Color.Empty });
			}

			public bool SetColor(Color? color)
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
						Background = (Color32)Color.Empty // TODO: use this properly
					});
				}
				return true;
			}

			private ArcFillMode _cachedFillMode;
			public ArcFillMode ArcFillMode
			{
				get => _cachedFillMode;
				set
				{
					_c.Request(new ChangeGC
					{
						ContextID = _gc
					}, new GCValues
					{
						ArcMode = (ArcMode)(value - 1)
					});
					_cachedFillMode = value;
				}
			}

			public void Arc(Rectangle rect, float angleA, float angleB)
			{
				Span<Arc> arcs = stackalloc Arc[]
				{
					new Arc(rect, angleA, angleB)
				};
				if (ArcFillMode != ArcFillMode.None && SetColor(Brush))
				{
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
				if (points.Length == 0) { return; }
				Span<X11.Point> xPoints = stackalloc X11.Point[points.Length + 1];
				for (int i = 0; i < points.Length; i++) {
					xPoints[i] = points[i];
				}
				xPoints[points.Length] = points[0];
				if (SetColor(Brush))
				{
					_c.Request(new FillPoly
					{
						Drawable = _drawable,
						GContext = _gc,
						Shape = Shape.Complex
					}, xPoints);
				}
				if (SetColor(Pen))
				{
					_c.Request(new PolyLine
					{
						Drawable = _drawable,
						GContext = _gc
					}, xPoints);
				}
			}

			public bool IsDisposed => _gc == 0;

			public void Dispose()
			{
				_c.Request(new FreeGC
				{
					GContext = _gc
				});
				_c.ReleaseID((uint)_gc);
				_gc = 0;
				if (!_isWindow)
				{
					_c.Request(new FreePixmap
					{
						Pixmap = (Pixmap)_drawable
					});
					_c.ReleaseID((uint)_drawable);
					_drawable = 0;
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

			public void Line(ReadOnlySpan<Point> points)
			{
				if (SetColor(Pen))
				{
					Span<X11.Point> xPoints = stackalloc X11.Point[points.Length];
					for (int i = 0; i < points.Length; i++)
					{
						xPoints[i] = points[i];
					}
					_c.Request(new PolyLine
					{
						CoordinateMode = CoordinateMode.Origin,
						Drawable = _drawable,
						GContext = _gc,
					},
					xPoints);
				}
			}

			public void Rectangle(ReadOnlySpan<Rectangle> rects)
			{
				Span<Rect> xRects = stackalloc Rect[rects.Length];
				for (int i = 0; i < rects.Length; i++)
				{
					xRects[i] = (Rect)rects[i];
				}
				if (SetColor(Brush))
				{
					_c.Request(new PolyFillRectangle
					{
						Drawable = _drawable,
						GContext = _gc,
					}, xRects);
				}
				if (SetColor(Pen))
				{
					_c.Request(new PolyRectangle
					{
						CoordinateMode = CoordinateMode.Origin,
						Drawable = _drawable,
						GContext = _gc,
					}, xRects);
				}
			}

			public void Text(Point position, string text)
			{
				if (Font != null && SetColor(TextColor))
				{
					switch (TextVAlign) {
						case VAlign.Middle:
							position.Y += (Font.Ascent - Font.Descent) / 2;
							break;
						case VAlign.Top:
							position.Y += Font.Ascent;
							break;
					}
					switch (TextHAlign) {
						case HAlign.Right:
							var (left, right) = Font.MeasureText(text);
							position.X -= left;
							break;
						case HAlign.Center:
							var (left1, right1) = Font.MeasureText(text);
							position.X -= (left1 - right1) / 2;
							break;
					}
					// TODO: Support newlines, wrapping etc.?
					_c.Request(new PolyText16
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

			public void CopyPlane(uint mask, Rectangle srcRect, Point dstPos, DrawingContext dst)
			{
				_c.Request(new CopyPlane
				{
					SrcDrawable = _drawable,
					DstDrawable = dst._drawable,
					GContext = dst._gc,
					Dst = new Rect {
						X = (short)dstPos.X,
						Y = (short)dstPos.Y,
						Width = (ushort)srcRect.Width,
						Height = (ushort)srcRect.Height
					},
					SrcX = (short)srcRect.X,
					SrcY = (short)srcRect.Y,
					BitPlane = mask
				});
			}
		}

		private class XScalableFont : IFontFamily
		{
			private Transport _c;
			private XLFD[] _fonts;

			public string Name => _fonts[0].FamilyName;

			public ReadOnlySpan<int> FixedSizes => default;

			public XScalableFont(Transport c, XLFD[] fonts)
			{
				_c = c;
				_fonts = fonts;
			}

			public bool Scalable => true;

			public IFont GetFont(float size, FontSlant slant, FontWeight weight)
			{
				return new XFont(_c, this, _fonts
					.FirstOrDefault(f =>
						f.Slant.ToString() == slant.ToString() &&
						f.WeightName.Equals(weight.ToString(), StringComparison.OrdinalIgnoreCase)
					));
			}
		}

		private class XBitmapFont : IFontFamily
		{
			private Transport _c;
			private XLFD[] _fonts;

			public XBitmapFont(Transport c, XLFD[] fonts)
			{
				_c = c;
				_fonts = fonts;
				_fixedSizes = fonts
					.Select(f => f.PixelSize)
					.Where(p => p != 0)
					.OrderBy(p => p)
					.ToArray();
			}

			private int[] _fixedSizes = Array.Empty<int>();
			public ReadOnlySpan<int> FixedSizes => _fixedSizes;
			public string Name => _fonts[0].FamilyName;
			public bool Scalable => false;
			
			private readonly Dictionary<XLFD, IFont> _cachedSizes
				= new Dictionary<XLFD, IFont>();

			// TODO: Normalize the pixel/point sizes
			public IFont GetFont(float size, FontSlant slant, FontWeight weight)
			{
				if (size <= 0)
				{
					throw new ArgumentOutOfRangeException(nameof(size));
				}
				var iSize = (int)Math.Round(size);
		
				// Find the closest FixedSize
				{
					int i;
					for (i = 0; i < (_fixedSizes.Length-1) && iSize >= _fixedSizes[i+1]; i++) { }
					iSize = _fixedSizes[i];
				}

				// TODO: Scale bitmap fonts?
				var fd = _fonts.First(f => XLFD.ParseSlant(f.Slant) == slant
					&& XLFD.ParseWeight(f.WeightName) == weight && f.PixelSize == iSize);

				if (!_cachedSizes.TryGetValue(fd, out var font))
				{
					_cachedSizes.Add(fd, font = new XFont(_c, this, fd));
				}
				return font;
			}

		}

		private class XFont : IFont
		{
			private Transport _c;
			private XLFD _font;
			private Font? _fontId;
			private int _ascent, _descent;

			public IFontFamily Family { get; }
			public float Size => _font.PointSize / 10f;

			public int Ascent
			{
				get {
					GetID();
					return _ascent;
				}
			}

			public int Descent
			{
				get {
					GetID();
					return _descent;
				}
			}

			public Font GetID()
			{
				if (!_fontId.HasValue)
				{
					_fontId = (Font)_c.ClaimID();
					_c.Request(new OpenFont
					{
						FontID = _fontId.Value
					}, _font.ToString());
					_c.Request(new QueryTextExtents { FontID = _fontId.Value },
						"<test>", out QueryTextExtents.Reply reply);
					_ascent = reply.FontAscent;
					_descent = reply.FontDescent;
				}
				return _fontId.Value;
			}

			public (int left, int right) MeasureText(string text)
			{
				_c.Request(new QueryTextExtents { FontID = GetID() }, text,
					out QueryTextExtents.Reply reply);
				return (reply.OverallLeft, reply.OverallRight);
			}

			public XFont(Transport c, IFontFamily family, XLFD font)
			{
				_c = c;
				_font = font;
				Family = family;
			}

			public void Dispose()
			{
				if (_fontId.HasValue)
				{
					_c.Request(new CloseFont { FontID = _fontId.Value });
					_fontId = null;
				}
			}
		}
	}
}