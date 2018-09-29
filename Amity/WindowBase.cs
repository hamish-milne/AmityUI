namespace Amity
{
	using System;
	using System.Linq;
	using System.Drawing;
	using System.Numerics;

	public interface IWindowAPI
	{
		bool IsSupported();
	}

	public class WindowBase : Window
	{
		public static IWindowAPI WindowTemplate { get; set; } = typeof(WindowBase)
			.Assembly.GetTypes().Where(typeof(IWindowAPI).IsAssignableFrom)
			.Select(Activator.CreateInstance)
			.Cast<IWindowAPI>()
			.FirstOrDefault(w => w.IsSupported());
		
		private readonly Window _api;

		public override event Action<Vector2> MouseMove
		{
			add => _api.MouseMove += value;
			remove => _api.MouseMove -= value;
		}
		public override event Action<int> MouseDown;
		public override event Action<int> KeyDown;
		public override event Action<int> KeyUp;
		public override event Action Paint
		{
			add => _api.Paint += value;
			remove => _api.Paint -= value;
		}
		public override event Action Draw
		{
			add => _api.Draw += value;
			remove => _api.Draw -= value;
		}
		public override Rectangle WindowArea => _api.WindowArea;
		public override Rectangle ClientArea => _api.ClientArea;

		public WindowBase()
		{
			_api = (Window)Activator.CreateInstance(WindowTemplate.GetType());
		}

		public override void Show() => _api.Show();

		public override Span<Color32> Buffer => _api.Buffer;

		public override IntPtr BufferPtr => _api.BufferPtr;

		public override IDrawingContext GetDrawingContext()
			=> _api.GetDrawingContext();
	}
}