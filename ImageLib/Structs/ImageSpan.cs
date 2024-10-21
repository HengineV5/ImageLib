using MathLib;

namespace ImageLib
{
	public ref struct ImageSpan<TPixel> where TPixel : unmanaged, IPixel<TPixel>
	{
		public int width;
		public int height;

		public Span<TPixel> data;

		public ref TPixel this[int x, int y]
		{
			get
			{
				return ref data[x + y * width];
			}
		}

		public Span<TPixel> this[int scanline]
		{
			get
			{
				return data.Slice(width * scanline, width);
			}
		}

		public ImageSpan(int width, int height, Span<TPixel> span)
		{
			this.width = width;
			this.height = height;
			this.data = span;
		}
	}
}
