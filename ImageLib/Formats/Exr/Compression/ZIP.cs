using ZlibNGSharpMinimal.Deflate;
using ZlibNGSharpMinimal.Inflate;

namespace ImageLib.Exr.Compression
{
	public static class ZIP
	{
		static ZngDeflater DEFLATER = new();
		static ZngInflater INFLATER = new();

		public static void Compress(scoped ReadOnlySpan<byte> input, scoped Span<byte> output)
		{
			
		}

		public static ulong Decompress(scoped ReadOnlySpan<byte> input, scoped Span<byte> output)
		{
			INFLATER.Reset();
			return INFLATER.Inflate(input, output);
		}
	}
}
