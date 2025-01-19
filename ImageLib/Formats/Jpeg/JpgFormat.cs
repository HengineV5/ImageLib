using ImageLib.Jxl;
using ImageLib.Png;
using MathLib;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using UtilLib.Memory;
using UtilLib.Span;
using UtilLib.Stream;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ImageLib.Jpg
{
	struct JpgComponent
	{
		public int dcTable;
		public int acTable;
		public int quantizationTable;
		public int coeff;

		public byte horizontalSampling;
		public byte verticalSampling;

		public int maxHorizontalSampling;
		public int maxVerticalSampling;

		public int horizontalSubsampling;
		public int verticalSubsampling;
	}

	struct JpgScanComponentData
	{
		public byte component;
		public byte dcTable;
		public byte acTable;
	}

	ref struct JpgScanHeader
	{
		public byte spectralSelectionStart;
		public byte spectralSelectionEnd;

		public byte approximateBitPositionHigh;
		public byte approximateBitPositionLow;

		public byte components;
		public Span<JpgScanComponentData> componentData;
	}

	public ref struct FixedBuffer44<T> where T : allows ref struct
	{
		private T _element0;
		private T _element1;
		private T _element2;
		private T _element3;

		[UnscopedRef]
		public ref T this[int idx]
		{
			get
			{
				if (idx == 0)
					return ref _element0;
				else if (idx == 1)
					return ref _element1;
				else if (idx == 2)
					return ref _element2;
				else if (idx == 3)
					return ref _element3;
				else
					throw new IndexOutOfRangeException();
			}
		}
	}

	public class JpgFormat : IFormatHandler<JpgConfig>
	{
		static byte[] JPG_IMAGE_START    = [0xFF, 0xD8];
		static byte[] JPG_APP_HEADER     = [0xFF, 0xE0];
		static byte[] JPG_QUANT_TABLE    = [0xFF, 0xDB];
		static byte[] JPG_FRAME_START    = [0xFF, 0xC0];
		static byte[] JPG_FRAME_START_2  = [0xFF, 0xC2];
		static byte[] JPG_HUFF_TABLE     = [0xFF, 0xC4];
		static byte[] JPG_SCAN_START     = [0xFF, 0xDA];
		static byte[] JPG_IMAGE_END      = [0xFF, 0xD9];

		const int MCU_BLOCK_SIZE = 8 * 8;

		public static JpgFormat Instance => new();

		public ImageMetadata GetMetadata(Stream stream)
		{
			using DataReader reader = new DataReader(stream, false);

			Console.WriteLine($"Total length: {reader.Remaining}");

			Span<byte> buff = stackalloc byte[2];
			reader.Read(buff);

			if (!buff.SequenceEqual(JPG_IMAGE_START))
				throw new Exception("Invalid jpeg signature");

			using var stackMem = MemoryPool<byte>.Shared.Rent(2048 * 16); // TODO: Mabye do some approximation here
			scoped SpanStack stack = stackMem.Memory.Span;

			scoped JpgFrameStart frameStart = default;

			while (true)
			{
				reader.Read(buff);

				if (buff.SequenceEqual(JPG_IMAGE_END))
					break;

				var len = reader.ReadUInt16();

				if (buff.SequenceEqual(JPG_FRAME_START) || buff.SequenceEqual(JPG_FRAME_START_2))
				{
					frameStart = JpgHelpers.DecodeFrameStart(reader, ref stack);
					break;
				}
				else if (buff.SequenceEqual(JPG_SCAN_START))
				{
					break;
				}

				reader.Seek(reader.Position + len - 2);
			}

			return new ImageMetadata(frameStart.components, 8, frameStart.width, frameStart.height + 10, 1);
		}

		public void Decode<TPixel>(Stream stream, scoped ImageSpan<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>
		{
			using DataReader reader = new DataReader(stream, false);

			Console.WriteLine($"Total length: {reader.Remaining}");

			Span<byte> buff = stackalloc byte[2];
			reader.Read(buff);

			if (!buff.SequenceEqual(JPG_IMAGE_START))
				throw new Exception("Invalid jpeg signature");

			using var stackMem = MemoryPool<byte>.Shared.Rent(2048 * 32); // TODO: Mabye do some approximation here
			scoped SpanStack stack = stackMem.Memory.Span;

			int quantIdx = 0;
			scoped FixedBuffer44<JpgQuantTable> quantTables = new();

			int dcIdx = 0;
			int acIdx = 0;
			scoped FixedBuffer44<SpanHuffmanTable<byte>> dcTables = new();
			scoped FixedBuffer44<SpanHuffmanTable<byte>> acTables = new();

			scoped JpgFrameStart frameStart = default;
			scoped JpgScanHeader scanHeader = default;

			while (true)
			{
				reader.Read(buff);

				if (buff.SequenceEqual(JPG_IMAGE_END))
					break;

				var len = reader.ReadUInt16();

				if (buff.SequenceEqual(JPG_APP_HEADER))
				{
					Console.WriteLine($"App header: {len}");
					reader.Seek(reader.Position + len - 2);
				}
				else if (buff.SequenceEqual(JPG_QUANT_TABLE))
				{
					Console.WriteLine($"Quant table: {len}");
					quantTables[quantIdx] = JpgHelpers.DecodeQuantTable(reader, ref stack);
					quantIdx++;
				}
				else if (buff.SequenceEqual(JPG_FRAME_START))
				{
					Console.WriteLine($"Frame start: {len}");
					frameStart = JpgHelpers.DecodeFrameStart(reader, ref stack);
				}
				else if (buff.SequenceEqual(JPG_HUFF_TABLE))
				{
					Console.WriteLine($"Huff table: {len}");

					var huff = JpgHelpers.DecodeHuffman(reader, ref stack, out int idx);

					if (huff.type == HuffmanTableType.DC)
					{
						dcTables[dcIdx++] = huff;
					}
					else if (huff.type == HuffmanTableType.AC)
					{
						acTables[acIdx++] = huff;
					}
				}
				else if (buff.SequenceEqual(JPG_SCAN_START))
				{
					Console.WriteLine($"0xFFDA: {reader.Remaining}");

					Console.WriteLine($"Scan start: {len}");

					scanHeader = JpgHelpers.DecodeScanHeader(reader, ref stack);

					break;
				}
			}

			Console.WriteLine($"Before remove: {reader.Remaining}");

			int remaining = (int)reader.Remaining;
			using IMemoryOwner<byte> data = MemoryPool<byte>.Shared.Rent(remaining);
			Span<byte> dataSpan = data.Memory.Span.Slice(0, remaining);

			reader.Read(dataSpan);

			var pre = dataSpan.Length;
			dataSpan = JpgHelpers.FilterFF00(dataSpan);
			Console.WriteLine($"Filtered bytes: {pre - dataSpan.Length}");
			scoped SpanBitReader dataStream = dataSpan;

			/*
			// Precompute IDCT table
			var output = JpgHelpers.PrecalcIDCT(ref stack);

			for (int y = 0; y < 8; y++)
			{
				for (int x = 0; x < 8; x++)
				{
					Console.Write($"{output[y * 8 + x]}f, ");
				}
				Console.WriteLine();
			}
			Console.WriteLine();
			 */

			Console.WriteLine($"Span: {dataSpan.Length}");
			Console.WriteLine($"Start of stream: {dataStream.Remaining / 8}");

			scoped Span<JpgComponent> components = GetComponents(ref frameStart, ref scanHeader, ref stack);

			int elementsPerMcu = components[0].maxHorizontalSampling * components[0].maxVerticalSampling;
			Span<float> dataBuff = stack.Alloc<float>(MCU_BLOCK_SIZE * elementsPerMcu * components.Length);

			Console.WriteLine("Componbents");
			for (int i = 0; i < components.Length; i++)
			{
				Console.WriteLine($"{i}");
				Console.WriteLine($"DcTable: {components[i].dcTable}");
				Console.WriteLine($"\tCount: {dcTables[components[i].dcTable].tree.Nodes}");
				Console.WriteLine($"AcTable: {components[i].acTable}");
				Console.WriteLine($"\tCount: {acTables[components[i].acTable].tree.Nodes}");
			}

			//Console.WriteLine($"Value: {dataSpan[20595]}, {dataSpan[20596]}, {dataSpan[20597]}");
			Console.WriteLine($"Total: {dataStream.Remaining + 2960}");
			Console.WriteLine($"Vertical: {frameStart.componentData[1].samplingVertical}");
			Console.WriteLine($"Horizontal: {frameStart.componentData[1].samplingHorizontal}");

			int rows = (frameStart.height + 8 * components[0].verticalSampling - 1) / (8 * components[0].verticalSampling);
			int cols = (frameStart.width + 8 * components[0].horizontalSampling - 1) / (8 * components[0].horizontalSampling);
			int levelShift = 1 << (frameStart.precision - 1);
			levelShift = 0;

			Console.WriteLine($"Rows: {rows}");
			Console.WriteLine($"Cols: {cols}");

			for (int row = 0; row < rows; row++)
			{
				for (int col = 0; col < cols; col++)
				{
					Console.WriteLine($"x: {col}, y: {row}");

					for (int i = 0; i < components.Length; i++)
					{
						ref JpgComponent component = ref components[i];

						DecodeComponent(ref dataStream, ref component, ref quantTables, ref dcTables, ref acTables, dataBuff.Slice(MCU_BLOCK_SIZE * elementsPerMcu * i, MCU_BLOCK_SIZE * elementsPerMcu), levelShift);
					}

					//Console.WriteLine("---");

					var yBuff = dataBuff.Slice(MCU_BLOCK_SIZE * elementsPerMcu * 0, MCU_BLOCK_SIZE * elementsPerMcu);
					var cbBuff = dataBuff.Slice(MCU_BLOCK_SIZE * elementsPerMcu * 1, MCU_BLOCK_SIZE * elementsPerMcu);
					var crBuff = dataBuff.Slice(MCU_BLOCK_SIZE * elementsPerMcu * 2, MCU_BLOCK_SIZE * elementsPerMcu);

					WriteChunkToImage(col, row, components[0].maxHorizontalSampling, components[0].maxVerticalSampling, yBuff, cbBuff, crBuff, image);

					dataBuff.Clear();

					//throw new Exception();
				}

				//throw new Exception();
			}
		}

		public void Encode<TPixel>(Stream stream, ImageSpan<TPixel> image, ref readonly JpgConfig config) where TPixel : unmanaged, IPixel<TPixel>
		{
			throw new NotImplementedException();
		}

		static void DecodeComponent(scoped ref SpanBitReader data, scoped ref JpgComponent component, scoped ref FixedBuffer44<JpgQuantTable> quantTables, scoped ref FixedBuffer44<SpanHuffmanTable<byte>> dcTables, scoped ref FixedBuffer44<SpanHuffmanTable<byte>> acTables, scoped Span<float> output, int levelShift)
		{
			using IMemoryOwner<float> buff = MemoryPool<float>.Shared.Rent(MCU_BLOCK_SIZE);
			buff.Memory.Span.Clear();

			output.Clear();

			for (int v = 0; v < component.verticalSampling; v++)
			{
				for (int h = 0; h < component.horizontalSampling; h++)
				{
					//output.Clear();

					var sampleOutput = output.Slice(MCU_BLOCK_SIZE * (v * component.maxHorizontalSampling + h), MCU_BLOCK_SIZE);
					//sampleOutput.Clear();

					DecodeChunk(ref data, ref quantTables[component.quantizationTable], in dcTables[component.dcTable], in acTables[component.acTable], ref component.coeff, sampleOutput);

					JpgHelpers.UndoZigZag(sampleOutput, buff.Memory.Span);

					JpgHelpers.CalcIDCT(buff.Memory.Span, sampleOutput);

					//JpgHelpers.ShiftData(sampleOutput, sampleOutput, levelShift);

					buff.Memory.Span.Clear();
				}
			}

			if (component.verticalSampling != component.maxVerticalSampling || component.horizontalSampling != component.maxHorizontalSampling)
			{
				using IMemoryOwner<float> buff2 = MemoryPool<float>.Shared.Rent(MCU_BLOCK_SIZE);
				buff2.Memory.Span.Clear();

				output.Slice(0, MCU_BLOCK_SIZE).TryCopyTo(buff2.Memory.Span);
				//var baseBlock = output.Slice(0, MCU_BLOCK_SIZE);
				var baseBlock = buff2.Memory.Span.Slice(0, MCU_BLOCK_SIZE);
				for (int by = 2 - 1; by >= 0; by--)
				{
					for (int bx = 2 - 1; bx >= 0; bx--)
					{
						//var baseBlock = output.Slice((MCU_BLOCK_SIZE / 4) * (by * 2 + bx), MCU_BLOCK_SIZE / 4);
						var outputBlock = output.Slice(MCU_BLOCK_SIZE * (by * 2 + bx), MCU_BLOCK_SIZE);

						for (int y = 0; y < 8; y++)
						{
							for (int x = 0; x < 8; x++)
							{
								//outputBlock[y * 8 + x] = baseBlock[(y / 2) * 4 + x / 2];
								outputBlock[y * 8 + x] = baseBlock[(y / 2 + by * 4) * 8 + (x / 2) + bx * 4];
							}
						}
					}
				}
				/*
				*/


				for (int y = 16 - 1; y >= 0; y--)
				{
					for (int x = 16 - 1; x >= 0; x--)
					{
						//output[16 * y + x] = output[8 * (y / 2) + x / 2];
						//output[16 * y + x] = output[16 * y + x / 2];
						//Console.WriteLine($"{8 * y + x} = {8 * (y / 2) + (x / 2)}");
						//output[16 * y + x] = 255f;
					}
				}
			}
			/*
			*/
		}

		static void DecodeChunk(scoped ref SpanBitReader data, scoped ref JpgQuantTable quantTable, scoped ref readonly SpanHuffmanTable<byte> dcTable, scoped ref readonly SpanHuffmanTable<byte> acTable, ref int oldDeltaCoeff, scoped Span<float> output)
		{
			//Console.WriteLine("-");
			//Console.WriteLine($"Total: {data.Remaining + 2960}");

			byte category = dcTable.ReadValue(ref data);
			int bits = data.ReadBits(category);

			//Console.WriteLine($"Category: {category}");
			//Console.WriteLine($"Bits: {bits}");

			//bool doPrint = category == 3;
			bool doPrint = false;

			oldDeltaCoeff = JpgHelpers.DecodeNumber(category, bits) + oldDeltaCoeff;
			output[0] = oldDeltaCoeff * quantTable.data[0];

			//Console.WriteLine(oldDeltaCoeff);
			//Console.WriteLine($"	Comp 1: {oldDeltaCoeff}");

			//if (doPrint)
			//	Console.WriteLine($"Nodes: {acTable.tree.Nodes}");

			for (int i = 1; i < 64; i++)
			{
				category = acTable.ReadValue(ref data, doPrint && i == 47);

				//if (doPrint)
				//	Console.WriteLine($"Ac cat {i}: {category}");

				if (category == 0)
					break;

				i += (category & 0xF0) >> 4;
				category = (byte)(category & 0x0F);

				bits = data.ReadBits(category);
				output[i] = JpgHelpers.DecodeNumber(category, bits) * quantTable.data[i];
				//Console.WriteLine($"O: {JpgHelpers.DecodeNumber(category, bits)}");
			}
		}

		static void WriteChunkToImage<TPixel>(int chunkX, int chunkY, int maxHorizontalSampling, int maxVerticalSampling, scoped Span<float> yBuff, scoped Span<float> cbBuff, scoped Span<float> crBuff, scoped ImageSpan<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>
		{
			static Rgb<UInt8, Rgb_Ops_Generic<UInt8>> YCrCbToRGB(float y, float cb, float cr)
			{
				var r = cr * (2 - 2 * 0.299f) + y;
				var b = cb * (2 - 2 * 0.114f) + y;
				var g = (y - 0.114f * b - 0.299f * r) / 0.587f;

				r = float.Clamp(r + 128, 0, 255);
				b = float.Clamp(b + 128, 0, 255);
				g = float.Clamp(g + 128, 0, 255);

				return new((byte)r, (byte)g, (byte)b);
			}

			PixelFormat inputFormat = new(ScalarType.Integer, 3, 1);

			int blockSize = 8;

			for (int by = 0; by < maxVerticalSampling; by++)
			{
				for (int bx = 0; bx < maxHorizontalSampling; bx++)
				{
					var yBuffBlock = yBuff.Slice(MCU_BLOCK_SIZE * (by * 2 + bx), MCU_BLOCK_SIZE);
					//var cbBuffBlock = cbBuff.Slice(MCU_BLOCK_SIZE * (0 * 2 + 0), MCU_BLOCK_SIZE);
					var cbBuffBlock = cbBuff.Slice(MCU_BLOCK_SIZE * (by * 2 + bx), MCU_BLOCK_SIZE);
					//var crBuffBlock = crBuff.Slice(MCU_BLOCK_SIZE * (0 * 2 + 0), MCU_BLOCK_SIZE);
					var crBuffBlock = crBuff.Slice(MCU_BLOCK_SIZE * (by * 2 + bx), MCU_BLOCK_SIZE);

					for (int y = 0; y < blockSize; y++)
					{
						for (int x = 0; x < blockSize; x++)
						{
							//var rgb = YCrCbToRGB(yBuff[y * blockSize + x], cbBuff[yHalf * 8 + xHalf], crBuff[yHalf * 8 + xHalf]);
							//var rgb = YCrCbToRGB(yBuff[y * blockSize + x], yBuff[y * blockSize + x], yBuff[y * blockSize + x]);
							//var rgb = YCrCbToRGB(cbBuff[y * blockSize + x], cbBuff[y * blockSize + x], cbBuff[y * blockSize + x]);

							//var rgb = new Rgb<UInt8, Rgb_Ops_Generic<UInt8>>((byte)yBuffBlock[y * blockSize + x], (byte)yBuffBlock[y * blockSize + x], (byte)yBuffBlock[y * blockSize + x]);
							//var rgb = new Rgb<UInt8, Rgb_Ops_Generic<UInt8>>((byte)cbBuffBlock[y * blockSize + x], (byte)cbBuffBlock[y * blockSize + x], (byte)cbBuffBlock[y * blockSize + x]);
							//var rgb = new Rgb<UInt8, Rgb_Ops_Generic<UInt8>>((byte)crBuffBlock[y * blockSize + x], (byte)crBuffBlock[y * blockSize + x], (byte)crBuffBlock[y * blockSize + x]);

							//var rgb = YCrCbToRGB(yBuffBlock[y * blockSize + x], cbBuffBlock[(y / 2) * blockSize + (x / 2)], crBuffBlock[(y / 2) * blockSize + (x / 2)]);
							var rgb = YCrCbToRGB(yBuffBlock[y * blockSize + x], cbBuffBlock[y * blockSize + x], crBuffBlock[y * blockSize + x]);

							unsafe
							{
								PixelOperations.Read(ref image[chunkX * blockSize * maxHorizontalSampling + bx * blockSize + x, chunkY * blockSize * maxVerticalSampling + by * blockSize + y], in inputFormat, new ReadOnlySpan<byte>((byte*)&rgb, 3));
							}
							//PixelOperations.Read(ref image[x * 8 + xx, y * 8 + yy], in inputFormat);
						}
					}
				}
			}
		}

		static Span<JpgComponent> GetComponents(scoped ref JpgFrameStart frameStart, scoped ref JpgScanHeader scanHeader, scoped ref SpanStack stack)
		{
			Span<JpgComponent> components = stack.Alloc<JpgComponent>(frameStart.components);

			int maxHorizontalSampling = 1;
			int maxVerticalSampling = 1;
			for (int i = 0; i < frameStart.componentData.Length; i++)
			{
				maxHorizontalSampling = int.Max(maxHorizontalSampling, frameStart.componentData[i].samplingHorizontal);
				maxVerticalSampling = int.Max(maxVerticalSampling, frameStart.componentData[i].samplingVertical);
			}

			for (int i = 0; i < components.Length; i++)
			{
				ref var comp = ref components[i];
				comp.quantizationTable = frameStart.componentData[i].quantTable;
				comp.horizontalSampling = frameStart.componentData[i].samplingHorizontal;
				comp.verticalSampling = frameStart.componentData[i].samplingVertical;
				comp.maxHorizontalSampling = maxHorizontalSampling;
				comp.maxVerticalSampling = maxVerticalSampling;
				comp.horizontalSubsampling = maxHorizontalSampling / comp.horizontalSampling;
				comp.verticalSubsampling = maxVerticalSampling / comp.verticalSampling;
				comp.coeff = 0;
				comp.dcTable = scanHeader.componentData[i].dcTable;
				comp.acTable = scanHeader.componentData[i].acTable;
			}

			return components;
		}
	}

	enum HuffmanTableType
	{
		DC = 0,
		AC = 1
	}

	static class JpgHelpers
	{
		static byte[] FF00 = [0xFF, 0x00];

		static byte[] ZIG_ZAG_PATTERN = [
			0, 1, 5, 6, 14, 15, 27, 28,
			2, 4, 7, 13, 16, 26, 29, 42,
			3, 8, 12, 17, 25, 30, 41, 43,
			9, 11, 18, 24, 31, 40, 44, 53,
			10, 19, 23, 32, 39, 45, 52, 54,
			20, 22, 33, 38, 46, 51, 55, 60,
			21, 34, 37, 47, 50, 56, 59, 61,
			35, 36, 48, 49, 57, 58, 62, 63,
		];

		// Precalculation of PrecalcICDT with precision of 8
		static float[] IDCT_TABLE = [
			0.70710677f, 0.70710677f, 0.70710677f, 0.70710677f, 0.70710677f, 0.70710677f, 0.70710677f, 0.70710677f,
			0.98078525f, 0.8314696f, 0.5555702f, 0.19509023f, -0.19509032f, -0.55557036f, -0.83146966f, -0.9807853f,
			0.9238795f, 0.38268343f, -0.38268352f, -0.9238796f, -0.9238795f, -0.38268313f, 0.3826836f, 0.92387956f,
			0.8314696f, -0.19509032f, -0.9807853f, -0.55557f, 0.5555704f, 0.98078525f, 0.19509007f, -0.8314698f,
			0.70710677f, -0.70710677f, -0.70710665f, 0.707107f, 0.70710677f, -0.70710725f, -0.70710653f, 0.7071068f,
			0.5555702f, -0.9807853f, 0.19509041f, 0.83146936f, -0.8314698f, -0.19509022f, 0.9807853f, -0.55557084f,
			0.38268343f, -0.9238795f, 0.92387956f, -0.3826839f, -0.38268298f, 0.9238793f, -0.92387974f, 0.3826839f,
			0.19509023f, -0.55557f, 0.83146936f, -0.9807852f, 0.9807854f, -0.8314696f, 0.55557114f, -0.19509155f
		];

		public static SpanHuffmanTable<byte> DecodeHuffman(DataReader reader, scoped ref SpanStack stack, out int idx)
		{
			byte htInfo = reader.ReadByte();
			idx = htInfo & 0x15; // First four bits;
			HuffmanTableType htType = (HuffmanTableType)((htInfo & 0x16) >> 4); // Fourth bit;

			Span<byte> lengths = stackalloc byte[16];
			reader.Read(lengths);

			Console.Write($"Lengths: ");

			for (int i = 0; i < lengths.Length; i++)
			{
				Console.Write($"{lengths[i]}, ");
			}
			Console.WriteLine();

			int sum = 0;
			for (int i = 0; i < lengths.Length; i++)
			{
				sum += lengths[i];
			}

			Span<byte> elements = stack.Alloc<byte>(sum);
			reader.Read(elements);

			SpanTree<byte> tree = stack.Alloc<SpanTree<byte>.Node>(128);
			SpanHuffmanTable<byte> huffmanTable = new(htType, tree);

			huffmanTable.AddDHT(lengths, elements);

			Console.WriteLine($"Tree Root: {tree.GetRoot()}");
			//tree.PrintTree();

			return huffmanTable;
		}

		public static JpgQuantTable DecodeQuantTable(DataReader reader, scoped ref SpanStack stack)
		{
			byte qtInfo = reader.ReadByte();

			int qtCount = qtInfo & 0xF;
			int qtPercision = (qtInfo & 0xF0) >> 4;

			int count = 64 * (qtPercision + 1);
			Span<byte> table = stack.Alloc<byte>(count);
			reader.Read(table);

			return new()
			{
				data = table
			};
		}

		public static JpgFrameStart DecodeFrameStart(DataReader reader, scoped ref SpanStack stack)
		{
			JpgFrameStart frameStart = new();
			frameStart.precision = reader.ReadByte();
			frameStart.height = reader.ReadInt16();
			frameStart.width = reader.ReadInt16();
			frameStart.components = reader.ReadByte();
			frameStart.componentData = stack.Alloc<JpgFrameComponentData>(frameStart.components);

			for (int i = 0; i < frameStart.components; i++)
			{
				frameStart.componentData[i] = new();
				frameStart.componentData[i].id = reader.ReadByte();

				byte sampling = reader.ReadByte();
				frameStart.componentData[i].samplingVertical = (byte)(sampling & 0xF);
				frameStart.componentData[i].samplingHorizontal = (byte)((sampling & 0xF0) >> 4);
				frameStart.componentData[i].quantTable = reader.ReadByte();
			}

			return frameStart;
		}

		public static JpgScanHeader DecodeScanHeader(DataReader reader, scoped ref SpanStack stack)
		{
			JpgScanHeader scanHeader = new();
			scanHeader.components = reader.ReadByte();
			scanHeader.componentData = stack.Alloc<JpgScanComponentData>(scanHeader.components);

			for (int i = 0; i < scanHeader.components; i++)
			{
				scanHeader.componentData[i].component = reader.ReadByte();

				byte tmp = reader.ReadByte();
				scanHeader.componentData[i].dcTable = (byte)(tmp >> 4);
				scanHeader.componentData[i].acTable = (byte)(tmp & 0x0F);
			}

			scanHeader.spectralSelectionStart = reader.ReadByte();
			scanHeader.spectralSelectionEnd = reader.ReadByte();

			byte approximateBitPosition = reader.ReadByte();
			scanHeader.approximateBitPositionHigh = (byte)(approximateBitPosition >> 4);
			scanHeader.approximateBitPositionLow = (byte)(approximateBitPosition & 0x0F);

			return scanHeader;
		}

		public static Span<byte> FilterFF00(Span<byte> data)
		{
			int charIdx = data.IndexOf(FF00);

			int removed = 0;

			while (charIdx != -1)
			{
				//Console.WriteLine($"Removed {removed} at {charIdx + removed}");

				data.Slice(charIdx + 2).TryCopyTo(data.Slice(charIdx + 1));
				removed++;

				data = data.Slice(0, data.Length - 1);
				int oldCharIdx = charIdx;
				charIdx = data.Slice(charIdx + 1).IndexOf(FF00);

				if (charIdx != -1)
					charIdx += oldCharIdx + 1;
			}

			return data;
		}

		public static ref SpanHuffmanTable<byte> GetHuffmanTable(int idx, ref FixedBuffer44<int> indicies, ref FixedBuffer44<SpanHuffmanTable<byte>> huffmanTables)
		{
			for (int i = 0; i < 4; i++)
			{
				if (indicies[i] == idx)
					return ref huffmanTables[i];
			}

			throw new IndexOutOfRangeException();
		}

		public static int DecodeNumber(int code, int bits)
		{
			int l = 1 << (code - 1); // Pow 2

			return bits - (bits >= l ? 0 : (2 * l - 1));
		}

		public static void UndoZigZag(scoped ReadOnlySpan<float> input, scoped Span<float> output)
		{
			for (int y = 0; y < 8; y++)
			{
				for (int x = 0; x < 8; x++)
				{
					output[y * 8 + x] = input[ZIG_ZAG_PATTERN[y * 8 + x]];
				}
			}
		}

		public static void CalcIDCT(scoped ReadOnlySpan<float> input, scoped Span<float> output, int precision = 8)
		{
			for (int y = 0; y < 8; y++)
			{
				for (int x = 0; x < 8; x++)
				{
					float sum = 0;

					for (int u = 0; u < precision; u++)
					{
						for (int v = 0; v < precision; v++)
						{
							sum += input[v * 8 + u] * IDCT_TABLE[u * 8 + x] * IDCT_TABLE[v * 8 + y];
						}
					}

					output[y * 8 + x] = sum / 4f;
				}
			}
		}

		public static void ShiftData(scoped ReadOnlySpan<float> input, scoped Span<float> output, int levelShift)
		{
			for (int y = 0; y < 8; y++)
			{
				for (int x = 0; x < 8; x++)
				{
					output[y * 8 + x] = output[y * 8 + x] + levelShift;
				}
			}
		}

		public static Span<float> PrecalcIDCT(scoped ref SpanStack stack, int precision = 8)
		{
			static float NormCoeff(int n)
			{
				if (n == 0)
					return 1 / float.Sqrt(2);
				else
					return 1;
			}

			Span<float> data = stack.Alloc<float>(precision * precision);

			for (int y = 0; y < precision; y++)
			{
				for (int x = 0; x < precision; x++)
				{
					data[y * precision + x] = NormCoeff(y) * MathF.Cos(((2f * x + 1f) * y * MathF.PI) / 16f);
				}
			}

			return data;
		}
		/*
		*/
	}
}
