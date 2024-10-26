namespace ImageLib.Exr
{
	struct ExrHeader
	{
		public bool isEmpty;

		public Memory<ExrChannel> channels;
		public ExrCompression compression;
		public ExrBoxI dataWindow;
		public ExrBoxI displayWindow;
		public ExrLineOrder lineOrder;
		public float pixelAspectRatio;
		public (float x, float y) screenWindowCenter;
		public float screenWindowWidth;

		public string view;

		public string name;
		public string type;
		public int chunkCount;
	}
}
