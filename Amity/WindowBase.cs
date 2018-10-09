namespace Amity
{
	using System;
	using System.Linq;
	using System.Drawing;
	public interface IWindowAPI : IWindow
	{
		bool IsSupported();
	}

	public class WindowBase : IWindow
	{
		public static IWindowAPI WindowTemplate { get; set; } = typeof(WindowBase)
			.Assembly.GetTypes().Where(typeof(IWindowAPI).IsAssignableFrom)
			.Select(Activator.CreateInstance)
			.Cast<IWindowAPI>()
			.FirstOrDefault(w => w.IsSupported());

		private readonly IWindow _api;

		public event Action<Point> MouseMove
		{
			add => _api.MouseMove += value;
			remove => _api.MouseMove -= value;
		}
		public event Action<int> MouseDown;
		public event Action<int> KeyDown;
		public event Action<int> KeyUp;
		public event Action Paint
		{
			add => _api.Paint += value;
			remove => _api.Paint -= value;
		}
		public event Action Draw
		{
			add => _api.Draw += value;
			remove => _api.Draw -= value;
		}
		public Rectangle WindowArea => _api.WindowArea;
		public Rectangle ClientArea => _api.ClientArea;

		public WindowBase()
		{
			_api = (IWindow)Activator.CreateInstance(WindowTemplate.GetType());
		}

		public void Show(Rectangle rect) => _api.Show(rect);

		public Span<Color32> Buffer => _api.Buffer;

		public IntPtr BufferPtr => _api.BufferPtr;

		public Point MousePosition => _api.MousePosition;

		public IDrawingContext GetDrawingContext()
			=> _api.GetDrawingContext();
		
		public void Invalidate() => _api.Invalidate();
	}
}