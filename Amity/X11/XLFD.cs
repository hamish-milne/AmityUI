namespace Amity.X11
{
	public class XLFD
	{
		public string Foundry { get; set; }
		public string FamilyName { get; set; }
		public string WeightName { get; set; }
		public string Slant { get; set; }
		public string SetWidthName { get; set; }
		public string AddStyle { get; set; }
		public int PixelSize { get; set; }
		public int PointSize { get; set; }
		public int ResolutionX { get; set; }
		public int ResolutionY { get; set; }
		public string Spacing { get; set; }
		public int AverageWidth { get; set; }
		public string CharsetRegistry { get; set; }
		public string CharsetEncoding { get; set; }

		public static FontSlant ParseSlant(string s)
		{
			switch (s)
			{
				case "i": return FontSlant.Italic;
				case "o": return FontSlant.Oblique;
				case "r": default: return FontSlant.Roman;
			}
		}

		public static FontWeight ParseWeight(string s)
		{
			switch (s)
			{
				case "extralight": return FontWeight.ExtraLight;
				case "light": return FontWeight.Light;
				case "semilight": return FontWeight.SemiLight;
				case "medium": default: return FontWeight.Medium;
				case "demibold": return FontWeight.DemiBold;
				case "bold": return FontWeight.Bold;
				case "black": return FontWeight.Black;
			}
		}

		public XLFD() { }

		private static readonly char[] separator = {'-'};
		public XLFD(string font)
		{
			var tokens = font.Split(separator);
			if (tokens.Length != 15)
			{
				return;
				//throw new ArgumentException($"Font name '{font}' is invalid");
			}
			Foundry = tokens[1];
			FamilyName = tokens[2];
			WeightName = tokens[3];
			Slant = tokens[4];
			SetWidthName = tokens[5];
			AddStyle = tokens[6];
			PixelSize = int.Parse(tokens[7]);
			PointSize = int.Parse(tokens[8]);
			ResolutionX = int.Parse(tokens[9]);
			ResolutionY = int.Parse(tokens[10]);
			Spacing = tokens[11];
			AverageWidth = int.Parse(tokens[12]);
			CharsetRegistry = tokens[13];
			CharsetEncoding = tokens[14];
		}

		public override string ToString() =>
			$"-{Foundry}-{FamilyName}-{WeightName}-{Slant}-{SetWidthName}-{AddStyle}-{PixelSize}-{PointSize}-{ResolutionX}-{ResolutionY}-{Spacing}-{AverageWidth}-{CharsetRegistry}-{CharsetEncoding}"
			.ToLowerInvariant();
		
		public XLFD Clone()
		{
			return (XLFD)MemberwiseClone();
		}
	}
}