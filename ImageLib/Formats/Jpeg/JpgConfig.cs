using MathLib;

namespace ImageLib.Jpg
{
	public struct JpgConfig : IFormatConfig<JpgConfig>
	{
		public static JpgConfig Default => default;

		public static JpgConfig FromPixel<TPixel>() where TPixel : unmanaged, IPixel<TPixel>
		{
			return Default;
		}
	}
}
