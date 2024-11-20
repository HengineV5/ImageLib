using ImageLib.Png;
using MathLib;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ImageLib.Jxl
{
	public class JxlFormat : IFormatHandler<JxlConfig>
	{
		public static JxlFormat Instance => new();

		public ImageMetadata GetMetadata(Stream stream)
		{
			throw new NotImplementedException();
		}

		public void Decode<TPixel>(Stream stream, scoped ImageSpan<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>
		{
			throw new NotImplementedException();
		}

		public void Encode<TPixel>(Stream stream, ImageSpan<TPixel> image, ref readonly JxlConfig config) where TPixel : unmanaged, IPixel<TPixel>
		{
			throw new NotImplementedException();
		}
	}

	static class JxlHelpers
	{
	}
}
