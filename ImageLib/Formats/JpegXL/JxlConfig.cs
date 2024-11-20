using MathLib;

namespace ImageLib.Jxl
{
	public struct JxlConfig : IFormatConfig<JxlConfig>
	{
		public static JxlConfig Default => default;

		public static JxlConfig FromPixel<TPixel>() where TPixel : unmanaged, IPixel<TPixel>
		{
			return Default;
		}
	}
}
