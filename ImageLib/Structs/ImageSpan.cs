using MathLib;
using System;
using System.Runtime.InteropServices;

namespace ImageLib
{
	public ref struct ImageSpan<TPixel> where TPixel : unmanaged, IPixel<TPixel>
	{
		public int Width { get; }

		public int Height { get; }

		public Span<TPixel> data; // TODO: Make private
		int srcWidth;

		int offsetX;
		int offsetY;

		public ref TPixel this[int x, int y]
		{
			get
			{
				if (x >= Width)
					throw new IndexOutOfRangeException();

				if (y >= Height)
					throw new IndexOutOfRangeException();

				return ref data[offsetX + x + (offsetY + y) * srcWidth];
			}
		}

		public Span<TPixel> this[int scanline]
		{
			get
			{
				if (scanline >= Height)
					throw new IndexOutOfRangeException();

				return data.Slice(srcWidth * scanline, Width);
			}
		}

		public ImageSpan(int width, int height, Span<TPixel> span)
		{
			this.Width = width;
			this.srcWidth = width;
			this.Height = height;
			this.offsetX = 0;
			this.offsetY = 0;
			this.data = span;
		}

		private ImageSpan(int width, int srcWidth, int height, int x, int y, Span<TPixel> span)
		{
			this.Width = width;
			this.srcWidth = srcWidth;
			this.Height = height;
			this.offsetX = x;
			this.offsetY = y;
			this.data = span;
		}

		public ImageSpan<TPixel> Slice(int width, int height, int x, int y)
		{
			if (x >= Width)
				throw new IndexOutOfRangeException();

			if (width + x >= Width)
				throw new IndexOutOfRangeException();

			if (y >= Height)
				throw new IndexOutOfRangeException();

			if (height + y >= Height)
				throw new IndexOutOfRangeException();

			return new ImageSpan<TPixel>(width, srcWidth, height, x + offsetX, y + offsetY, data);
		}

		public void CopyTo(scoped Span<TPixel> dest)
		{
			if (dest.Length < Width * Height)
				throw new IndexOutOfRangeException();

			for (int i = 0; i < Height; i++)
			{
				this[i].CopyTo(dest.Slice(i * Width));
			}
		}

		public void CopyTo(scoped Span<byte> dest)
		{
			var bytesPerPixel = (TPixel.BitDepth / 8) * TPixel.Channels;
			var bytesPerScanline = Width * bytesPerPixel;

			if (dest.Length < Width * Height * bytesPerPixel)
				throw new IndexOutOfRangeException();

			for (int i = 0; i < Height; i++)
			{
				var scanline = MemoryMarshal.Cast<TPixel, byte>(this[i]);
				scanline.CopyTo(dest.Slice(i * bytesPerScanline, bytesPerScanline));
			}
		}
	}

	public static class ImageSpanExtensions
	{
		public static ImageSpan<TPixel> Fill<TPixel>(this ImageSpan<TPixel> span, ref readonly TPixel value) where TPixel : unmanaged, IPixel<TPixel>
		{
			for (int y = 0; y < span.Height; y++)
			{
				for (int x = 0; x < span.Width; x++)
				{
					span[x, y] = value;
				}
			}

			return span;
		}

		public static ImageSpan<TPixel> Fill<TPixel>(this ImageSpan<TPixel> span, scoped ImageSpan<TPixel> value) where TPixel : unmanaged, IPixel<TPixel>
		{
			for (int y = 0; y < value.Height; y++)
			{
				for (int x = 0; x < value.Width; x++)
				{
					span[x, y] = value[x, y];
				}
			}

			return span;
		}

		public static ImageSpan<TPixel> FlipVertical<TPixel>(this ImageSpan<TPixel> span) where TPixel : unmanaged, IPixel<TPixel>
		{
			for (int y = 0; y < span.Height / 2; y++)
			{
				for (int x = 0; x < span.Width; x++)
				{
					TPixel tmp = span[x, y];
					span[x, y] = span[x, span.Height - 1 - y];
					span[x, span.Height - 1 - y] = tmp;
				}
			}

			return span;
		}

		public static ImageSpan<TPixel> FlipHorizontal<TPixel>(this ImageSpan<TPixel> span) where TPixel : unmanaged, IPixel<TPixel>
		{
			for (int y = 0; y < span.Height; y++)
			{
				for (int x = 0; x < span.Width / 2; x++)
				{
					TPixel tmp = span[x, y];
					span[x, y] = span[span.Width - 1 - x, y];
					span[span.Width - 1 - x, y] = tmp;
				}
			}

			return span;
		}

		public static ImageSpan<TPixel> Map<TPixel>(this ImageSpan<TPixel> span, scoped ImageSpan<TPixel> source, Func<int, int, (int x, int y)> transform) where TPixel : unmanaged, IPixel<TPixel>
		{
			for (int y = 0; y < span.Height; y++)
			{
				for (int x = 0; x < span.Width; x++)
				{
					(int tx, int ty) = transform(x, y);
					span[x, y] = source[tx, ty];
				}
			}

			return span;
		}
	}

	public static class GraphicsImageExtensions
	{
		public static ImageSpan<TPixel> MipMap<TPixel>(this ImageSpan<TPixel> span, scoped ImageSpan<TPixel> image, int mips = 4) where TPixel : unmanaged, IPixel<TPixel>
		{
			span.Fill(image);
			for (int i = 1; i <= mips; i++)
			{
				float mipWidthFactor = MathF.Pow(0.5f, i);
				float mipHeightFactor = MathF.Pow(0.5f, i);

				int mipWidth = (int)(image.Width * mipWidthFactor);
				int mipHeight = (int)(image.Height * mipHeightFactor);

				int mipXStart = image.Width - 1;
				int mipYStart = (int)(image.Height * mipHeightFactor) - 1;

				span.Slice(mipWidth, mipHeight, mipXStart, mipYStart).Map(image, (int x, int y) =>
				{
					return ((int)(x / mipWidthFactor), (int)(y / mipHeightFactor));
				});
			}

			return span;
		}
	}
}
