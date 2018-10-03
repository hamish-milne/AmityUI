namespace Amity.X11
{
	using System;
	using System.Reflection;
	using System.Runtime.InteropServices;

	
	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct ConnectionRequest
	{
		public byte ByteOrder;
		public ushort MajorVersion;
		public ushort MinorVersion;
		public ushort AuthNameLength;
		public ushort AuthDataLength;
		private ushort _unused;
	}


	public enum WindowClass : ushort
	{
		CopyFromPArent,
		InputOutput,
		InputOnly
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct CreateWindowData
	{
		public byte Opcode;
		public byte Depth;
		public ushort RequestLength;
		public uint WindowId;
		public uint Parent;
		public ushort X;
		public ushort Y;
		public ushort Width;
		public ushort Height;
		public ushort BorderWidth;
		public WindowClass Class;
		public uint Visual;
	}

	public abstract class ValuesBase : IWritable
	{
		public delegate void WriteObject(Span<byte> dst, object obj);

		private static void Write<T>(Span<byte> dst, object obj) where T : struct
		{
			var i = (T)obj;
			MemoryMarshal.Write(dst, ref i);
		}

		private static MethodInfo _writeMethod =
			((WriteObject)Write<int>).Method.GetGenericMethodDefinition();
		
		public int MaxSize => 32*sizeof(int);

		public int WriteTo(Span<byte> data)
		{
			const int elementSize = 4;
			int totalSize = sizeof(uint);
			uint mask = 0;
			var fields = GetType().GetFields();
			var dst = data.Slice(sizeof(uint));
			for (int i = 0; i < fields.Length; i++)
			{
				var obj = fields[i].GetValue(this);
				if (obj != null)
				{
					mask |= (uint)(1 << i);
					var method = _writeMethod.MakeGenericMethod(obj.GetType());
					var del = (WriteObject)Delegate.CreateDelegate(typeof(WriteObject), null, method);
					del(dst, obj);
					dst = data.Slice(elementSize);
					totalSize += elementSize;
				}
			}
			MemoryMarshal.Write(data, ref mask);
			return totalSize;
		}
	}

	public class WindowValues : ValuesBase
	{
		public uint? BackgroundPixmap;
		public uint? BackgroundPixel;
		public uint? BorderPixmap;
		public uint? BorderPixel;
		public byte? BitGravity;
		public byte? WinGravity;
		public BackingStoreType? BackingStore;
		public uint? BackingPlanes;
		public uint? BackingPixel;
		public byte? OverrideRedirect;
		public bool? SaveUnder;
		public Event? EventMask;
		public Event? DoNotPropagateMask;
		public uint? Colormap;
		public uint? Cursor;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct MapWindowData
	{
		public byte Opcode;
		public ushort RequestLength;
		public uint Window;
	}
}