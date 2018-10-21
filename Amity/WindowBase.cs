namespace Amity
{
	using System;
	using System.Linq;
	using System.Drawing;
	using System.Collections.Generic;

	public class WindowBase : IWindow
	{
		private static readonly List<Func<bool, IWindow>> _factories
			= new List<Func<bool, IWindow>>();
		
		public static void Register(Func<bool, IWindow> factory)
		{
			_factories.Add(factory);
		}

		private readonly IWindow _api;

		public event Action<Point> MouseMove
		{
			add => _api.MouseMove += value;
			remove => _api.MouseMove -= value;
		}
		public event Action<int> MouseDown;
		public event Action<int> KeyDown;
		public event Action<int> KeyUp;
		public event Action Resize
		{
			add => _api.Resize += value;
			remove => _api.Resize -= value;
		}
		public event Action Draw
		{
			add => _api.Draw += value;
			remove => _api.Draw -= value;
		}
		public Rectangle WindowArea
		{
			get => _api.WindowArea;
			set => _api.WindowArea = value;
		}
		public Rectangle ClientArea
		{
			get => _api.ClientArea;
			set => _api.ClientArea = value;
		}
		public bool IsVisible
		{
			get => _api.IsVisible;
			set => _api.IsVisible = value;
		}

		public WindowBase()
		{
			if (_factories.Count == 1)
			{
				_api = _factories[0](true);
			} else {
				_api = _factories.Select(f => f(false)).First(f => f != null);
			}
		}

		public void Run() => _api.Run();

		public Point MousePosition
		{
			get => _api.MousePosition;
			set => _api.MousePosition = value;
		}

		public IDrawingContext CreateDrawingContext()
			=> _api.CreateDrawingContext();

		public IDrawingContext CreateBitmap(Size size)
			=> _api.CreateBitmap(size);
		
		public void Invalidate() => _api.Invalidate();
		public IFont LoadFont(string name) => throw new NotImplementedException();
	}
}