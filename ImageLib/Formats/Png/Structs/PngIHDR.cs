namespace ImageLib.Png
{
	struct PngIHDR
	{
		public uint width;
		public uint height;
		public byte bitDepth;
		public byte colorType;
		public byte compressionMethod;
		public byte filterMethod;
		public byte interlaceMethod;
	}
}
