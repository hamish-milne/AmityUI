namespace Amity.X11
{
	using System;
	using System.Collections.Generic;
	using System.Drawing;
	using System.Runtime.InteropServices;
	using System.Linq;

	public class Protocol : Transport
	{
		private ConnectionSuccess _info;
		private List<Format> _formats = new List<Format>();
		private List<(Screen, List<(Depth, List<VisualType>)>)> _screens
			= new List<(Screen, List<(Depth, List<VisualType>)>)>();

		public Protocol(System.Net.EndPoint endPoint) : base(endPoint)
		{
		}
		
		public void MessageLoop()
		{
			MessageLoop(buffer =>
			{
				var span = buffer.AsSpan();
				bool handled = false;
				foreach (var e in _events)
				{
					if (e.Opcodes.Contains(buffer[0]))
					{
						e.Read(span);
						handled = true;
						break;
					}
				}
				if (!handled)
				{
					//throw new Exception($"Unknown response code {buffer[0]}");
				}
			});
		}

		private readonly EventBase[] _events =
		{
			new Event<Error>{Opcodes = {0}},
			new Event<ResizeRequestEvent>{Opcodes = {3}},
			new Event<KeyEvent>{Opcodes = {2, 3, 4, 5}},
		};

		private abstract class EventBase
		{
			public HashSet<byte> Opcodes { get; } = new HashSet<byte>();
			public abstract void Read(Span<byte> data);
		}

		private class Event<T> : EventBase where T : struct
		{
			public Action<T> OnEvent;

			public override void Read(Span<byte> data)
			{
				OnEvent?.Invoke(MemoryMarshal.Read<T>(data));
			}
		}

		public void ListenTo<T>(Action<T> action) where T : struct
		{
			_events.OfType<Event<T>>().Single().OnEvent += action;
		}

		public void Initialize()
		{
			Write(new ConnectionRequest
			{
				ByteOrder = 0x6C,
				MajorVersion = 11
			});
			Send(false);

			// Response
			var response = Read<ConnectionResponse>();
			switch (response.Code)
			{
				case 0:
				case 2:
					throw new Exception(ReadString(response.ReasonLengthBytes));
				case 1:
					var response2 = Read<ConnectionSuccess>();
					var vendor = ReadString(response2.VendorLength);
					_formats.Clear();
					for (var i = 0; i < response2.FormatCount; i++)
					{
						_formats.Add(Read<Format>());
					}
					_screens.Clear();
					for (var i = 0; i < response2.ScreenCount; i++)
					{
						var screen = Read<Screen>();
						var depths = new List<(Depth, List<VisualType>)>();
						for (var j = 0; j < screen.DepthCount; j++)
						{
							var depth = Read<Depth>();
							var visualTypes = new List<VisualType>();
							for (var k = 0; k < depth.VisualsCount; k++)
							{
								visualTypes.Add(Read<VisualType>());
							}
							depths.Add((depth, visualTypes));
						}
						_screens.Add((screen, depths));
					}
					_info = response2;
					break;
			}
		}

		public uint CreateWindow(Rectangle rect)
		{
			var screen = _screens[0].Item1;
			var data = new CreateWindowData
			{
				Opcode = 1,
				X = (ushort)rect.X,
				Y = (ushort)rect.Y,
				Width = (ushort)rect.Width,
				Height = (ushort)rect.Height,
				Visual = screen.RootVisual,
				Parent = screen.Root,
				Depth = screen.RootDepth,
				WindowId = _info.ResourceIdBase + 1,
				Class = WindowClass.InputOutput,
			};
			Write(data);
			Write(new WindowValues
			{
				EventMask = Event.KeyPress | Event.KeyRelease,
			});
			Send(true);
			Write(new MapWindowData
			{
				Opcode = 8,
				Window = data.WindowId
			});
			Send(true);
			return data.WindowId;
		}
	}
}