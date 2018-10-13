namespace Amity
{
	using System;
	using System.Drawing;
	using System.Runtime.InteropServices;
	using static System.Runtime.InteropServices.UnmanagedType;
	using System.Collections.Generic;
	using System.Diagnostics;

	public class Win32 : IWindowAPI
	{
		public bool IsSupported()
			=> Environment.OSVersion.Platform == PlatformID.Win32NT;

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
						throw new Exception($"Error code {code} (format message failed)");
					} else {
						throw new Exception(lpBuffer);
					}
				}
				throw new Exception("Something went wrong, but no error code was set");
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
					// TODO: Resize event
					break;
				case WM.PAINT:
					Draw?.Invoke();
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
		public event Action Paint;
		public event Action Draw;

		public Point MousePosition { get; private set; }

		public void Show(Rectangle rect)
		{
			SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT.PER_MONITOR_AWARE_V2);
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
				rect.Width,
				rect.Height,
				IntPtr.Zero,
				IntPtr.Zero,
				hInstance,
				IntPtr.Zero
			);
			ThrowError(_hwnd == IntPtr.Zero);
			_instances[_hwnd] = this;

			ShowWindow(_hwnd, 1);
			_dstDc = GetDC(_hwnd);
			
			// TODO: OnShow event?

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
					throw new Exception("Exception in callback", _exceptionFromCallback);
				}
			}
		}

		public IDrawingContext GetDrawingContext()
		{
			return new DrawingContext(this);
		}

		private class DrawingContext : IDrawingContext
		{
			private IntPtr _hwnd;
			private IntPtr _hdc;
			private PAINTSTRUCT _paint;

			public DrawingContext(Win32 parent)
			{
				_hwnd = parent._hwnd;
				_hdc = parent._dstDc;
				BeginPaint(_hwnd, out _paint);
				SelectObject(_hdc, GetStockObject(StockObjects.DC_PEN));
				SelectObject(_hdc, GetStockObject(StockObjects.DC_BRUSH));
			}

			private static ColorRef ToColorRef(Color color)
			{
				return new ColorRef
				{
					R = color.R,
					G = color.G,
					B = color.B
				};
			}

			public Color? Brush
			{
				get => GetDCBrushColor(_hdc);
				set
				{
					SelectObject(_hdc, GetStockObject(value == null
						? StockObjects.NULL_BRUSH : StockObjects.DC_BRUSH));
					if (value.HasValue) {
						SetDCBrushColor(_hdc, ToColorRef(value.Value));
					}
				}
			}

			public Color? Pen
			{
				get => GetDCPenColor(_hdc);
				set
				{
					SelectObject(_hdc, GetStockObject(value == null
						? StockObjects.NULL_PEN : StockObjects.DC_PEN));
					if (value.HasValue) {
						SetDCPenColor(_hdc, ToColorRef(value.Value));
					}
				}
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
				EndPaint(_hwnd, ref _paint);
			}

			public void EndPolygon(bool forceClose)
			{
				throw new NotImplementedException();
			}

			public void Line(Point a, Point b)
			{
				ThrowError(MoveToEx(_hdc, a.X, a.Y, out var _) == 0);
				ThrowError(LineTo(_hdc, b.X, b.Y) == 0);
			}

			public void PushPoint(Point next)
			{
				throw new NotImplementedException();
			}

			public void Rectangle(Rectangle rect)
			{
				ThrowError(Win32.Rectangle(_hdc,
					rect.Left, rect.Top, rect.Right, rect.Bottom) == 0);
			}

			public void Text(Point position, string font, string text)
			{
				throw new NotImplementedException();
			}

			public unsafe void Image(Span<Color32> data, Size size, Point destination)
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
					ThrowError(SetDIBitsToDevice(_hdc, destination.X, destination.Y,
						(uint)size.Width, (uint)size.Height, 0, 0, 0,
						(uint)size.Height, (IntPtr)ptr, ref lpbmi, DIB.RGB_COLORS) == 0);
				}
			}
		}

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		private struct ColorRef
		{
			public byte R, G, B;

			public static implicit operator Color(ColorRef c)
			{
				return Color.FromArgb(c.R, c.G, c.B);
			}
		}

#region pinvoke

		private const string User = "User32";
		private const string Kernel = "Kernel32";
		private const string Gdi = "Gdi32";

		enum WM : uint
		{
			NULL = 0x0000,
			CREATE = 0x0001,
			DESTROY = 0x0002,
			MOVE = 0x0003,
			SIZE = 0x0005,
			ACTIVATE = 0x0006,
			SETFOCUS = 0x0007,
			KILLFOCUS = 0x0008,
			ENABLE = 0x000A,
			SETREDRAW = 0x000B,
			SETTEXT = 0x000C,
			GETTEXT = 0x000D,
			GETTEXTLENGTH = 0x000E,
			PAINT = 0x000F,
			CLOSE = 0x0010,
			QUERYENDSESSION = 0x0011,
			QUERYOPEN = 0x0013,
			ENDSESSION = 0x0016,
			QUIT = 0x0012,
			ERASEBKGND = 0x0014,
			SYSCOLORCHANGE = 0x0015,
			SHOWWINDOW = 0x0018,
			WININICHANGE = 0x001A,
			SETTINGCHANGE = WININICHANGE,
			DEVMODECHANGE = 0x001B,
			ACTIVATEAPP = 0x001C,
			FONTCHANGE = 0x001D,
			TIMECHANGE = 0x001E,
			CANCELMODE = 0x001F,
			SETCURSOR = 0x0020,
			MOUSEACTIVATE = 0x0021,
			CHILDACTIVATE = 0x0022,
			QUEUESYNC = 0x0023,
			GETMINMAXINFO = 0x0024,
			PAINTICON = 0x0026,
			ICONERASEBKGND = 0x0027,
			NEXTDLGCTL = 0x0028,
			SPOOLERSTATUS = 0x002A,
			DRAWITEM = 0x002B,
			MEASUREITEM = 0x002C,
			DELETEITEM = 0x002D,
			VKEYTOITEM = 0x002E,
			CHARTOITEM = 0x002F,
			SETFONT = 0x0030,
			GETFONT = 0x0031,
			SETHOTKEY = 0x0032,
			GETHOTKEY = 0x0033,
			QUERYDRAGICON = 0x0037,
			COMPAREITEM = 0x0039,
			GETOBJECT = 0x003D,
			COMPACTING = 0x0041,
			[Obsolete]
			COMMNOTIFY = 0x0044,
			WINDOWPOSCHANGING = 0x0046,
			WINDOWPOSCHANGED = 0x0047,
			[Obsolete]
			POWER = 0x0048,
			COPYDATA = 0x004A,
			CANCELJOURNAL = 0x004B,
			NOTIFY = 0x004E,
			INPUTLANGCHANGEREQUEST = 0x0050,
			INPUTLANGCHANGE = 0x0051,
			TCARD = 0x0052,
			HELP = 0x0053,
			USERCHANGED = 0x0054,
			NOTIFYFORMAT = 0x0055,
			CONTEXTMENU = 0x007B,
			STYLECHANGING = 0x007C,
			STYLECHANGED = 0x007D,
			DISPLAYCHANGE = 0x007E,
			GETICON = 0x007F,
			SETICON = 0x0080,
			NCCREATE = 0x0081,
			NCDESTROY = 0x0082,
			NCCALCSIZE = 0x0083,
			NCHITTEST = 0x0084,
			NCPAINT = 0x0085,
			NCACTIVATE = 0x0086,
			GETDLGCODE = 0x0087,
			SYNCPAINT = 0x0088,
			NCMOUSEMOVE = 0x00A0,
			NCLBUTTONDOWN = 0x00A1,
			NCLBUTTONUP = 0x00A2,
			NCLBUTTONDBLCLK = 0x00A3,
			NCRBUTTONDOWN = 0x00A4,
			NCRBUTTONUP = 0x00A5,
			NCRBUTTONDBLCLK = 0x00A6,
			NCMBUTTONDOWN = 0x00A7,
			NCMBUTTONUP = 0x00A8,
			NCMBUTTONDBLCLK = 0x00A9,
			NCXBUTTONDOWN = 0x00AB,
			NCXBUTTONUP = 0x00AC,
			NCXBUTTONDBLCLK = 0x00AD,
			INPUT_DEVICE_CHANGE = 0x00FE,
			INPUT = 0x00FF,
			KEYFIRST = 0x0100,
			KEYDOWN = 0x0100,
			KEYUP = 0x0101,
			CHAR = 0x0102,
			DEADCHAR = 0x0103,
			SYSKEYDOWN = 0x0104,
			SYSKEYUP = 0x0105,
			SYSCHAR = 0x0106,
			SYSDEADCHAR = 0x0107,
			UNICHAR = 0x0109,
			KEYLAST = 0x0108,
			IME_STARTCOMPOSITION = 0x010D,
			IME_ENDCOMPOSITION = 0x010E,
			IME_COMPOSITION = 0x010F,
			IME_KEYLAST = 0x010F,
			INITDIALOG = 0x0110,
			COMMAND = 0x0111,
			SYSCOMMAND = 0x0112,
			TIMER = 0x0113,
			HSCROLL = 0x0114,
			VSCROLL = 0x0115,
			INITMENU = 0x0116,
			INITMENUPOPUP = 0x0117,
			MENUSELECT = 0x011F,
			MENUCHAR = 0x0120,
			ENTERIDLE = 0x0121,
			MENURBUTTONUP = 0x0122,
			MENUDRAG = 0x0123,
			MENUGETOBJECT = 0x0124,
			UNINITMENUPOPUP = 0x0125,
			MENUCOMMAND = 0x0126,
			CHANGEUISTATE = 0x0127,
			UPDATEUISTATE = 0x0128,
			QUERYUISTATE = 0x0129,
			CTLCOLORMSGBOX = 0x0132,
			CTLCOLOREDIT = 0x0133,
			CTLCOLORLISTBOX = 0x0134,
			CTLCOLORBTN = 0x0135,
			CTLCOLORDLG = 0x0136,
			CTLCOLORSCROLLBAR = 0x0137,
			CTLCOLORSTATIC = 0x0138,
			MOUSEFIRST = 0x0200,
			MOUSEMOVE = 0x0200,
			LBUTTONDOWN = 0x0201,
			LBUTTONUP = 0x0202,
			LBUTTONDBLCLK = 0x0203,
			RBUTTONDOWN = 0x0204,
			RBUTTONUP = 0x0205,
			RBUTTONDBLCLK = 0x0206,
			MBUTTONDOWN = 0x0207,
			MBUTTONUP = 0x0208,
			MBUTTONDBLCLK = 0x0209,
			MOUSEWHEEL = 0x020A,
			XBUTTONDOWN = 0x020B,
			XBUTTONUP = 0x020C,
			XBUTTONDBLCLK = 0x020D,
			MOUSEHWHEEL = 0x020E,
			MOUSELAST = 0x020E,
			PARENTNOTIFY = 0x0210,
			ENTERMENULOOP = 0x0211,
			EXITMENULOOP = 0x0212,
			NEXTMENU = 0x0213,
			SIZING = 0x0214,
			CAPTURECHANGED = 0x0215,
			MOVING = 0x0216,
			POWERBROADCAST = 0x0218,
			DEVICECHANGE = 0x0219,
			MDICREATE = 0x0220,
			MDIDESTROY = 0x0221,
			MDIACTIVATE = 0x0222,
			MDIRESTORE = 0x0223,
			MDINEXT = 0x0224,
			MDIMAXIMIZE = 0x0225,
			MDITILE = 0x0226,
			MDICASCADE = 0x0227,
			MDIICONARRANGE = 0x0228,
			MDIGETACTIVE = 0x0229,
			MDISETMENU = 0x0230,
			ENTERSIZEMOVE = 0x0231,
			EXITSIZEMOVE = 0x0232,
			DROPFILES = 0x0233,
			MDIREFRESHMENU = 0x0234,
			IME_SETCONTEXT = 0x0281,
			IME_NOTIFY = 0x0282,
			IME_CONTROL = 0x0283,
			IME_COMPOSITIONFULL = 0x0284,
			IME_SELECT = 0x0285,
			IME_CHAR = 0x0286,
			IME_REQUEST = 0x0288,
			IME_KEYDOWN = 0x0290,
			IME_KEYUP = 0x0291,
			MOUSEHOVER = 0x02A1,
			MOUSELEAVE = 0x02A3,
			NCMOUSEHOVER = 0x02A0,
			NCMOUSELEAVE = 0x02A2,
			WTSSESSION_CHANGE = 0x02B1,
			TABLET_FIRST = 0x02c0,
			TABLET_LAST = 0x02df,
			CUT = 0x0300,
			COPY = 0x0301,
			PASTE = 0x0302,
			CLEAR = 0x0303,
			UNDO = 0x0304,
			RENDERFORMAT = 0x0305,
			RENDERALLFORMATS = 0x0306,
			DESTROYCLIPBOARD = 0x0307,
			DRAWCLIPBOARD = 0x0308,
			PAINTCLIPBOARD = 0x0309,
			VSCROLLCLIPBOARD = 0x030A,
			SIZECLIPBOARD = 0x030B,
			ASKCBFORMATNAME = 0x030C,
			CHANGECBCHAIN = 0x030D,
			HSCROLLCLIPBOARD = 0x030E,
			QUERYNEWPALETTE = 0x030F,
			PALETTEISCHANGING = 0x0310,
			PALETTECHANGED = 0x0311,
			HOTKEY = 0x0312,
			PRINT = 0x0317,
			PRINTCLIENT = 0x0318,
			APPCOMMAND = 0x0319,
			THEMECHANGED = 0x031A,
			CLIPBOARDUPDATE = 0x031D,
			DWMCOMPOSITIONCHANGED = 0x031E,
			DWMNCRENDERINGCHANGED = 0x031F,
			DWMCOLORIZATIONCOLORCHANGED = 0x0320,
			DWMWINDOWMAXIMIZEDCHANGE = 0x0321,
			GETTITLEBARINFOEX = 0x033F,
			HANDHELDFIRST = 0x0358,
			HANDHELDLAST = 0x035F,
			AFXFIRST = 0x0360,
			AFXLAST = 0x037F,
			PENWINFIRST = 0x0380,
			PENWINLAST = 0x038F,
			APP = 0x8000,
			USER = 0x0400,

			CPL_LAUNCH = USER+0x1000,
			CPL_LAUNCHED = USER+0x1001,
			SYSTIMER = 0x118,

			HSHELL_ACCESSIBILITYSTATE = 11,
			HSHELL_ACTIVATESHELLWINDOW = 3,
			HSHELL_APPCOMMAND = 12,
			HSHELL_GETMINRECT = 5,
			HSHELL_LANGUAGE = 8,
			HSHELL_REDRAW = 6,
			HSHELL_TASKMAN = 7,
			HSHELL_WINDOWCREATED = 1,
			HSHELL_WINDOWDESTROYED = 2,
			HSHELL_WINDOWACTIVATED = 4,
			HSHELL_WINDOWREPLACED = 13
		}

		private delegate IntPtr WNDPROC(
			IntPtr hwnd,
			WM uMsg,
			UIntPtr wParam,
			IntPtr lParam
		);

		[DllImport(User)]
		private static extern IntPtr DefWindowProcW(
			IntPtr hwnd,
			WM uMsg,
			UIntPtr wParam,
			IntPtr lParam
		);

		private enum CS : uint
		{
			BYTEALIGNCLIENT = 0X1000,
			BYTEALIGHWINDOW = 0X2000,
			CLASSDC = 0X0040,
			DBLCLKS = 0X0008,
			DROPSHADOW = 0X00020000,
			GLOBALCLASS = 0X4000,
			HREDRAW = 0X0002,
			NOCLOSE = 0X0200,
			OWNDC = 0X0020,
			PARENTDC = 0X0080,
			SAVEBITS = 0X0800,
			VREDRAW = 0X0001,
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct WNDCLASSEXW
		{
			public int cbSize;
			public CS style;
			[MarshalAs(FunctionPtr)] public WNDPROC lpfnWndProc;
			public int cbClsExtra;
			public int cbWndExtra;
			public IntPtr hInstance;
			public IntPtr hIcon;
			public IntPtr hCursor;
			public IntPtr hbrBackground;
			[MarshalAs(LPWStr)] public string lpszMenuName;
			[MarshalAs(LPWStr)] public string lpszClassName;
			public IntPtr hIconSm;
		}

		[DllImport(User)]
		private static extern ushort RegisterClassExW(
			ref WNDCLASSEXW wndClass
		);

		[DllImport(Kernel)]
		private static extern IntPtr GetModuleHandle(
			[MarshalAs(LPStr)] string lpModuleName
		);

		private enum WS_EX
		{
			ACCEPTFILES         = 0x00000010,
			APPWINDOW           = 0x00040000,
			CLIENTEDGE          = 0x00000200,
			COMPOSITED          = 0x02000000,
			CONTEXTHELP         = 0x00000400,
			CONTROLPARENT       = 0x00010000,
			DLGMODALFRAME       = 0x00000001,
			LAYERED             = 0x00080000,
			LAYOUTRTL           = 0x00400000,
			LEFT                = 0x00000000,
			LTRREADING          = 0X00000000,
			MDICHILD            = 0X00000040,
			NOACTIVATE          = 0X08000000,
			NOINHERITLAYOUT     = 0X00100000,
			NOPARENTNOTIFY      = 0X00000004,
			NOREDIRECTIONBITMAP = 0X00200000,
			RIGHT               = 0X00001000,
			RIGHTSCROLLBAR      = 0X00000000,
			RTLREADING          = 0X00002000,
			STATICEDGE          = 0X00020000,
			TOOLWINDOW          = 0X00000080,
			TOPMOST             = 0X00000008,
			TRANSPARENT         = 0X00000020,
			WINDOWEDGE          = 0X00000100,
			OVERLAPPEDWINDOW = WINDOWEDGE | CLIENTEDGE,
			PALETTEWINDOW = WINDOWEDGE | TOOLWINDOW | TOPMOST,
		}

		private enum WS : uint
		{
			BORDER = 0X00800000,
			CAPTION = 0X00C00000,
			CHILD = 0X40000000,
			CHILDWINDOW = CHILD,
			CLIPCHILDREN = 0X02000000,
			CLIPSIBLINGS = 0X04000000,
			DISABLED = 0X08000000,
			DLGFRAME = 0X00400000,
			GROUP = 0X00020000,
			HSCROLL = 0X00100000,
			ICONIC = 0X20000000,
			MAXIMIZE = 0X01000000,
			MAXIMIZEBOX = 0X00010000,
			MINIMIZE = ICONIC,
			MINIMIZEBOX = 0X00020000,
			OVERLAPPED = 0X00000000,
			OVERLAPPEDWINDOW = OVERLAPPED | CAPTION | SYSMENU
				| THICKFRAME | MINIMIZEBOX | MAXIMIZEBOX,
			POPUP = 0X80000000,
			POPUPWINDOW = POPUP | BORDER | SYSMENU,
			SIZEBOX = 0X00040000,
			SYSMENU = 0X00080000,
			TABSTOP = 0X00010000,
			THICKFRAME = SIZEBOX,
			TILED = OVERLAPPED,
			TILEDWINDOW = OVERLAPPEDWINDOW,
			VISIBLE = 0X10000000,
			VSCROLL = 0X00200000,
		}

		[DllImport(User)]
		private static extern IntPtr CreateWindowExW(
			WS_EX dwExStyle,
			IntPtr lpClassName,
			[MarshalAs(LPWStr)] string lpWindowName,
			WS dwStyle,
			uint X,
			uint Y,
			int nWidth,
			int nHeight,
			IntPtr hWndParent,
			IntPtr hMenu,
			IntPtr hInstance,
			IntPtr lpParam
		);

		[Flags]
		enum FORMAT_MESSAGE
		{
			ALLOCATE_BUFFER = 0x00000100,
			ARGUMENT_ARRAY = 0x00002000,
			FROM_HMODULE = 0x00000800,
			FROM_STRING = 0x00000400,
			FROM_SYSTEM = 0x00001000,
			IGNORE_INSERTS = 0x00000200,
			MAX_WIDTH_MASK = 0x000000FF
		}

		[DllImport(Kernel)]
		private static extern int FormatMessage(
			FORMAT_MESSAGE dwFlags,
			IntPtr lpSource,
			int dwMessageId,
			int dwLanguageId,
			[MarshalAs(LPStr)] out string lpBuffer,
			int nSize,
			IntPtr Arguments
		);

		[DllImport(Kernel)]
		private static extern int GetLastError();

		[DllImport(User)]
		private static extern IntPtr LoadIconW(
			IntPtr hInstance,
			IntPtr lpIconName
		);

		[DllImport(User)]
		private static extern IntPtr LoadCursorW(
			IntPtr hInstance,
			IntPtr lpCursorName
		);

		[DllImport(User)]
		private static extern int ShowWindow(
			IntPtr hWnd,
			int nCmdShow
		);

		[DllImport(User)]
		private static extern int UpdateWindow(
			IntPtr hWnd
		);

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct POINT
		{
			public int x;
			public int y;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct MSG
		{
			public IntPtr hwnd;
			public WM message;
			public UIntPtr wParam;
			public IntPtr lParam;
			public uint time;
			public POINT pt;
			public uint lPrivate;
		}

		[DllImport(User)]
		private static extern int GetMessage(
			out MSG lpMsg,
			IntPtr hWnd,
			uint wMsgFilterMin,
			uint wMsgFilterMax
		);

		[DllImport(User)]
		private static extern int TranslateMessage(
			ref MSG lpMsg
		);

		[DllImport(User)]
		private static extern IntPtr DispatchMessage(
			ref MSG lpMsg
		);

		enum GCL
		{
			CBCLSEXTRA = -20,
			CBWNDEXTRA = -18,
			HBRBACKGROUND = -10,
			HCURSOR = -12,
			HICON = -14,
			HICONSM = -34,
			HMODULE = -16,
			MENUNAME = -8,
			STYLE = -26,
			WNDPROC = -24,
		}

		[DllImport(User)]
		private static extern UIntPtr SetClassLongPtrW(
			IntPtr hWnd,
			GCL nIndex,
			IntPtr dwNewLong
		);

		[DllImport(User)]
		private static extern IntPtr GetDC(
			IntPtr hWnd
		);

		[DllImport(Gdi)]
		private static extern IntPtr CreateCompatibleDC(
			IntPtr hdc
		);

		enum DIB : uint
		{
			RGB_COLORS = 0x00,
			PAL_COLORS = 0x01,
			PAL_INDICES = 0x02
		};

		enum BI : uint
		{
			RGB = 0X0000,
			RLE8 = 0X0001,
			RLE4 = 0X0002,
			BITFIELDS = 0X0003,
			JPEG = 0X0004,
			PNG = 0X0005,
			CMYK = 0X000B,
			CMYKRLE8 = 0X000C,
			CMYKRLE4 = 0X000D,
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		struct BITMAPINFOHEADER
		{
			public uint biSize;
			public int biWidth;
			public int biHeight;
			public ushort biPlanes;
			public ushort biBitCount;
			public BI biCompression;
			public uint biSizeImage;
			public int biXPelsPerMeter;
			public int biYPelsPerMeter;
			public uint biClrUsed;
			public uint biClrImportant;
		}

		[DllImport(Gdi)]
		private static extern IntPtr CreateDIBSection(
			IntPtr hdc,
			ref BITMAPINFOHEADER pbmi,
			DIB usage,
			out IntPtr ppvBits,
			IntPtr hSection,
			uint offset
		);
		
		[DllImport(Gdi)]
		private static extern int GdiFlush();

		[DllImport(Gdi)]
		private static extern IntPtr SelectObject(
			IntPtr hdc,
			IntPtr h
		);

		public enum RasterOp : uint {
			SRCCOPY     = 0x00CC0020,
			SRCPAINT    = 0x00EE0086,
			SRCAND      = 0x008800C6,
			SRCINVERT   = 0x00660046,
			SRCERASE    = 0x00440328,
			NOTSRCCOPY  = 0x00330008,
			NOTSRCERASE = 0x001100A6,
			MERGECOPY   = 0x00C000CA,
			MERGEPAINT  = 0x00BB0226,
			PATCOPY     = 0x00F00021,
			PATPAINT    = 0x00FB0A09,
			PATINVERT   = 0x005A0049,
			DSTINVERT   = 0x00550009,
			BLACKNESS   = 0x00000042,
			WHITENESS   = 0x00FF0062,
			CAPTUREBLT  = 0x40000000,
		}

		[DllImport(Gdi)]
		private static extern int BitBlt(
			IntPtr hdc,
			int x,
			int y,
			int cx,
			int cy,
			IntPtr hdcSrc,
			int x1,
			int y1,
			RasterOp rop
		);

		enum DPI_AWARENESS_CONTEXT : int
		{
			UNAWARE = -1,
			SYSTEM_AWARE = -2,
			PER_MONITOR_AWARE = -3,
			PER_MONITOR_AWARE_V2 = -4,
		}

		[DllImport(User)]
		private static extern int SetProcessDpiAwarenessContext(
			DPI_AWARENESS_CONTEXT value
		);

		private struct RECT
		{
			public int left;
			public int top;
			public int right;
			public int bottom;
		}

		[DllImport(User)]
		private static extern int GetClientRect(
			IntPtr hWnd,
			out RECT lpRect
		);

		[DllImport(User)]
		private static extern int GetWindowRect(
			IntPtr hWnd,
			out RECT lpRect
		);

		[DllImport(User)]
		private static extern void PostQuitMessage(
			int nExitCode
		);

		[DllImport(Gdi)]
		private static extern int DeleteObject(
			IntPtr ho
		);

		[DllImport(Gdi)]
		private static extern ColorRef SetDCPenColor(
			IntPtr hdc,
			ColorRef color
		);

		[DllImport(Gdi)]
		private static extern ColorRef GetDCPenColor(
			IntPtr hdc
		);

		[DllImport(Gdi)]
		private static extern ColorRef SetDCBrushColor(
			IntPtr hdc,
			ColorRef color
		);

		[DllImport(Gdi)]
		private static extern ColorRef GetDCBrushColor(
			IntPtr hdc
		);

		[DllImport(Gdi)]
		private static extern int MoveToEx(
			IntPtr hdc,
			int x,
			int y,
			out POINT lppt
		);

		[DllImport(Gdi)]
		private static extern int LineTo(
			IntPtr hdc,
			int x,
			int y
		);

		private enum StockObjects
		{
			WHITE_BRUSH     = 0,
			LTGRAY_BRUSH    = 1,
			GRAY_BRUSH      = 2,
			DKGRAY_BRUSH    = 3,
			BLACK_BRUSH     = 4,
			NULL_BRUSH      = 5,
			HOLLOW_BRUSH    = NULL_BRUSH,
			WHITE_PEN       = 6,
			BLACK_PEN       = 7,
			NULL_PEN    = 8,
			OEM_FIXED_FONT  = 10,
			ANSI_FIXED_FONT = 11,
			ANSI_VAR_FONT   = 12,
			SYSTEM_FONT     = 13,
			DEVICE_DEFAULT_FONT = 14,
			DEFAULT_PALETTE     = 15,
			SYSTEM_FIXED_FONT   = 16,
			DEFAULT_GUI_FONT    = 17,
			DC_BRUSH    = 18,
			DC_PEN      = 19,
		}

		[DllImport(Gdi)]
    	private static extern IntPtr GetStockObject(
			StockObjects fnObject
		);

		[StructLayout(LayoutKind.Sequential)]
		struct PAINTSTRUCT
		{
			public IntPtr hdc;
			public bool fErase;
			public RECT rcPaint;
			public bool fRestore;
			public bool fIncUpdate;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst=32)]
			public byte [] rgbReserved;
		}

		[DllImport(User)]
		private static extern IntPtr BeginPaint(
			IntPtr hwnd,
			out PAINTSTRUCT lpPaint
		);

		[DllImport(User)]
		private static extern int EndPaint(
			IntPtr hwnd,
			ref PAINTSTRUCT lpPaint
		);

		[DllImport(Gdi)]
		private static extern int Rectangle(
			IntPtr hdc,
			int left,
			int top,
			int right,
			int bottom
		);

		[DllImport(User)]
		private static extern int InvalidateRect(
			IntPtr hwnd,
			IntPtr lpRect,
			int bErase
		);

		[DllImport(Gdi)]
		private static extern int SetDIBitsToDevice(
			IntPtr hdc,
			int xDest,
			int yDest,
			uint w,
			uint h,
			int xSrc,
			int ySrc,
			uint StartScan,
			uint cLines,
			IntPtr lpvBits,
			ref BITMAPINFOHEADER lpbmi,
			DIB ColorUse
		);

#endregion pinvoke

	}
}