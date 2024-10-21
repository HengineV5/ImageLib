using MathLib;

namespace ImageLib.Hdr
{
	public struct HdrConfig : IFormatConfig<HdrConfig>
	{
		public static HdrConfig Default => default;

		public static HdrConfig FromPixel<TPixel>() where TPixel : unmanaged, IPixel<TPixel>
		{
			return Default;
		}
	}
}
