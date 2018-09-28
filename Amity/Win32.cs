namespace Amity
{
	using System;
	using System.Drawing;
	using System.Numerics;
	using System.Runtime.InteropServices;
	using static System.Runtime.InteropServices.UnmanagedType;
	using System.Collections.Generic;

	public class Win32 : Window, IWindowAPI
	{
		private const string User = "User32";
		private const string Kernel = "Kernel32";
		private const string Gdi = "Gdi32";

		public bool IsSupported()
			=> Environment.OSVersion.Platform == PlatformID.Win32NT;
		
		public override unsafe Span<Color32> Buffer
			=> new Span<Color32>((void*)_bitmap, _bitmapWidth * _bitmapHeight);

#region pinvoke
		private delegate IntPtr WNDPROC(
			IntPtr hwnd,
			uint uMsg,
			UIntPtr wParam,
			IntPtr lParam
		);

		[DllImport(User)]
		private static extern IntPtr DefWindowProcW(
			IntPtr hwnd,
			uint uMsg,
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
			OVERLAPPEDWINDOW = OVERLAPPED | CAPTION | SYSMENU | THICKFRAME | MINIMIZEBOX | MAXIMIZEBOX,
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
			public uint message;
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

#endregion pinvoke

		public override Rectangle WindowArea
		{
			get {
				ThrowError(GetWindowRect(_hwnd, out var rect) == 0);
				return new Rectangle(
					rect.left,
					rect.top,
					rect.right - rect.left,
					rect.top - rect.bottom
				);
			}
		}

		public override Rectangle ClientArea
		{
			get {
				ThrowError(GetClientRect(_hwnd, out var rect) == 0);
				return new Rectangle(
					rect.left,
					rect.top,
					rect.right - rect.left,
					rect.top - rect.bottom
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
					var ret = FormatMessage(FORMAT_MESSAGE.ALLOCATE_BUFFER | FORMAT_MESSAGE.FROM_SYSTEM,
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

		private static readonly Dictionary<IntPtr, Win32> _instances = new Dictionary<IntPtr, Win32>();
		private static Exception _exceptionFromCallback;

		static Win32()
		{
			ClassWndProc = (hwnd, uMsg, wParam, lParam) =>
			{
				try
				{
					_instances.TryGetValue(hwnd, out var inst);
					if (inst?.HandleMessage(uMsg) == true)
					{
						return IntPtr.Zero;
					}
				} catch(Exception e)
				{
					_exceptionFromCallback = e;
				}
				return DefWindowProcW(hwnd, uMsg, wParam, lParam);;
			};
		}

		private bool _hasBlitted;
		private bool HandleMessage(uint uMsg)
		{
			switch (uMsg)
			{
				case 0x000F:
					Paint?.Invoke();
					if (!_hasBlitted){
						BitBlt(_dstDc, 0, 0, _bitmapWidth, _bitmapHeight, _srcDc, 0, 0, RasterOp.SRCCOPY);
						_hasBlitted = true;
					}
					return true;
			}
			return false;
		}

		private IntPtr _hwnd;
		private IntPtr _bitmap;
		private int _bitmapWidth;
		private int _bitmapHeight;
		private IntPtr _srcDc;
		private IntPtr _dstDc;

		public override event Action<Vector2> MouseMove;
		public override event Action<int> MouseDown;
		public override event Action<int> KeyDown;
		public override event Action<int> KeyUp;
		public override event Action Paint;

		const int InitialWidth = 400;
		const int InitialHeight = 300;

		public override void Show()
		{
			var width = InitialWidth;
			var height = InitialHeight;

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

			var hWnd = CreateWindowExW(
				WS_EX.OVERLAPPEDWINDOW | WS_EX.APPWINDOW,
				new IntPtr(atom),
				"My window!",
				WS.OVERLAPPEDWINDOW,
				0x80000000,
				0x80000000,
				width,
				height,
				IntPtr.Zero,
				IntPtr.Zero,
				hInstance,
				IntPtr.Zero
			);
			ThrowError(hWnd == IntPtr.Zero);
			_instances[hWnd] = this;

			var bitmapInfo = new BITMAPINFOHEADER
			{
				biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
				biWidth = width,
				biHeight = -height,
				biPlanes = 1,
				biBitCount = 32,
				biCompression = BI.RGB,
			};

			_dstDc = GetDC(hWnd);
			var dc = CreateCompatibleDC(_dstDc);
			_srcDc = dc;

			var bitmap = CreateDIBSection(dc, ref bitmapInfo, DIB.RGB_COLORS,
				out _bitmap, IntPtr.Zero, 0);
			ThrowError(bitmap == IntPtr.Zero || _bitmap == IntPtr.Zero);
			_bitmapWidth = width;
			_bitmapHeight = height;
			SelectObject(_srcDc, bitmap);

			ShowWindow(hWnd, 1);
			UpdateWindow(hWnd);

			while (GetMessage(out var msg, hWnd, 0, 0) != 0)
			{
				TranslateMessage(ref msg);
				DispatchMessage(ref msg);
				if (_exceptionFromCallback != null)
				{
					throw new Exception("Exception in callback", _exceptionFromCallback);
				}
			}
		}
	}
}