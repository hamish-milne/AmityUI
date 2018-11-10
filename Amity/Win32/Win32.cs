namespace Amity
{
	using System;
	using System.Linq;
	using System.Drawing;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Runtime.InteropServices;

	public partial class Win32 : IWindow
	{
		public static bool IsSupported
			=> Environment.OSVersion.Platform == PlatformID.Win32NT;
		
		public static IWindow Factory(bool force)
		{
			return force || IsSupported ? new Win32() : null;
		}

		private FontFamily[] _fonts;

		private class FontFamily : IFontFamily
		{
			public FontFamily(IntPtr hdc, string name)
			{
				HDC = hdc;
				Name = name;
			}

			public IntPtr HDC { get; }
			public string Name { get; }

			public ReadOnlySpan<int> FixedSizes => Array.Empty<int>();

			public bool Scalable => true;

			public IFont GetFont(float size, FontSlant slant, Amity.FontWeight weight)
			{
				return new Font(this, (int)size, slant != FontSlant.Roman, weight);
			}
		}

		private class Font : IFont
		{
			public Font(FontFamily family, int size, bool italic, Amity.FontWeight weight)
			{
				Size = size;
				FontPtr = CreateFontIndirectW(new LOGFONTW
				{
					lfCharSet = FontCharSet.DEFAULT_CHARSET,
					lfFaceName = family.Name,
					lfHeight = (int)size,
					lfItalic = italic,
					lfWeight = FontWeight.FW_MEDIUM + (100 * (int)weight)
				});
				ThrowError(GetTextMetricsW(family.HDC, out var lptm) == false);
				Ascent = lptm.tmAscent;
				Descent = lptm.tmDescent;
			}

			public IntPtr FontPtr { get; private set; }

			public IFontFamily Family { get; }

			public float Size { get; }

			public int Ascent { get; }

			public int Descent { get; }

			public void Dispose()
			{
				DeleteObject(FontPtr);
				FontPtr = IntPtr.Zero;
			}

			public (int left, int right) MeasureText(string text)
			{
				ThrowError(GetTextExtentPoint32W(((FontFamily)Family).HDC, text, text.Length, out var size) == false);
				return (0, size.cx); // TODO: Proper right-left metrics here
			}
		}

		public ReadOnlySpan<IFontFamily> Fonts
		{
			get
			{
				if (_fonts == null)
				{
					var hdc = GetDC(_hwnd);
					var logfont = new LOGFONTW
					{
						lfCharSet = FontCharSet.DEFAULT_CHARSET,
						lfFaceName = "",
						lfPitchAndFamily = 0
					};
					var fontsList = new HashSet<string>();
					int EnumFontFamExProc(
						in LOGFONTW lpelfe,
						in NEWTEXTMETRICEX lpntme,
						int FontType,
						IntPtr lParam
					)
					{
						fontsList.Add(lpelfe.lfFaceName);
						return 1;
					}
					EnumFontFamiliesExW(hdc, ref logfont, EnumFontFamExProc, IntPtr.Zero, 0);
					_fonts = fontsList.Select(n => new FontFamily(hdc, n)).ToArray();
				}
				return _fonts;
			}
		}

		public Rectangle WindowArea
		{
			get {
				ThrowError(GetWindowRect(_hwnd, out var rect) == 0);
				return new Rectangle(
					rect.left,
					rect.top,
					rect.right - rect.left,
					rect.bottom - rect.top
				);
			}
			set => throw new NotImplementedException();
		}

		public Rectangle ClientArea
		{
			get {
				ThrowError(GetClientRect(_hwnd, out var rect) == 0);
				return new Rectangle(
					rect.left,
					rect.top,
					rect.right - rect.left,
					rect.bottom - rect.top
				);
			}
			set => ThrowError(!SetWindowPos(_hwnd, IntPtr.Zero,
				value.X, value.Y, value.Width, value.Height, SWP.NoZOrder));
		}

		private static void ThrowError(bool hasError)
		{
			if (hasError)
			{
				var code = GetLastError();
				if (code != 0)
				{
					var ret = FormatMessage(FORMAT_MESSAGE.ALLOCATE_BUFFER
						| FORMAT_MESSAGE.FROM_SYSTEM,
						IntPtr.Zero,
						code,
						0,
						out var lpBuffer,
						0,
						IntPtr.Zero);
					if (ret <= 0)
					{
						throw new Exception(
							$"Error code {code} (format message failed)");
					} else {
						throw new Exception(lpBuffer);
					}
				}
				throw new Exception(
					"Something went wrong, but no error code was set");
			}
		}

		// This native callback is static to avoid leaking memory
		private static readonly WNDPROC ClassWndProc;

		private static readonly Dictionary<IntPtr, Win32> _instances
			= new Dictionary<IntPtr, Win32>();
		private static Exception _exceptionFromCallback;

		// Limit the repaint rate to avoid flickering
		private static Stopwatch _stopwatch = new Stopwatch();

		private double _lastRepaintTime = double.NegativeInfinity;
		private const double RepaintPeriod = 0.02;

		static Win32()
		{
			_stopwatch.Start();
			ClassWndProc = (hwnd, uMsg, wParam, lParam) =>
			{
				try
				{
					_instances.TryGetValue(hwnd, out var inst);
					if (inst != null)
					{
						inst.HandleMessage(uMsg, wParam, lParam);
						if (!inst._isValid) {
							inst._isValid = true;
							var t = _stopwatch.Elapsed.TotalSeconds;
							if (t - inst._lastRepaintTime > RepaintPeriod)
							{
								inst._lastRepaintTime = t;
								//InvalidateRect(hwnd, IntPtr.Zero, 0);
							}
						}
					}
				} catch(Exception e)
				{
					_exceptionFromCallback = e;
				}
				return DefWindowProcW(hwnd, uMsg, wParam, lParam);
			};
		}

		private bool _isValid = true;

		public void Invalidate() => _isValid = false;

		private void HandleMessage(WM uMsg, UIntPtr wParam, IntPtr lParam)
		{
			switch (uMsg)
			{
				case WM.DESTROY:
					PostQuitMessage(0);
					break;
				case WM.SIZE:
					Resize?.Invoke();
					break;
				case WM.PAINT:
					BeginPaint(_hwnd, out var paint);
					Draw?.Invoke();
					EndPaint(_hwnd, ref paint);
					break;
				case WM.MOUSEMOVE:
					var xPos = (short)(ushort)(uint)lParam.ToInt32();
					var yPos = (short)(ushort)((uint)lParam.ToInt32() >> 16);
					MousePosition = new Point(xPos, yPos);
					MouseMove?.Invoke(MousePosition);
					break;
			}
		}

		private IntPtr _hwnd;
		private IntPtr _dstDc;

		public event Action<Point> MouseMove;
		public event Action<int> MouseDown;
		public event Action<int> KeyDown;
		public event Action<int> KeyUp;
		public event Action Resize;
		public event Action Draw;

		// TODO: Warp pointer
		public Point MousePosition { get; set; }

		public Win32()
		{
			
			// SetProcessDpiAwarenessContext(
			// 	DPI_AWARENESS_CONTEXT.PER_MONITOR_AWARE_V2);
			var hInstance = GetModuleHandle(null);
			var wndClass = new WNDCLASSEXW
			{
				cbSize = Marshal.SizeOf(typeof(WNDCLASSEXW)),
				style = CS.HREDRAW | CS.VREDRAW,
				lpfnWndProc = ClassWndProc,
				hInstance = hInstance,
				hIcon = LoadIconW(IntPtr.Zero, new IntPtr(32516)),
				hCursor = LoadCursorW(IntPtr.Zero, new IntPtr(32512)),
				lpszClassName = "AmityWindowClass"
			};
			var atom = RegisterClassExW(ref wndClass);
			ThrowError(atom == 0);

			_hwnd = CreateWindowExW(
				WS_EX.OVERLAPPEDWINDOW | WS_EX.APPWINDOW,
				new IntPtr(atom),
				"My window!",
				WS.OVERLAPPEDWINDOW,
				0x80000000,
				0x80000000,
				0x80000000,
				0x80000000,
				IntPtr.Zero,
				IntPtr.Zero,
				hInstance,
				IntPtr.Zero
			);
			ThrowError(_hwnd == IntPtr.Zero);
			_instances[_hwnd] = this;

			_dstDc = GetDC(_hwnd);
		}

		public bool IsVisible
		{
			get => IsWindowVisible(_hwnd);
			set
			{
				if (IsVisible)
				{
					ShowWindow(_hwnd, SW.Hide);
				} else {
					ShowWindow(_hwnd, SW.ShowNormal);
					Resize?.Invoke();
				}
			}
		}

		public IDrawingContext Icon => throw new NotImplementedException();

		public void Run()
		{
			while (GetMessage(out var msg, _hwnd, 0, 0) != 0)
			{
				TranslateMessage(ref msg);
				DispatchMessage(ref msg);
				if (msg.message == WM.NULL)
				{
					break;
				}
				if (_exceptionFromCallback != null)
				{
					throw new Exception(
						"Exception in callback", _exceptionFromCallback);
				}
			}
		}

		public IDrawingContext CreateDrawingContext()
		{
			return new DrawingContext(_hwnd);
		}

		public IDrawingContext CreateBitmap(Size size)
		{
			return new DrawingContext(_hwnd, size);
		}

		private class DrawingContext : IDrawingContext
		{
			private IntPtr _hdc;
			private IntPtr _bitmap;

			private void ConfigureDC()
			{
				SetBkMode(_hdc, BkMode.TRANSPARENT);
				SelectObject(_hdc, GetStockObject(StockObjects.DC_PEN));
				SelectObject(_hdc, GetStockObject(StockObjects.DC_BRUSH));
			}

			public DrawingContext(IntPtr hwnd)
			{
				_hdc = GetDC(hwnd);
				ConfigureDC();
			}

			public DrawingContext(IntPtr hwnd, Size size)
			{
				var tmpDc = GetDC(hwnd);
				_hdc = CreateCompatibleDC(tmpDc);
				_bitmap = CreateCompatibleBitmap(tmpDc, size.Width, size.Height);
				ReleaseDC(tmpDc);
				SelectObject(_hdc, _bitmap);
				ConfigureDC();
			}

			private static Color ToColor(uint cr) =>
				Color.FromArgb((byte)(cr), (byte)(cr >> 8), (byte)(cr >> 16));

			private static uint ToColorRef(Color color) =>
				(uint)((color.R) | (color.G << 8) | (color.B << 16));

			public Color? Brush
			{
				get => ToColor(GetDCBrushColor(_hdc));
				set
				{
					ThrowError(SelectObject(_hdc, GetStockObject(value == null
						? StockObjects.NULL_BRUSH : StockObjects.DC_BRUSH))
						== IntPtr.Zero);
					if (value.HasValue) {
						ThrowError(
							SetDCBrushColor(_hdc, ToColorRef(value.Value))
							== uint.MaxValue);
					}
				}
			}

			public Color? Pen
			{
				get => ToColor(GetDCPenColor(_hdc));
				set
				{
					ThrowError(SelectObject(_hdc, GetStockObject(value == null
						? StockObjects.NULL_PEN : StockObjects.DC_PEN))
						== IntPtr.Zero);
					if (value.HasValue) {
						ThrowError(SetDCPenColor(_hdc, ToColorRef(value.Value))
						== uint.MaxValue);
					}
				}
			}

			private bool _textEnabled = true;
			public Color? TextColor
			{
				get => _textEnabled ? ToColor(GetTextColor(_hdc)) : default;
				set
				{
					if (value.HasValue)
					{
						ThrowError(SetTextColor(_hdc, ToColorRef(value.Value))
						== uint.MaxValue);
						_textEnabled = true;
					} else {
						_textEnabled = false;
					}
				}
			}

			private Font _font;
			public IFont Font
			{
				get => _font;
				set
				{
					_font = (Font)value;
					if (_font != null) {
						SelectObject(_hdc, _font.FontPtr);
					}
				}
			}

			// TODO: Implement these:
			public float LineWidth { get; set; }

			public ArcFillMode ArcFillMode { get; set; }
			public HAlign TextHAlign { get; set; }
			public VAlign TextVAlign { get; set; }

			public void Arc(Rectangle rect, float angleA, float angleB)
			{
				var radialLength = Math.Max(rect.Width, rect.Height) * 100;
				var cx = (rect.Right - rect.Left) / 2;
				var cy = (rect.Bottom - rect.Top) / 2;
				angleA *= (float)Math.PI / 180;
				angleB *= (float)Math.PI / 180;
				var x3 = (int)(Math.Cos(angleB) * radialLength) + cx;
				var y3 = (int)(Math.Sin(angleB) * radialLength) + cy;
				var x4 = (int)(Math.Cos(angleA) * radialLength) + cx;
				var y4 = (int)(Math.Sin(angleA) * radialLength) + cy;
				switch (ArcFillMode)
				{
					case ArcFillMode.Chord:
						ThrowError(!Chord(_hdc,
							rect.Left, rect.Top,
							rect.Right, rect.Bottom,
							x3, y3, x4, y4
						));
						break;
					case ArcFillMode.Slice:
						ThrowError(!Pie(_hdc,
							rect.Left, rect.Top,
							rect.Right, rect.Bottom,
							x3, y3, x4, y4
						));
						break;
					default:
						ThrowError(!Win32.Arc(_hdc,
							rect.Left, rect.Top,
							rect.Right, rect.Bottom,
							x3, y3, x4, y4
						));
						break;

				}
			}

			public void Polygon(ReadOnlySpan<Point> points)
			{
				Span<POINT> winPoints = points.Length < 100
					? stackalloc POINT[points.Length]
					: new POINT[points.Length];
				for (int i = 0; i < points.Length; i++) {
					winPoints[i] = new POINT{x = points[i].X, y = points[i].Y};
				}
				ThrowError(!Win32.Polygon(_hdc, in winPoints[0], winPoints.Length));
			}

			public void Dispose()
			{
				ReleaseDC(_hdc);
				_hdc = IntPtr.Zero;
				ReleaseDC(_bitmap);
				_bitmap = IntPtr.Zero;
			}

			~DrawingContext()
			{
				Dispose();
			}

			public void Line(ReadOnlySpan<Point> points)
			{
				// TODO: Use PolyLine here?
				ThrowError(MoveToEx(_hdc, points[0].X, points[0].Y, out var _) == 0);
				foreach (var p in points.Slice(1))
				{
					ThrowError(LineTo(_hdc, p.X, p.Y) == 0);
				}
			}

			public void Rectangle(ReadOnlySpan<Rectangle> rects)
			{
				foreach (var rect in rects)
					ThrowError(Win32.Rectangle(_hdc,
						rect.Left, rect.Top, rect.Right, rect.Bottom) == 0);
			}

			public void Text(Point position, string text)
			{
				if (!_textEnabled || Font == null) { return; }
				switch (TextHAlign)
				{
					case HAlign.Center: position.X -= Font.MeasureText(text).right/2; break;
					case HAlign.Right: position.X -= Font.MeasureText(text).right; break;
				}
				switch (TextVAlign)
				{
					case VAlign.Middle: position.Y -= Font.Ascent/2; break;
					case VAlign.Bottom: position.Y -= Font.Ascent; break;
				}

				ExtTextOutW(_hdc, position.X, position.Y,
					0, null, text, (uint)text.Length, null);
				// TODO: Allow formatting etc?
				// var dtp = new DRAWTEXTPARAMS
				// {
				// 	cbSize = (uint)Marshal.SizeOf<DRAWTEXTPARAMS>(),
				// 	iTabLength = 4,
				// 	iLeftMargin = 0,
				// 	iRightMargin = 0
				// };
				// RECT rect = new Rectangle(position, new Size(0, 0));
				// Win32.DrawTextExW(_hdc, text, text.Length, ref rect,
				// 	DT.NoClip | DT.Left | DT.Top, ref dtp);
			}

			public unsafe void Image(
				Span<Color32> data, Size size, Point destination)
			{
				if (data.Length != (size.Width * size.Height))
				{
					throw new ArgumentOutOfRangeException(
			$"Buffer length {data.Length} doesn't match image size {size}");
				}
				var lpbmi = new BITMAPINFOHEADER
				{
					biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
					biWidth = size.Width,
					biHeight = -size.Height,
					biPlanes = 1,
					biBitCount = 32,
					biCompression = BI.RGB
				};
				fixed (Color32* ptr = data)
				{
					ThrowError(SetDIBitsToDevice(
						_hdc, destination.X, destination.Y,
						(uint)size.Width, (uint)size.Height, 0, 0, 0,
						(uint)size.Height, (IntPtr)ptr,
						ref lpbmi, DIB.RGB_COLORS) == 0);
				}
			}

			public void CopyTo(Rectangle srcRect, Point dstPos, IDrawingContext dst)
			{
				var dstC = (DrawingContext)dst;
				ThrowError(0 == BitBlt(dstC._hdc, dstPos.X, dstPos.Y,
					srcRect.Width, srcRect.Height, _hdc, srcRect.X, srcRect.Y, RasterOp.SRCCOPY));
			}
		}

		public void FlushIcon()
		{
			throw new NotImplementedException();
		}


	}
}