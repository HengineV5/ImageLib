namespace ImageLib.Png
{
	public enum PngColorType
	{
		Greyscale = 0,
		TrueColor = 2,
		IndexedColor = 3,
		GreyscaleWithAlpha = 4,
		TruecolorWithAlpha = 6
	}

	struct PngIHDR
	{
		public uint width;
		public uint height;
		public byte bitDepth;
		public PngColorType colorType;
		public byte compressionMethod;
		public byte filterMethod;
		public byte interlaceMethod;
	}
}
