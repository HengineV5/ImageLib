namespace ImageLib
{
	public struct ImageMetadata
	{
		public int Channels;
		public int Bitdepth;
		public int Width;
		public int Height;
		public int Depth;

		public ImageMetadata(int channels, int bitdepth, int width, int height, int depth)
		{
			Channels = channels;
			Bitdepth = bitdepth;
			Width = width;
			Height = height;
			Depth = depth;
		}
	}
}
