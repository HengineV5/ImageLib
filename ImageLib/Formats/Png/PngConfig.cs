using MathLib;

namespace ImageLib.Png
{
	public struct PngConfig : IFormatConfig<PngConfig>
	{
		public static PngConfig Default => new(8, PngColorType.TruecolorWithAlpha, 0, 0, 0, 6);

		public byte bitDepth;
		public PngColorType colorType;
		public byte compressionMethod;
		public byte filterMethod;
		public byte interlaceMethod;
		public int compressionLevel;

		public PngConfig(byte bitDepth, PngColorType colorType, byte compressionMethod, byte filterMethod, byte interlaceMethod, int compressionLevel)
		{
			this.bitDepth = bitDepth;
			this.colorType = colorType;
			this.compressionMethod = compressionMethod;
			this.filterMethod = filterMethod;
			this.interlaceMethod = interlaceMethod;
			this.compressionLevel = compressionLevel;
		}

		public static PngConfig FromPixel<TPixel>() where TPixel : unmanaged, IPixel<TPixel>
		{
			return new((byte)TPixel.BitDepth, PngHelpers.GetColorType(TPixel.Channels), 0, 0, 0, 6);
		}
	}
}
