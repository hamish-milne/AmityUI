namespace Amity.X11
{
	using System;
	using System.Text;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;

	public abstract class PropertiesBase
	{
		public delegate int MaxSize<T>(T value);
		public delegate T FromBytes<T>(ReadOnlySpan<byte> input);
		public delegate int ToBytes<T>(T value, Span<byte> output);

		private static class Converter<T>
		{
			public static MaxSize<T> MaxSize;
			public static FromBytes<T> FromBytes;
			public static ToBytes<T> ToBytes;
			public static string TypeName;
		}

		protected static void RegisterPropertyType<T>(
			string typeName, MaxSize<T> maxSize,
			FromBytes<T> fromBytes, ToBytes<T> toBytes
		)
		{
			Converter<T>.TypeName = typeName;
			Converter<T>.MaxSize = maxSize;
			Converter<T>.FromBytes = fromBytes;
			Converter<T>.ToBytes = toBytes;
		}

		protected static void RegisterPropertyType<T>(string typeName)
			where T : unmanaged
		{
			Converter<T>.TypeName = typeName;
			Converter<T>.MaxSize = Marshal.SizeOf<T>;
			Converter<T>.FromBytes = MemoryMarshal.Read<T>;
			Converter<T>.ToBytes = (value, data) => 
			{
				MemoryMarshal.Write<T>(data, ref value);
				return Marshal.SizeOf<T>();
			};
		}

		private readonly Transport _c;
		private readonly Window _wId;

		protected PropertiesBase(Transport c, Window wId)
		{
			_c = c;
			_wId = wId;
		}

		private readonly Dictionary<string, uint> _atoms
			= new Dictionary<string, uint>();
		
		private uint GetAtom(string str)
		{
			if (!_atoms.TryGetValue(str, out var atom))
			{
				_c.Request(new InternAtom
				{
					OnlyIfExists = true, // TODO: Allow this to be false?
				}, str, out InternAtom.Reply reply);
				_atoms.Add(str, atom = reply.Atom);
				if (atom == 0)
				{
					throw new Exception($"The atom {str} doesn't exist!");
				}
			}
			return atom;
		}

		private byte[] _buffer = new byte[65535];

		public T GetProperty<T>([CallerMemberName] string name = null)
		{
			// TODO: Iterate over with bytes-after
			_c.Request(new GetProperty
			{
				Delete = false,
				Window = _wId,
				Property = GetAtom(name),
				Type = GetAtom(Converter<T>.TypeName),
				Offset = 0,
				Length = (uint)_buffer.Length
			}, out GetProperty.Reply reply, out Span<byte> replyData);
			if (reply.Format == 0)
			{
				throw new Exception($"Property {name} doesn't exist");
			}
			if (reply.BytesAfter > 0)
			{
				throw new NotSupportedException($"Buffer is too small");
			}
			return Converter<T>.FromBytes(replyData);
		}

		public void SetProperty<T>(T value, [CallerMemberName] string name = null)
		{
			var len = Converter<T>.ToBytes(value, _buffer.AsSpan());
			_c.Request(new ChangeProperty
			{
				Window = _wId,
				Property = GetAtom(name),
				Type = GetAtom(Converter<T>.TypeName),
				Format = 8, // TODO: Change this?
			}, _buffer.AsMemory(0, len));
		}
	}

	public class ICCMProperties : PropertiesBase
	{
		public ICCMProperties(Transport c, Window wId) : base(c, wId) { }

		unsafe static ICCMProperties()
		{
			RegisterPropertyType<string>(
				"UTF8_STRING",
				str => Encoding.UTF8.GetMaxByteCount(str.Length),
				data => {
					fixed (byte* ptr = data)
						return Encoding.UTF8.GetString(ptr, data.Length);
				},
				(value, data) => value.WriteOut(data)
			);

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
	}
}