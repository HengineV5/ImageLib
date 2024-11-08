using Engine.Utils;
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

namespace ImageLib.Hdr
{
	public class HdrFormat : IFormatHandler<HdrConfig>
	{
		public static HdrFormat Instance => new();

		static string HDR_FORMAT_SPECIFIER = "FORMAT=32-bit_rle_rgbe\n";
		static string HDR_EXPOSURE = "EXPOSURE=";
		static string HDR_GAMMA = "GAMMA=";

		public ImageMetadata GetMetadata(Stream stream)
		{
			// Read header, not really interested. https://www.graphics.cornell.edu/~bjw/rgbe.html for documentation
			HdrHeader header = ReadHeader(stream);

			return new ImageMetadata(3, 16, header.width, header.height, 1);
		}

		public void Decode<TPixel>(Stream stream, scoped ImageSpan<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>
		{
			// Read header, not really interested. https://www.graphics.cornell.edu/~bjw/rgbe.html for documentation
			HdrHeader header = ReadHeader(stream);

			if (header.width < 8 || header.width > 0x7fff)
				throw new NotImplementedException(); // TODO: RLE not supported, read flat.

			Span<byte> rgbeBuff = stackalloc byte[4];
			Span<byte> channelBuff = stackalloc byte[2];
			Span<byte> pixelBuff = stackalloc byte[8];

			IMemoryOwner<byte> row = MemoryPool<byte>.Shared.Rent(header.width * 4);

			PixelFormat pixelFormat = new(ScalarType.Integer, 4, 2); // TODO: Change this to floating
			for (int scanline = 0; scanline < header.height; scanline++)
			{
				stream.Read(rgbeBuff);

				if (rgbeBuff[0] != 2 || rgbeBuff[1] != 2 || (rgbeBuff[2] & 0x80) != 0)
				{
					throw new Exception("Not RLE encoded");
				}
				else
				{
					for (int channel = 0; channel < 4; channel++)
					{
						ReadScanlineChannelRLE(stream, header.width, channel, channelBuff, row.Memory.Span);
					}
				}

				for (int pixel = 0; pixel < header.width; pixel++)
				{
					float f = 1 * MathF.Pow(2, row.Memory.Span[pixel + header.width * 3] - (128 + 8));
					float r = row.Memory.Span[pixel] * f;
					float g = row.Memory.Span[pixel + header.width * 1] * f;
					float b = row.Memory.Span[pixel + header.width * 2] * f;


					BitConverter.TryWriteBytes(pixelBuff.Slice(0, 2), (ushort)MathF.Min(65535, r * ushort.MaxValue));
					BitConverter.TryWriteBytes(pixelBuff.Slice(2, 2), (ushort)MathF.Min(65535, g * ushort.MaxValue));
					BitConverter.TryWriteBytes(pixelBuff.Slice(4, 2), (ushort)MathF.Min(65535, b * ushort.MaxValue));

					PixelOperations.Read(ref image[pixel, scanline], in pixelFormat, pixelBuff);
				}
			}
		}

		public void Encode<TPixel>(Stream stream, ImageSpan<TPixel> image, ref readonly HdrConfig config) where TPixel : unmanaged, IPixel<TPixel>
		{
			throw new NotImplementedException();
		}

		static HdrHeader ReadHeader(Stream stream)
		{
			Span<byte> buff = stackalloc byte[128];
			Span<char> charBuff = MemoryMarshal.Cast<byte, char>(buff);

			if (!HdrHelpers.ReadLine(stream, buff, out int length))
				throw new Exception();

			//if (buff[0] != '#' || buff[1] == '?')
			//	throw new Exception("Invalid HDR file.");

			if (!HdrHelpers.ReadLine(stream, buff, out length))
				throw new Exception();

			while (true)
			{
				if (buff[0] == 0 || buff[0] == '\n')
					throw new Exception("No format specifier found.");

				if (HdrHelpers.FindIdx(buff, HDR_FORMAT_SPECIFIER) != -1)
					break;

				if (HdrHelpers.FindIdx(buff, HDR_GAMMA) != -1)
				{
					// TODO: If needed next bytes after this is gamma.
				}

				if (HdrHelpers.FindIdx(buff, HDR_EXPOSURE) != -1)
				{
					// TODO: If needed next bytes after this is exposure.
				}

				if (!HdrHelpers.ReadLine(stream, buff, out length))
					throw new Exception();
			}

			if (!HdrHelpers.ReadLine(stream, buff, out length))
				throw new Exception();

			if (buff[0] != '\n')
				throw new Exception("Missing blank line after format specifier.");

			if (!HdrHelpers.ReadLine(stream, buff, out length))
				throw new Exception();

			int yIdx = HdrHelpers.FindIdx(buff, 'Y');
			int xIdx = HdrHelpers.FindIdx(buff, 'X');

			int width = int.Parse(buff.Slice(xIdx + 2, length - xIdx - 3));
			int height = int.Parse(buff.Slice(yIdx + 2, xIdx - yIdx - 4));

			HdrHeader header = new();
			header.width = width;
			header.height = height;

			return header;
		}

		static void ReadScanlineChannelRLE(Stream stream, int width, int channel, scoped Span<byte> channelBuff, scoped Span<byte> row)
		{
			int a = 0;
			while (a < width)
			{
				if (stream.Read(channelBuff) < channelBuff.Length)
					throw new Exception();

				if (channelBuff[0] > 128)
				{
					int count = channelBuff[0] - 128;
					if (count == 0 || count > width - a)
						throw new Exception();

					for (int b = 0; b < count; b++)
					{
						row[a + width * channel] = channelBuff[1];
						a++;
					}
				}
				else
				{
					int count = channelBuff[0];
					if (count == 0 || count > width - a)
						throw new Exception();

					row[a + width * channel] = channelBuff[1];
					a++;

					if (--count > 0)
					{
						stream.Read(row.Slice(a + width * channel, count * 1));

						a += count;
					}
				}
			}
		}

		ImageMemory<TPixel> ReadFlat<TPixel>(Stream stream, int width, int height) where TPixel : unmanaged, IPixel<TPixel>
		{
			var img = Image.CreateEmpty<TPixel>(width, height);

			Span<byte> floatPixelBuff = stackalloc byte[4];
			Span<byte> pixelBuff = stackalloc byte[4];

			PixelFormat pixelFormat = new(ScalarType.Integer, 4, 2); // TODO: Change to float
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					stream.Read(floatPixelBuff);

					if (floatPixelBuff[3] == 0)
					{
						pixelBuff.Clear();
						PixelOperations.Read(ref img[x, y], in pixelFormat, pixelBuff);
						continue;
					}

					float f = 1 * MathF.Pow(2, floatPixelBuff[3] - (128 + 8));
					float r = floatPixelBuff[0] * f;
					float g = floatPixelBuff[1] * f;
					float b = floatPixelBuff[2] * f;

					BitConverter.TryWriteBytes(pixelBuff.Slice(0, 1), (ushort)MathF.Min(65535, r * ushort.MaxValue));
					BitConverter.TryWriteBytes(pixelBuff.Slice(2, 1), (ushort)MathF.Min(65535, g * ushort.MaxValue));
					BitConverter.TryWriteBytes(pixelBuff.Slice(4, 1), (ushort)MathF.Min(65535, b * ushort.MaxValue));

					PixelOperations.Read(ref img[x, y], in pixelFormat, pixelBuff);
				}
			}

			return img;
		}
	}

	static class HdrHelpers
	{
		public static bool ReadLine(Stream stream, scoped SpanList<byte> buffer, out int length, char newLine = '\n')
		{
			length = 0;
			while (stream.CanRead)
			{
				byte c = (byte)stream.ReadByte();

				length++;
				buffer.Add(c);

				if (c == newLine)
					return true;
			}

			return false;
		}

		public static int FindIdx(scoped Span<byte> buff, scoped ReadOnlySpan<char> data)
		{
			int charsFound = 0;
			for (int i = 0; i < buff.Length; i++)
			{
				if (charsFound == data.Length)
					return i - charsFound;

				if (buff[i] == data[charsFound])
					charsFound++;
			}

			return -1;
		}

		public static int FindIdx(scoped Span<byte> buff, byte data)
		{
			for (int i = 0; i < buff.Length; i++)
			{
				if (buff[i] == data)
					return i;
			}

			return -1;
		}

		public static int FindIdx(scoped Span<byte> buff, char data)
		{
			return FindIdx(buff, (byte)data);
		}
	}
}
