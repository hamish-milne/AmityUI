namespace Amity
{
	using System;
	using System.Runtime.InteropServices;

	public static class Utility
	{
		public static void AlphaPremultiply(this Span<Color32> data)
		{
			var bytes = MemoryMarshal.Cast<Color32, byte>(data);
			for (int i = 0; i < bytes.Length; i += 4)
			{
				var a = bytes[i + 3];
				bytes[i + 0] = (byte)((bytes[i + 0] * a)/byte.MaxValue); 
				bytes[i + 1] = (byte)((bytes[i + 1] * a)/byte.MaxValue); 
				bytes[i + 2] = (byte)((bytes[i + 2] * a)/byte.MaxValue); 
			}
		}
	}
}