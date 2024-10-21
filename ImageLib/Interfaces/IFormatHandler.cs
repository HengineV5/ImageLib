using MathLib;

namespace ImageLib
{
	public interface IFormatHandler<TConfig> where TConfig : struct, IFormatConfig<TConfig>
	{
		ImageMetadata GetMetadata(Stream stream);

		void Decode<TPixel>(Stream stream, scoped ImageSpan<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>;

		void Encode<TPixel>(Stream stream, scoped ImageSpan<TPixel> image, ref readonly TConfig config) where TPixel : unmanaged, IPixel<TPixel>;
	}

	public interface IFormatConfig<TSelf>
	{
		static abstract TSelf Default { get; }

		static abstract TSelf FromPixel<TPixel>() where TPixel : unmanaged, IPixel<TPixel>;
	}
}
