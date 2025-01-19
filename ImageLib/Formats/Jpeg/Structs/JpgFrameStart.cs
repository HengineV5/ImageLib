namespace ImageLib.Jpg
{
	ref struct JpgFrameStart
	{
		public byte precision;
		public short height;
		public short width;
		public byte components;
		public Span<JpgFrameComponentData> componentData;
	}
}
