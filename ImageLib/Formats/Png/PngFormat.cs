using MathLib;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Xml.Linq;
using UtilLib.Span;
using UtilLib.Stream;
using ZlibNGSharpMinimal.Deflate;
using ZlibNGSharpMinimal.Inflate;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ImageLib.Png
{
	public class PngFormat : IFormatHandler<PngConfig>
	{
		public static PngFormat Instance => new();

		static byte[] PNG_SIGNATURE = [137, 80, 78, 71, 13, 10, 26, 10];

		static ZngDeflater DEFLATER = new();
		static ZngInflater INFLATER = new();

		public ImageMetadata GetMetadata(Stream stream)
		{
			using DataReader reader = new DataReader(stream);

			Span<byte> signature = stackalloc byte[8];
			int r = reader.Read(signature);

			if (!signature.SequenceEqual(PNG_SIGNATURE))
				throw new Exception("Invalid png signature");

			while (true)
			{
				PngChunkHeader header = PngHelpers.ReadChunkHeader(reader);

				if (header.chunkType == "IHDR")
				{
					var ihdr = PngHelpers.ReadIHDR(in header, reader);

					return PngHelpers.GetMetadata(in ihdr);
				}
			}

			throw new Exception("Unable to find image header");
		}

		public unsafe void Decode<TPixel>(Stream stream, scoped ImageSpan<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>
		{
			using DataReader reader = new DataReader(stream);

			Span<byte> signature = stackalloc byte[8];
			int r = reader.Read(signature);

			if (!signature.SequenceEqual(PNG_SIGNATURE))
				throw new Exception("Invalid png signature");

			PngIHDR ihdr = new();
			IMemoryOwner<byte> imgData = MemoryPool<byte>.Shared.Rent(0);
			SpanList<byte> imgDataList = new();

			while (true)
			{
				PngChunkHeader header = PngHelpers.ReadChunkHeader(reader);

				if (header.chunkType == "IEND")
					break;

				if (header.length == 0)
				{
					reader.ReadUInt32();
					continue;
				}

				switch (header.chunkType)
				{
					case "IHDR":
						ihdr = PngHelpers.ReadIHDR(in header, reader);

						if (image.Width < ihdr.width || image.Height < ihdr.height)
							throw new Exception("Provided image span is too small.");

						if (TPixel.BitDepth < ihdr.bitDepth)
							throw new Exception("Cannot load image into frame with a lower bitdepth."); // For future henke: See line 206

						int imgByteSize = PngHelpers.GetPixelChannels(ihdr.colorType) * (ihdr.bitDepth / 8) * (image.Width + 1) * image.Height;
						imgData = MemoryPool<byte>.Shared.Rent(imgByteSize); // TODO: This is probably overkill.
						imgData.Memory.Span.Clear();

						imgDataList = new(imgData.Memory.Span);

						break;
					case "IDAT":
						//PngIDAT idat = PngHelpers.ReadIDAT(in header, reader, ZLIB, ref zStream);
						PngIDAT idat = PngHelpers.ReadIDAT(in header, reader, ref imgDataList);
						break;
					case "sRGB":
						PngsRGB srgb = PngHelpers.ReadsRGB(in header, reader);
						break;
					case "iCCP":
						PngHelpers.ReadiCCP(in header, INFLATER, reader);
						break;
					default:
						// Skip unknown segments.
						reader.Seek(reader.Position + header.length);

						break;
				}

				//Console.WriteLine($"{header.chunkType}: {header.length}, {bytesCount}, {imgDataList.Count}");

				using IMemoryOwner<byte> headerData = MemoryPool<byte>.Shared.Rent((int)header.length);
				Span<byte> headerDataSpan = headerData.Memory.Span.Slice(0, (int)header.length);

				reader.Seek(reader.Position - headerDataSpan.Length);
				reader.Read(headerDataSpan);

				// Read CRC
				var readCrc = reader.ReadUInt32();
				var calcCrc = PngHelpers.CRC32(headerDataSpan, PngHelpers.CRC32(Encoding.ASCII.GetBytes(header.chunkType), 0));

				if (readCrc != calcCrc)
					throw new Exception("Invalid crc");
			}

			using var uncompressed = MemoryPool<byte>.Shared.Rent(imgData.Memory.Length);

			INFLATER.Reset();
			ulong read = INFLATER.Inflate(imgDataList.AsSpan(), uncompressed.Memory.Span);
			ProcessData(in ihdr, uncompressed.Memory.Span.Slice(0, (int)read), image);
		}

		public void Encode<TPixel>(Stream stream, scoped ImageSpan<TPixel> image, ref readonly PngConfig config) where TPixel : unmanaged, IPixel<TPixel>
		{
			using DataWriter writer = new DataWriter(stream);

			writer.Write(PNG_SIGNATURE);

			PngIHDR ihdr = new()
			{
				width = (uint)image.Width,
				height = (uint)image.Height,
				bitDepth = config.bitDepth,
				colorType = config.colorType,
				compressionMethod = config.compressionMethod,
				filterMethod = config.filterMethod,
				interlaceMethod = config.interlaceMethod,
			};

			PngsRGB srgb = new()
			{
				renderingIntent = 0
			};

			PngHelpers.WriteIHDR(in ihdr, writer);
			PngHelpers.WritesRGB(in srgb, writer);

			int imgByteSize = PngHelpers.GetImageByteSize(in ihdr);
			using var imgData = MemoryPool<byte>.Shared.Rent(imgByteSize);
			Span<byte> imgSpan = imgData.Memory.Span.Slice(0, imgByteSize);
			imgSpan.Clear();

			FilterImage(in ihdr, image, imgSpan);
			PngHelpers.WriteIDAT(imgSpan, DEFLATER, writer, config.compressionLevel);

			PngHelpers.WriteIEND(writer);
		}

		static ImageMemory<TPixel> SetupImage<TPixel>(ref readonly PngIHDR ihdr) where TPixel : unmanaged, IPixel<TPixel>
		{
			return Image.CreateEmpty<TPixel>((int)ihdr.width, (int)ihdr.height);
		}

		static unsafe void ProcessData<TPixel>(ref readonly PngIHDR ihdr, scoped ReadOnlySpan<byte> data, scoped ImageSpan<TPixel> img) where TPixel : unmanaged, IPixel<TPixel>
		{
			Span<byte> imgBytes = MemoryMarshal.Cast<TPixel, byte>(img.data); // TODO: Buffer scanlines for accessing purposes.

			PixelFormat inputFormat = new(ScalarType.Integer, PngHelpers.GetPixelChannels(ihdr.colorType), ihdr.bitDepth / 8);

			int bytesPerPixel = inputFormat.channels * inputFormat.bytesPerChannel;
			int stride = (int)ihdr.width * bytesPerPixel + 1;

			int imgBytesPerPixel = TPixel.Channels * (TPixel.BitDepth / 8);
			int imgStride = (int)ihdr.width * imgBytesPerPixel;

			int bytePerPixelRatio = imgBytesPerPixel / bytesPerPixel; // This will break if TPixel bitdepth is smnaller than source image bitdepth

			Span<byte> tmpPixel = stackalloc byte[bytesPerPixel];

			fixed(byte* dataPtr = data)
			fixed(byte* imgBytesPtr = imgBytes)
			{
				for (int scanline = 0; scanline < ihdr.height; scanline++)
				{
					int scanlineStart = scanline * stride;
					byte filterType = dataPtr[scanlineStart];

					if (filterType < 0 || filterType > 4)
						throw new Exception($"Unknown filter type {filterType}");

					for (int pixel = 0; pixel < ihdr.width; pixel++)
					{
						for (int x = 0; x < bytesPerPixel; x++)
						{
							byte xByte = dataPtr[scanline * stride + 1 + (pixel * bytesPerPixel) + x]; // First byte of each scanline is filter type

							tmpPixel[x] = xByte;

							int imgByteOffset = x * bytePerPixelRatio + pixel * imgBytesPerPixel;
							switch (filterType)
							{
								case 1: // Sub
									tmpPixel[x] += GetPixelByte(imgBytesPtr, imgStride, imgByteOffset - imgBytesPerPixel, scanline);
									break;
								case 2: // Up
									tmpPixel[x] += GetPixelByte(imgBytesPtr, imgStride, imgByteOffset, scanline - 1);
									break;
								case 3: // Avg
									tmpPixel[x] += (byte)((GetPixelByte(imgBytesPtr, imgStride, imgByteOffset - imgBytesPerPixel, scanline) + GetPixelByte(imgBytes, imgStride, imgByteOffset, scanline - 1)) / 2);
									break;
								case 4: // Paeth
									byte a = GetPixelByte(imgBytesPtr, imgStride, imgByteOffset - imgBytesPerPixel, scanline);
									byte b = GetPixelByte(imgBytesPtr, imgStride, imgByteOffset, scanline - 1);
									byte c = GetPixelByte(imgBytesPtr, imgStride, imgByteOffset - imgBytesPerPixel, scanline - 1);

									tmpPixel[x] += PngHelpers.PaethPredictor(a, b, c);
									break;
								default:
									break;
							}
						}

						PixelOperations.Read(ref img[pixel, scanline], in inputFormat, tmpPixel);
					}
				}
			}
		}

		static unsafe void ProcessDataO<TPixel>(ref readonly PngIHDR ihdr, scoped ReadOnlySpan<byte> data, scoped ImageSpan<TPixel> img) where TPixel : unmanaged, IPixel<TPixel>
		{
			Span<byte> imgBytes = MemoryMarshal.Cast<TPixel, byte>(img.data);

			PixelFormat inputFormat = new(ScalarType.Integer, PngHelpers.GetPixelChannels(ihdr.colorType), ihdr.bitDepth / 8);

			int bytesPerPixel = inputFormat.channels * inputFormat.bytesPerChannel;
			int stride = (int)ihdr.width * bytesPerPixel + 1;

			int imgBytesPerPixel = TPixel.Channels * (TPixel.BitDepth / 8);
			int imgStride = (int)ihdr.width * imgBytesPerPixel;

			int bytePerPixelRatio = imgBytesPerPixel / bytesPerPixel; // This will break if TPixel bitdepth is smnaller than source image bitdepth

			using var scanlineMem1 = MemoryPool<byte>.Shared.Rent(stride - 1);
			using var scanlineMem2 = MemoryPool<byte>.Shared.Rent(stride - 1);

			bool inverted = false;
			Span<byte> scanlineCurr = scanlineMem1.Memory.Span.Slice(0, stride - 1);
			Span<byte> scanlinePrev = scanlineMem2.Memory.Span.Slice(0, stride - 1);

			for (int scanline = 0; scanline < ihdr.height; scanline++)
			{
				int scanlineStart = scanline * stride;
				byte filterType = data[scanlineStart];

				data.Slice(scanline * stride + 1, stride - 1).TryCopyTo(scanlineCurr);

				switch (filterType)
				{
					case 1: // Sub
						ProcessSubScanline(scanlineCurr);
						break;
					case 2: // Up
						ProcessUpScanline(scanlineCurr, scanlinePrev);
						break;
					case 3: // Avg
						ProcessAvgScanline(scanlineCurr, scanlinePrev);
						break;
					case 4: // Paeth
						ProcessPaethScanline(scanlineCurr, scanlinePrev);
						break;
					default:
						throw new Exception($"Unknown filter type {filterType}");
				}

				for (int pixel = 0; pixel < ihdr.width; pixel++)
				{
					PixelOperations.Read(ref img[pixel, scanline], in inputFormat, data.Slice(scanline * stride + 1 + (pixel * bytesPerPixel)));
				}
			}
		}

		// Current assumptions:
		//  4 bytes per pixel
		//  Scanline has been filled with data
		static void ProcessSubScanline(scoped Span<byte> scanlineCurr)
		{
			Vector64<byte> vecPrev = Vector64<byte>.Zero;
			Vector64<byte> vecCurr = Vector64<byte>.Zero;

			for (int i = 4; i < scanlineCurr.Length; i += 4)
			{
				vecPrev = Vector64.LoadUnsafe(ref scanlineCurr[i - 4]);
				vecCurr = Vector64.LoadUnsafe(ref scanlineCurr[i]);

				vecPrev = Vector64.Add(vecPrev, vecCurr);

				scanlineCurr[i] = vecPrev[0];
				scanlineCurr[i + 1] = vecPrev[1];
				scanlineCurr[i + 2] = vecPrev[2];
				scanlineCurr[i + 3] = vecPrev[3];
			}
		}

		// Current assumptions:
		//  4 bytes per pixel
		//  Scanline has been filled with data
		static void ProcessUpScanline(scoped Span<byte> scanlineCurr, scoped Span<byte> scanlinePrev)
		{
			Vector64<byte> vecPrev = Vector64<byte>.Zero;
			Vector64<byte> vecUp = Vector64<byte>.Zero;

			for (int i = 0; i < scanlineCurr.Length; i += 4)
			{
				vecPrev = Vector64.LoadUnsafe(ref scanlinePrev[i]);
				vecUp = Vector64.LoadUnsafe(ref scanlineCurr[i]);

				vecPrev = Vector64.Add(vecPrev, vecUp);

				scanlineCurr[i] = vecPrev[0];
				scanlineCurr[i + 1] = vecPrev[1];
				scanlineCurr[i + 2] = vecPrev[2];
				scanlineCurr[i + 3] = vecPrev[3];
			}
		}

		// Current assumptions:
		//  4 bytes per pixel
		//  Scanline has been filled with data
		static void ProcessAvgScanline(scoped Span<byte> scanlineCurr, scoped Span<byte> scanlinePrev)
		{
			Vector64<byte> vecPrev = Vector64<byte>.Zero;
			Vector64<byte> vecUp = Vector64<byte>.Zero;
			Vector64<byte> vecCurr = Vector64<byte>.Zero;

			for (int i = 0; i < 4; i += 4)
			{
				vecUp = Vector64.LoadUnsafe(ref scanlinePrev[i]);
				vecCurr = Vector64.LoadUnsafe(ref scanlineCurr[i]);

				vecUp = Vector64.Add(Vector64.Divide(vecUp, (byte)2), vecCurr);

				scanlineCurr[i] = vecUp[0];
				scanlineCurr[i + 1] = vecUp[1];
				scanlineCurr[i + 2] = vecUp[2];
				scanlineCurr[i + 3] = vecUp[3];
			}

			for (int i = 4; i < scanlineCurr.Length; i += 4)
			{
				vecUp = Vector64.LoadUnsafe(ref scanlinePrev[i]);
				vecPrev = Vector64.LoadUnsafe(ref scanlineCurr[i - 4]);
				vecCurr = Vector64.LoadUnsafe(ref scanlineCurr[i]);

				vecUp = Vector64.Add(Vector64.Divide(Vector64.Add(vecUp, vecPrev), (byte)2), vecCurr);

				scanlineCurr[i] = vecUp[0];
				scanlineCurr[i + 1] = vecUp[1];
				scanlineCurr[i + 2] = vecUp[2];
				scanlineCurr[i + 3] = vecUp[3];
			}
		}

		// Current assumptions:
		//  4 bytes per pixel
		//  Scanline has been filled with data
		static unsafe void ProcessPaethScanline(scoped Span<byte> scanlineCurr, scoped Span<byte> scanlinePrev)
		{
			fixed(byte* scanlineCurrPtr = scanlineCurr)
			fixed(byte* scanlinePrevPtr = scanlinePrev)
			{
				for (int i = 4; i < scanlineCurr.Length; i += 4)
				{
					Vector128<short>  ass = Vector128.Create(scanlineCurrPtr[i - 4], scanlineCurrPtr[i - 4 + 1], scanlineCurrPtr[i - 4 + 2], scanlineCurrPtr[i - 4 + 3], 0, 0, 0, 0);
					Vector128<short>  bs = Vector128.Create(scanlinePrevPtr[i], scanlinePrevPtr[i + 1], scanlinePrevPtr[i + 2], scanlinePrevPtr[i + 3], 0, 0, 0, 0);
					Vector128<short>  cs = Vector128.Create(scanlinePrevPtr[i - 4], scanlinePrevPtr[i - 4 + 1], scanlinePrevPtr[i - 4 + 2], scanlinePrevPtr[i - 4 + 3], 0, 0, 0, 0);

					Vector128<ushort> pa = Ssse3.Abs(Ssse3.Subtract(bs, cs));
					Vector128<ushort> pb = Ssse3.Abs(Ssse3.Subtract(ass, cs));
					Vector128<ushort> pc = Ssse3.Abs(Ssse3.Subtract(Ssse3.Subtract(Ssse3.Add(ass, bs), cs), cs));

					Avx.BlendVariable(bs, cs, Vector128.As<ushort, short>(Vector128.LessThanOrEqual(pb, pc)));
					//	Vector128.ConditionalSelect(Vector128.As<ushort, short>(Vector128.LessThanOrEqual(pb, pc)), bs, cs)
					
					//Vector128<short> result = Vector128.ConditionalSelect(Vector128.As<ushort, short>(Vector128.BitwiseAnd(Vector128.LessThanOrEqual(pa, pb), Vector128.LessThanOrEqual(pa, pc))), ass, Vector128.ConditionalSelect(Vector128.As<ushort, short>(Vector128.LessThanOrEqual(pb, pc)), bs, cs));
					Vector128<short> result = Avx.BlendVariable(ass, Avx.BlendVariable(bs, cs, Vector128.As<ushort, short>(Vector128.LessThanOrEqual(pb, pc))), Vector128.As<ushort, short>(Vector128.BitwiseAnd(Vector128.LessThanOrEqual(pa, pb), Vector128.LessThanOrEqual(pa, pc))));

					scanlineCurrPtr[i] = (byte)result[0];
					scanlineCurrPtr[i + 1] = (byte)result[1];
					scanlineCurrPtr[i + 2] = (byte)result[2];
					scanlineCurrPtr[i + 3] = (byte)result[3];
				}
			}
		}

		/*
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte PaethPredictor(byte a, byte b, byte c)
		{
			Vector128<int> vec = Vector128.Abs(Vector128.Create(b - c, a - c, a + b - 2 * c, 0));

			if (vec[0] <= vec[1] && vec[0] <= vec[2])
				return a;

			if (vec[1] <= vec[2])
				return b;

			return c;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte PaethPredictor(byte a, byte b, byte c)
		{
			int p = a + b - c;
			int pa = int.Abs(p - a);
			int pb = int.Abs(p - b);
			int pc = int.Abs(p - c);

			if (pa <= pb && pa <= pc)
				return a;

			if (pb <= pc)
				return b;

			return c;
		}
		*/

		static void FilterImage<TPixel>(ref readonly PngIHDR ihdr, scoped ImageSpan<TPixel> img, scoped Span<byte> output) where TPixel : unmanaged, IPixel<TPixel>
		{
			PixelFormat outputFormat = new(ScalarType.Integer, PngHelpers.GetPixelChannels(ihdr.colorType), ihdr.bitDepth / 8);
			int bytesPerPixel = outputFormat.channels * outputFormat.bytesPerChannel;
			int stride = img.Width * bytesPerPixel;

			for (int scanline = 0; scanline < img.Height; scanline++)
			{
				Span<byte> outputScanline = output.Slice(1 + (stride + 1) * scanline, stride);
				Span<TPixel> imgScanline = img[scanline];

				for (int x = 0; x < imgScanline.Length; x++)
				{
					PixelOperations.Write(in imgScanline[x], in outputFormat, outputScanline.Slice(x * bytesPerPixel, bytesPerPixel));
				}
			}

			for (int scanline = img.Height - 1; scanline >= 0; scanline--)
			{
				Span<byte> scanlineSpan = output.Slice(1 + (stride + 1) * scanline, stride);

				if (scanline == 0)
				{
					output[(stride + 1) * scanline] = 1;
				}
				else
				{
					output[(stride + 1) * scanline] = 4;
				}

				for (int i = scanlineSpan.Length - 1; i >= 0; i--)
				{
					if (scanline == 0)
					{
						scanlineSpan[i] -= GetPixelByte(output, stride + 1, i - bytesPerPixel, scanline, 1);
					}
					else
					{
						byte a = GetPixelByte(output, stride + 1, i - bytesPerPixel, scanline, 1);
						byte b = GetPixelByte(output, stride + 1, i, scanline - 1, 1);
						byte c = GetPixelByte(output, stride + 1, i - bytesPerPixel, scanline - 1, 1);

						scanlineSpan[i] -= PngHelpers.PaethPredictor(a, b, c);
					}
				}
			}

			/*
//scanlineSpan[i] -= GetPixelByte(output, outputStride + 1, i - outputBytesPerPixel, scanline, 1);
//scanlineSpan[i] -= GetPixelByte(output, outputStride + 1, i, scanline - 1, 1);
//scanlineSpan[i] -= (byte)((GetPixelByte(output, outputStride + 1, i - outputBytesPerPixel, scanline, 1) + GetPixelByte(output, outputStride + 1, i, scanline - 1, 1)) / 2);

byte a = GetPixelByte(output, outputStride + 1, i - outputBytesPerPixel, scanline, 1);
byte b = GetPixelByte(output, outputStride + 1, i, scanline - 1, 1);
byte c = GetPixelByte(output, outputStride + 1, i - outputBytesPerPixel, scanline - 1, 1);

scanlineSpan[i] -= PngHelpers.PaethPredictor(a, b, c);
*/
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static unsafe byte GetPixelByte(scoped Span<byte> data, int stride, int x, int y, int xOffset = 0)
		{
			ref var dataRef = ref MemoryMarshal.AsRef<byte>(data);
			return x < 0 || y < 0 ? (byte)0 : Unsafe.Add(ref dataRef, y * stride + x + xOffset);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static unsafe byte GetPixelByte(byte* dataPtr, int stride, int x, int y, int xOffset = 0)
		{
			return x < 0 || y < 0 ? (byte)0 : dataPtr[y * stride + x + xOffset];
		}
	}

	internal static class PngHelpers
	{
		// Precomputed CRC table from https://www.w3.org/TR/2003/REC-PNG-20031110/#D-CRCAppendix
		static long[] CRC_TABLE = [0, 1996959894, 3993919788, 2567524794, 124634137, 1886057615, 3915621685, 2657392035, 249268274, 2044508324, 3772115230, 2547177864, 162941995, 2125561021, 3887607047, 2428444049, 498536548, 1789927666, 4089016648, 2227061214, 450548861, 1843258603, 4107580753, 2211677639, 325883990, 1684777152, 4251122042, 2321926636, 335633487, 1661365465, 4195302755, 2366115317, 997073096, 1281953886, 3579855332, 2724688242, 1006888145, 1258607687, 3524101629, 2768942443, 901097722, 1119000684, 3686517206, 2898065728, 853044451, 1172266101, 3705015759, 2882616665, 651767980, 1373503546, 3369554304, 3218104598, 565507253, 1454621731, 3485111705, 3099436303, 671266974, 1594198024, 3322730930, 2970347812, 795835527, 1483230225, 3244367275, 3060149565, 1994146192, 31158534, 2563907772, 4023717930, 1907459465, 112637215, 2680153253, 3904427059, 2013776290, 251722036, 2517215374, 3775830040, 2137656763, 141376813, 2439277719, 3865271297, 1802195444, 476864866, 2238001368, 4066508878, 1812370925, 453092731, 2181625025, 4111451223, 1706088902, 314042704, 2344532202, 4240017532, 1658658271, 366619977, 2362670323, 4224994405, 1303535960, 984961486, 2747007092, 3569037538, 1256170817, 1037604311, 2765210733, 3554079995, 1131014506, 879679996, 2909243462, 3663771856, 1141124467, 855842277, 2852801631, 3708648649, 1342533948, 654459306, 3188396048, 3373015174, 1466479909, 544179635, 3110523913, 3462522015, 1591671054, 702138776, 2966460450, 3352799412, 1504918807, 783551873, 3082640443, 3233442989, 3988292384, 2596254646, 62317068, 1957810842, 3939845945, 2647816111, 81470997, 1943803523, 3814918930, 2489596804, 225274430, 2053790376, 3826175755, 2466906013, 167816743, 2097651377, 4027552580, 2265490386, 503444072, 1762050814, 4150417245, 2154129355, 426522225, 1852507879, 4275313526, 2312317920, 282753626, 1742555852, 4189708143, 2394877945, 397917763, 1622183637, 3604390888, 2714866558, 953729732, 1340076626, 3518719985, 2797360999, 1068828381, 1219638859, 3624741850, 2936675148, 906185462, 1090812512, 3747672003, 2825379669, 829329135, 1181335161, 3412177804, 3160834842, 628085408, 1382605366, 3423369109, 3138078467, 570562233, 1426400815, 3317316542, 2998733608, 733239954, 1555261956, 3268935591, 3050360625, 752459403, 1541320221, 2607071920, 3965973030, 1969922972, 40735498, 2617837225, 3943577151, 1913087877, 83908371, 2512341634, 3803740692, 2075208622, 213261112, 2463272603, 3855990285, 2094854071, 198958881, 2262029012, 4057260610, 1759359992, 534414190, 2176718541, 4139329115, 1873836001, 414664567, 2282248934, 4279200368, 1711684554, 285281116, 2405801727, 4167216745, 1634467795, 376229701, 2685067896, 3608007406, 1308918612, 956543938, 2808555105, 3495958263, 1231636301, 1047427035, 2932959818, 3654703836, 1088359270, 936918000, 2847714899, 3736837829, 1202900863, 817233897, 3183342108, 3401237130, 1404277552, 615818150, 3134207493, 3453421203, 1423857449, 601450431, 3009837614, 3294710456, 1567103746, 711928724, 3020668471, 3272380065, 1510334235, 755167117];

		public static PngChunkHeader ReadChunkHeader(DataReader reader)
		{
			PngChunkHeader header = new();
			header.length = reader.ReadUInt32();

			for (int i = 0; i < 4; i++)
			{
				header.chunkType += reader.ReadChar();
			}

			return header;
		}

		public static void WriteChunk(string chunkType, scoped ReadOnlySpan<byte> data, DataWriter writer)
		{
			writer.Write(data.Length);
			writer.Write(chunkType);
			writer.Write(data);
			writer.Write(CRC32(data, CRC32(Encoding.ASCII.GetBytes(chunkType), 0)));
		}

		public static PngIHDR ReadIHDR(ref readonly PngChunkHeader header, DataReader reader)
		{
			if (header.length != 13)
				throw new Exception();

			PngIHDR idhr = new();
			idhr.width = reader.ReadUInt32();
			idhr.height = reader.ReadUInt32();
			idhr.bitDepth = reader.ReadByte();
			idhr.colorType = reader.ReadByte();
			idhr.compressionMethod = reader.ReadByte();
			idhr.filterMethod = reader.ReadByte();
			idhr.interlaceMethod = reader.ReadByte();

			return idhr;
		}

		public static void WriteIHDR(ref readonly PngIHDR ihdr, DataWriter writer)
		{
			SpanList<byte> data = stackalloc byte[13];
			data.Add(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(ihdr.width)));
			data.Add(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(ihdr.height)));
			data.Add(ihdr.bitDepth);
			data.Add(ihdr.colorType);
			data.Add(ihdr.compressionMethod);
			data.Add(ihdr.filterMethod);
			data.Add(ihdr.interlaceMethod);

			WriteChunk("IHDR", data.AsSpan(), writer);
		}

		static int read = 0;

		public static PngIDAT ReadIDAT(ref readonly PngChunkHeader header, DataReader reader, ref SpanList<byte> data)
		{
			reader.Read(data.Reserve((int)header.length));

			return default;
		}

		public static void WriteIDAT(scoped ReadOnlySpan<byte> data, ZngDeflater deflater, DataWriter writer, int compressionLevel)
		{
			using var pngData = MemoryPool<byte>.Shared.Rent(data.Length * 2);
			pngData.Memory.Span.Clear();

			deflater.Reset();
			ulong size = deflater.Deflate(data, pngData.Memory.Span, DeflateFlushMethod.Finish);

			WriteChunk("IDAT", pngData.Memory.Span.Slice(0, (int)size), writer);
		}

		public static void WriteIEND(DataWriter writer)
		{
			WriteChunk("IEND", [], writer);
		}

		public static PngsRGB ReadsRGB(ref readonly PngChunkHeader header, DataReader reader)
		{
			if (header.length != 1)
				throw new Exception();

			PngsRGB sRGB = new();
			sRGB.renderingIntent = reader.ReadByte();

			return sRGB;
		}

		public static void ReadiCCP(ref readonly PngChunkHeader header, ZngInflater inflater, DataReader reader)
		{
			Span<byte> buff = stackalloc byte[79];
			int nameLength = reader.ReadUntill(buff, 0x0);

			byte compressionMethod = reader.ReadByte();

			Span<byte> data = stackalloc byte[(int)header.length - nameLength - 1];
			reader.Read(data);

			Span<byte> data2 = stackalloc byte[data.Length * 4];
			inflater.Reset();
			inflater.Inflate(data, data2);
		}

		public static void WritesRGB(ref readonly PngsRGB srgb, DataWriter writer)
		{
			SpanList<byte> data = stackalloc byte[1];
			data.Add(srgb.renderingIntent);

			WriteChunk("sRGB", data.AsSpan(), writer);
		}

		public static int GetPixelChannels(int colorType)
		{
			switch (colorType)
			{
				case 0:
					return 1;
				case 2:
					return 3;
				case 4:
					return 2;
				case 6:
					return 4;
				default:
					throw new Exception("Unknown color type");
			}
		}

		public static byte GetColorType(int channels)
		{
			switch (channels)
			{
				case 1:
					return 0;
				case 2:
					return 4;
				case 3:
					return 2;
				case 4:
					return 6;
				default:
					throw new Exception("Unsupported number of channels");
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte PaethPredictor(byte a, byte b, byte c)
		{
			Vector128<int> vec = Vector128.Abs(Vector128.Create(b - c, a - c, a + b - 2 * c, 0));

			if (vec[0] <= vec[1] && vec[0] <= vec[2])
				return a;

			if (vec[1] <= vec[2])
				return b;

			return c;
		}

		/*
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte PaethPredictor(byte a, byte b, byte c)
		{
			int p = a + b - c;
			int pa = int.Abs(p - a);
			int pb = int.Abs(p - b);
			int pc = int.Abs(p - c);

			if (pa <= pb && pa <= pc)
				return a;

			if (pb <= pc)
				return b;

			return c;
		}
		*/

		public static byte ReconstructSub(byte x, byte a)
		{
			return (byte)(int.Abs(x - a) & 0xFF);
		}

		public static byte ReconstructUp(byte x, byte b)
		{
			return (byte)(int.Abs(x - b) & 0xFF);
		}

		public static byte ReconstructAverage(byte x, byte a, byte b)
		{
			return (byte)(int.Abs(x - (a + b) / 2) & 0xFF);
		}

		public static byte ReconstructPaeth(byte x, byte a, byte b, byte c)
		{
			return (byte)(int.Abs(x - PaethPredictor(a, b, c)) & 0xFF);
		}

		public static ImageMetadata GetMetadata(ref readonly PngIHDR ihdr)
		{
			return new ImageMetadata(GetPixelChannels(ihdr.colorType), ihdr.bitDepth, (int)ihdr.width, (int)ihdr.height, 1);
		}

		public static uint CRC32(scoped ReadOnlySpan<byte> data, uint crc)
		{
			uint c = crc ^ 0xffffffff;

			for (int i = 0; i < data.Length; i++)
			{
				c = (uint)CRC_TABLE[(c ^ data[i]) & 255] ^ ((c >> 8) & 0xFFFFFF);
			}

			return c ^ 0xffffffff;
		}

		public static int GetImageByteSize(ref readonly PngIHDR ihdr)
		{
			int bytesPerPixel = GetPixelChannels(ihdr.colorType) * (ihdr.bitDepth / 8);
			return bytesPerPixel * (int)ihdr.height * (int)ihdr.width + (int)ihdr.height;
		}
	}
}
