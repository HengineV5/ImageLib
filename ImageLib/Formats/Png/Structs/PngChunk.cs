namespace ImageLib.Png
{
	struct PngChunk
	{
		public uint length;
		public string chunkType;
		public Memory<byte> data;
		public uint crc;
	}
}
