namespace Amity.X11
{
	using System;
	using System.Text;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;

	public static partial class Util
	{
		private static class Sizes<T>
			where T : struct
		{
			public static readonly int Size =
				typeof(T).IsEnum
				? Marshal.SizeOf(typeof(T).GetEnumUnderlyingType())
				: Marshal.SizeOf<T>();
		}

		public static int SizeOf<T>() where T : struct => Sizes<T>.Size;
	}

	public abstract class PropertiesBase
	{
		unsafe static PropertiesBase()
		{
			RegisterPropertyType<string>(
				"STRING",
				8,
				str => Encoding.UTF8.GetMaxByteCount(str.Length),
				data => {
					fixed (byte* ptr = data)
						return Encoding.UTF8.GetString(ptr, data.Length);
				},
				(value, data) => value.WriteOut(data)
			);

			RegisterPropertyType<Window>("WINDOW");
			RegisterPropertyType<Atom>("ATOM");
			RegisterPropertyType<uint>("CARDINAL");
		}

		public delegate int MaxSize<T>(T value);
		public delegate T FromBytes<T>(ReadOnlySpan<byte> input);
		public delegate int ToBytes<T>(T value, Span<byte> output);

		private static class Converter<T>
		{
			public static int Format;
			public static MaxSize<T> MaxSize;
			public static FromBytes<T> FromBytes;
			public static ToBytes<T> ToBytes;
			public static string TypeName;
		}

		protected static void RegisterPropertyType<T>(
			string typeName, int format, MaxSize<T> maxSize,
			FromBytes<T> fromBytes, ToBytes<T> toBytes
		)
		{
			Converter<T>.Format = format;
			Converter<T>.TypeName = typeName;
			Converter<T>.MaxSize = maxSize;
			Converter<T>.FromBytes = fromBytes;
			Converter<T>.ToBytes = toBytes;
		}

		protected static void RegisterPropertyType<T>(string typeName)
			where T : unmanaged
		{
			Converter<T>.Format = Util.SizeOf<T>();
			Converter<T>.TypeName = typeName;
			Converter<T>.MaxSize = _ => Util.SizeOf<T>();
			Converter<T>.FromBytes = MemoryMarshal.Read<T>;
			Converter<T>.ToBytes = (value, data) => 
			{
				MemoryMarshal.Write<T>(data, ref value);
				return Util.SizeOf<T>();
			};
		}

		private readonly Transport _c;
		private readonly Window _wId;

		protected PropertiesBase(Transport c, Window wId)
		{
			_c = c;
			_wId = wId;
		}

		private readonly Dictionary<string, Atom> _atoms
			= new Dictionary<string, Atom>();
		
		private Atom GetAtom(string str)
		{
			if (!_atoms.TryGetValue(str, out var atom))
			{
				_c.Request(new InternAtom { }, str, out InternAtom.Reply reply);
				_atoms.Add(str, atom = reply.Atom);
				if (atom == 0)
				{
					throw new Exception($"The atom {str} doesn't exist!");
				}
			}
			return atom;
		}

		private byte[] _buffer = new byte[65535];

		private GetProperty MakeGetRequest<T>(string name) =>
			new GetProperty
			{
				Delete = false,
				Window = _wId,
				Property = GetAtom(name),
				Type = GetAtom(Converter<T>.TypeName),
				Offset = 0,
				Length = (uint)_buffer.Length
			};
		
		private ChangeProperty MakeSetRequest<T>(string name) =>
			new ChangeProperty
			{
				Window = _wId,
				Property = GetAtom(name),
				Type = GetAtom(Converter<T>.TypeName),
				Format = (byte)Converter<T>.Format, // TODO: Change this?
			};

		public T GetProperty<T>([CallerMemberName] string name = null)
		{
			if (name == null) { throw new ArgumentNullException(nameof(name)); }
			// TODO: Iterate over with bytes-after
			_c.Request(MakeGetRequest<T>(name),
				out GetProperty.Reply reply, out Span<byte> replyData);
			if (reply.Format == 0)
			{
				return default(T);
			}
			if (reply.BytesAfter > 0)
			{
				throw new NotSupportedException($"Buffer is too small");
			}
			return Converter<T>.FromBytes(replyData);
		}

		public void SetProperty<T>(T value, [CallerMemberName] string name = null)
		{
			if (name == null) { throw new ArgumentNullException(nameof(name)); }
			var len = Converter<T>.ToBytes(value, _buffer);
			_c.Request(MakeSetRequest<T>(name), _buffer.AsMemory(0, len));
		}

		public Span<T> GetSpan<T>([CallerMemberName] string name = null)
			where T : unmanaged
		{
			if (name == null) { throw new ArgumentNullException(nameof(name)); }
			// TODO: Iterate over with bytes-after
			_c.Request(MakeGetRequest<T>(name),
				out GetProperty.Reply reply, out Span<byte> replyData);
			if (reply.Format == 0)
			{
				return default(Span<T>);
			}
			return MemoryMarshal.Cast<byte, T>(replyData);
		}

		public void SetSpan<T>(Span<T> value, [CallerMemberName] string name = null)
			where T : unmanaged
		{
			if (name == null) { throw new ArgumentNullException(nameof(name)); }
			var src = MemoryMarshal.AsBytes(value);
			src.CopyTo(_buffer);
			_c.Request(MakeSetRequest<T>(name), _buffer.AsMemory(0, src.Length));
		}
	}

	public class ICCCM : PropertiesBase
	{
		public ICCCM(Transport c, Window wId) : base(c, wId) { }

		unsafe static ICCCM()
		{
			RegisterPropertyType<WmHints>("WM_HINTS");
			RegisterPropertyType<IconSize>("WM_ICON_SIZE");
			RegisterPropertyType<WmState>("WM_STATE");
		}

		public string WM_CLASS
		{
			get => GetProperty<string>();
			set => SetProperty(value);
		}

		public string WM_NAME
		{
			get => GetProperty<string>();
			set => SetProperty(value);
		}

		public string WM_ICON_NAME
		{
			get => GetProperty<string>();
			set => SetProperty(value);
		}

		public string WM_CLIENT_MACHINE
		{
			get => GetProperty<string>();
			set => SetProperty(value);
		}

		public WmHints WM_HINTS
		{
			get => GetProperty<WmHints>();
			set => SetProperty(value);
		}

		public Window WM_TRANSIENT_FOR
		{
			get => GetProperty<Window>();
			set => SetProperty(value);
		}

		public Span<Atom> WM_PROTOCOLS
		{
			get => GetSpan<Atom>();
			set => SetSpan(value);
		}

		public Span<Window> WM_COLORMAP_WINDOWS
		{
			get => GetSpan<Window>();
			set => SetSpan(value);
		}

		public WmState WM_STATE
		{
			get => GetProperty<WmState>();
			set => SetProperty(value);
		}

		public IconSize WM_ICON_SIZE
		{
			get => GetProperty<IconSize>();
			set => SetProperty(value);
		}
	}

	public class NetWM : PropertiesBase
	{
		public NetWM(Transport c, Window wId) : base(c, wId)
		{
		}

		public string _NET_WM_NAME
		{
			get => GetProperty<string>();
			set => SetProperty(value);
		}

		public string _NET_WM_VISIBLE_NAME
		{
			get => GetProperty<string>();
			set => SetProperty(value);
		}

		public string _NET_WM_ICON_NAME
		{
			get => GetProperty<string>();
			set => SetProperty(value);
		}

		public string _NET_WM_VISIBLE_ICON_NAME
		{
			get => GetProperty<string>();
			set => SetProperty(value);
		}

		public uint _NET_WM_DESKTOP
		{
			get => GetProperty<uint>();
			set => SetProperty(value);
		}
	}
}