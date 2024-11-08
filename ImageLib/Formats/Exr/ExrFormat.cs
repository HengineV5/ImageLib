using Engine.Utils;
using ImageLib.Exr.Compression;
using ImageLib.Hdr;
using MathLib;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageLib.Exr
{
	struct ExrPart
	{
		public ExrHeader header;
		public Memory<ulong> offsets;
	}

	public struct ExrConfig : IFormatConfig<ExrConfig>
	{
		public static ExrConfig Default => throw new NotImplementedException();

		public static ExrConfig FromPixel<TPixel>() where TPixel : unmanaged, MathLib.IPixel<TPixel>
		{
			throw new NotImplementedException();
		}
	}

	public class ExrFormat : IFormatHandler<ExrConfig>
	{
		public static ExrFormat Instance => new();

		static byte[] EXR_SIGNATURE = [0x76, 0x2f, 0x31, 0x01];
		 
		public ImageMetadata GetMetadata(Stream stream)
		{
			return default;
		}

		public void Decode<TPixel>(Stream stream, scoped ImageSpan<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>
		{
			using DataReader reader = new DataReader(stream, true);

			Span<byte> signature = stackalloc byte[4];
			reader.Read(signature);

			if (!signature.SequenceEqual(EXR_SIGNATURE))
				throw new Exception("Invalid png signature");

			ExrVersion version = ReadVersion(reader);

			Memory<ExrPart> parts;

			if (!version.isMultiPart)
			{
				if (version.isSinglePart)
					throw new Exception();

				parts = new ExrPart[1];
				parts.Span[0].header = ExrHelpers.ReadHeader(reader, version.hasLongNames);

				var linesPerBlock = ExrHelpers.GetScanLinesPerBlock(parts.Span[0].header.compression);
				var blockCount = (parts.Span[0].header.dataWindow.xMax - parts.Span[0].header.dataWindow.xMin) / linesPerBlock;

				parts.Span[0].offsets = ExrHelpers.ReadOffsetTables(reader, blockCount);
			}
			else
			{
				using var headerMem = MemoryPool<ExrHeader>.Shared.Rent(1024);
				SpanList<ExrHeader> headers = headerMem.Memory.Span;

				while (true)
				{
					ExrHeader header = ExrHelpers.ReadHeader(reader, version.hasLongNames);

					if (header.isEmpty)
						break;

					headers.Add(header);
				}

				parts = new ExrPart[headers.Count];
				for (int i = 0; i < headers.Count; i++)
				{
					parts.Span[i] = new()
					{
						header = headers[i],
						offsets = ExrHelpers.ReadOffsetTables(reader, headers[i].chunkCount)
					};
				}
			}

			/*
			Console.WriteLine(parts.Span[0].header.compression);
			Console.WriteLine(parts.Span[0].header.channels.Span[0].pixelType);
			Console.WriteLine(parts.Span[0].header.type);
			Console.WriteLine(reader.Position);
			Console.WriteLine((long)parts.Span[0].offsets.Span[0]);
			*/

			reader.Seek((long)parts.Span[0].offsets.Span[0]);

			if (version.isMultiPart)
			{
				ulong partNumber = reader.ReadUInt32(); // Seems to have swapped endian for some reason.
				ulong partNumber2 = reader.ReadUInt32();
				Console.WriteLine($"Part number: {partNumber}");
				Console.WriteLine($"Part number2: {partNumber2}");
			}

			int scanline = reader.ReadInt32();
			int dataSize = reader.ReadInt32();

			Console.WriteLine($"startY: {scanline}");
			Console.WriteLine($"data size: {dataSize}");

			using var buffIn = MemoryPool<byte>.Shared.Rent(dataSize);
			using var buffOut = MemoryPool<byte>.Shared.Rent(dataSize * 100);

			var spanIn = buffIn.Memory.Span.Slice(0, dataSize);
			var spanOut = buffOut.Memory.Span;

			reader.Read(spanIn);
			PIZ.Decompress(spanIn, spanOut);

			//SpanDataReader spanReader = new(buffIn.Memory.Span.Slice(0, dataSize), true);
		}

		public void Encode<TPixel>(Stream stream, ImageSpan<TPixel> image, ref readonly ExrConfig config) where TPixel : unmanaged, IPixel<TPixel>
		{
			throw new NotImplementedException();
		}

		static ExrVersion ReadVersion(DataReader reader)
		{
			byte version1 = reader.ReadByte();

			byte version2 = reader.ReadByte();
			bool isSinglePart = ((version2 & (1 << 1)) >> 1) == 1;
			bool hasLongNames = ((version2 & (1 << 2)) >> 2) == 1;
			bool hasDeepData = ((version2 & (1 << 3)) >> 3) == 1;
			bool isMultiPart = ((version2 & (1 << 4)) >> 4) == 1;

			byte version3 = reader.ReadByte();
			byte version4 = reader.ReadByte();

			return new(version1, isSinglePart, hasLongNames, hasDeepData, isMultiPart);
		}
	}

	static class ExrHelpers
	{
		public static int GetScanLinesPerBlock(ExrCompression compression)
		{
			switch (compression)
			{
				default:
					return 1;
				case ExrCompression.Zip:
				case ExrCompression.Pxr24:
					return 16;
				case ExrCompression.Piz:
				case ExrCompression.B44:
				case ExrCompression.B44a:
				case ExrCompression.Dwaa:
					return 32;
				case ExrCompression.Dwab:
					return 256;
			}
		}

		public static int ReadString(DataReader reader, scoped SpanList<byte> buff)
		{
			byte c = reader.ReadByte();
			while (c != 0x00)
			{
				buff.Add(c);
				c = reader.ReadByte();
			}

			return buff.Count;
		}

		public static int ReadString(DataReader reader, scoped SpanList<char> buff)
		{
			char c = reader.ReadChar();
			while (c != 0x00)
			{
				buff.Add(c);
				c = reader.ReadChar();
			}

			return buff.Count;
		}

		public static string ReadInfiniteString(DataReader reader)
		{
			int strLength = reader.ReadInt32();
			if (strLength == 0)
				return string.Empty;

			using var mem = MemoryPool<char>.Shared.Rent(strLength);

			reader.Read(mem.Memory.Span.Slice(0, strLength));

			return mem.Memory.Span.Slice(0, strLength).ToString();
		}

		public static ExrHeader ReadHeader(DataReader reader, bool longNames)
		{
			Span<char> strBuff = stackalloc char[longNames ? 255 : 31];

			ExrHeader header = new();
			header.isEmpty = true;

			while (true)
			{
				int strLength = ExrHelpers.ReadString(reader, strBuff);
				if (strLength == 0)
					break;

				header.isEmpty = false;

				switch (strBuff)
				{
					case var _ when strBuff.Slice(0, strLength).SequenceEqual("channels"):
						strLength = ExrHelpers.ReadString(reader, strBuff);

						if (!strBuff.Slice(0, strLength).SequenceEqual("chlist"))
							throw new Exception();

						reader.ReadInt32(); // Attribute size
						header.channels = ExrHelpers.ReadChannels(reader);
						break;
					case var _ when strBuff.Slice(0, strLength).SequenceEqual("chunkCount"):
						strLength = ExrHelpers.ReadString(reader, strBuff);

						if (!strBuff.Slice(0, strLength).SequenceEqual("int"))
							throw new Exception();

						reader.ReadInt32(); // Attribute size
						header.chunkCount = reader.ReadInt32();
						break;
					case var _ when strBuff.Slice(0, strLength).SequenceEqual("compression"):
						strLength = ExrHelpers.ReadString(reader, strBuff);

						if (!strBuff.Slice(0, strLength).SequenceEqual("compression"))
							throw new Exception();

						reader.ReadInt32(); // Attribute size
						header.compression = (ExrCompression)reader.ReadByte();
						break;
					case var _ when strBuff.Slice(0, strLength).SequenceEqual("dataWindow"):
						strLength = ExrHelpers.ReadString(reader, strBuff);

						if (!strBuff.Slice(0, strLength).SequenceEqual("box2i"))
							throw new Exception();

						reader.ReadInt32(); // Attribute size
						header.dataWindow = ExrHelpers.ReadBox2I(reader);
						break;
					case var _ when strBuff.Slice(0, strLength).SequenceEqual("displayWindow"):
						strLength = ExrHelpers.ReadString(reader, strBuff);

						if (!strBuff.Slice(0, strLength).SequenceEqual("box2i"))
							throw new Exception();

						reader.ReadInt32(); // Attribute size
						header.dataWindow = ExrHelpers.ReadBox2I(reader);
						break;
					case var _ when strBuff.Slice(0, strLength).SequenceEqual("lineOrder"):
						strLength = ExrHelpers.ReadString(reader, strBuff);

						if (!strBuff.Slice(0, strLength).SequenceEqual("lineOrder"))
							throw new Exception();

						reader.ReadInt32(); // Attribute size
						header.lineOrder = (ExrLineOrder)reader.ReadByte();
						break;
					case var _ when strBuff.Slice(0, strLength).SequenceEqual("name"):
						strLength = ExrHelpers.ReadString(reader, strBuff);

						if (!strBuff.Slice(0, strLength).SequenceEqual("string"))
							throw new Exception();

						header.name = ExrHelpers.ReadInfiniteString(reader);
						break;
					case var _ when strBuff.Slice(0, strLength).SequenceEqual("pixelAspectRatio"):
						strLength = ExrHelpers.ReadString(reader, strBuff);

						if (!strBuff.Slice(0, strLength).SequenceEqual("float"))
							throw new Exception();

						reader.ReadInt32(); // Attribute size
						header.pixelAspectRatio = reader.ReadFloat();
						break;
					case var _ when strBuff.Slice(0, strLength).SequenceEqual("screenWindowCenter"):
						strLength = ExrHelpers.ReadString(reader, strBuff);

						if (!strBuff.Slice(0, strLength).SequenceEqual("v2f"))
							throw new Exception();

						reader.ReadInt32(); // Attribute size
						header.screenWindowCenter.x = reader.ReadFloat();
						header.screenWindowCenter.y = reader.ReadFloat();
						break;
					case var _ when strBuff.Slice(0, strLength).SequenceEqual("screenWindowWidth"):
						strLength = ExrHelpers.ReadString(reader, strBuff);

						if (!strBuff.Slice(0, strLength).SequenceEqual("float"))
							throw new Exception();

						reader.ReadInt32(); // Attribute size
						header.screenWindowWidth = reader.ReadFloat();
						break;
					case var _ when strBuff.Slice(0, strLength).SequenceEqual("type"):
						strLength = ExrHelpers.ReadString(reader, strBuff);

						if (!strBuff.Slice(0, strLength).SequenceEqual("string"))
							throw new Exception();

						header.type = ExrHelpers.ReadInfiniteString(reader);
						break;
					case var _ when strBuff.Slice(0, strLength).SequenceEqual("view"):
						strLength = ExrHelpers.ReadString(reader, strBuff);

						if (!strBuff.Slice(0, strLength).SequenceEqual("string"))
							throw new Exception();

						header.view = ExrHelpers.ReadInfiniteString(reader);
						break;
					default:
						throw new Exception();
				}
			};

			return header;
		}

		public static Memory<ExrChannel> ReadChannels(DataReader reader)
		{
			Span<byte> strBuff = stackalloc byte[255];

			using var buff = MemoryPool<ExrChannel>.Shared.Rent(64);
			SpanList<ExrChannel> channelList = buff.Memory.Span;

			while (true)
			{
				int strLength = ReadString(reader, strBuff);
				if (strLength == 0) break;

				ExrChannel channel = new();
				channel.name = Encoding.ASCII.GetString(strBuff.Slice(0, strLength));
				channel.pixelType = (ExrPixelType)reader.ReadInt32();
				channel.linear = reader.ReadByte() == 1;

				if (reader.ReadByte() != 0)
					throw new Exception();

				if (reader.ReadByte() != 0)
					throw new Exception();

				if (reader.ReadByte() != 0)
					throw new Exception();

				channel.xSampeling = reader.ReadInt32();
				channel.ySampeling = reader.ReadInt32();

				channelList.Add(channel);
			}

			Memory<ExrChannel> channels = new ExrChannel[channelList.Count];
			channelList.AsSpan().TryCopyTo(channels.Span);

			return channels;
		}

		public static Memory<ulong> ReadOffsetTables(DataReader dataReader, int count)
		{
			Memory<ulong> offsets = new ulong[count];

			for (int i = 0; i < count; i++)
			{
				offsets.Span[i] = dataReader.ReadUInt64();
			}

			return offsets;
		}

		public static ExrBoxI ReadBox2I(DataReader reader)
		{
			return new ExrBoxI()
			{
				xMin = reader.ReadInt32(),
				yMin = reader.ReadInt32(),
				xMax = reader.ReadInt32(),
				yMax = reader.ReadInt32()
			};
		}

		public static ExrBoxF ReadBox2F(DataReader reader)
		{
			return new ExrBoxF()
			{
				xMin = reader.ReadFloat(),
				yMin = reader.ReadFloat(),
				xMax = reader.ReadFloat(),
				yMax = reader.ReadFloat()
			};
		}
	}
}
