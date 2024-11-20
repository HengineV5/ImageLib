using Engine.Utils;
using ImageLib.Png;
using MathLib;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ImageLib.Jpg
{
	public class JpgFormat : IFormatHandler<JpgConfig>
	{
		static byte[] JPG_IMAGE_START  = [0xFF, 0xD8];
		static byte[] JPG_APP_HEADER   = [0xFF, 0xE0];
		static byte[] JPG_QUANT_TABLE  = [0xFF, 0xDB];
		static byte[] JPG_FRAME_START  = [0xFF, 0xC0];
		static byte[] JPG_HUFF_TABLE   = [0xFF, 0xC4];
		static byte[] JPG_SCAN_START   = [0xFF, 0xDA];
		static byte[] JPG_IMAGE_END    = [0xFF, 0xD9];

		public static JpgFormat Instance => new();

		public ImageMetadata GetMetadata(Stream stream)
		{
			using DataReader reader = new DataReader(stream, false);

			Span<byte> buff = stackalloc byte[2];
			reader.Read(buff);

			if (!buff.SequenceEqual(JPG_IMAGE_START))
				throw new Exception("Invalid jpeg signature");

			while (true)
			{
				reader.Read(buff);

				if (buff.SequenceEqual(JPG_IMAGE_END))
					break;

				if (buff.SequenceEqual(JPG_SCAN_START))
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
					reader.Seek(reader.Position + len - 2);
				}
				else if (buff.SequenceEqual(JPG_FRAME_START))
				{
					Console.WriteLine($"Frame start: {len}");
					reader.Seek(reader.Position + len - 2);
				}
				else if (buff.SequenceEqual(JPG_HUFF_TABLE))
				{
					Console.WriteLine($"Huff table: {len}");
					JpgHelpers.DecodeHuffman(reader);
				}

				//Console.WriteLine($"0x{buff[0]:X}{buff[1]:X} header: {len}");
			}

			reader.Read(buff);

			return default;
		}

		public void Decode<TPixel>(Stream stream, scoped ImageSpan<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>
		{
			using DataReader reader = new DataReader(stream);

			Span<byte> signature = stackalloc byte[2];
			int r = reader.Read(signature);

			if (!signature.SequenceEqual(JPG_IMAGE_START))
				throw new Exception("Invalid jpeg signature");
		}

		public void Encode<TPixel>(Stream stream, ImageSpan<TPixel> image, ref readonly JpgConfig config) where TPixel : unmanaged, IPixel<TPixel>
		{
			throw new NotImplementedException();
		}

		static void ReadChunk()
		{

		}
	}

	enum HuffmanTableType
	{
		DC = 0,
		AC = 1
	}

	static class JpgHelpers
	{
		public static HuffmanTable<byte> DecodeHuffman(DataReader reader)
		{
			byte htInfo = reader.ReadByte();
			int htIdx = htInfo & 0x7; // First three bits;
			HuffmanTableType htType = (HuffmanTableType)(htInfo & 0x8); // Fourth bit;

			Console.WriteLine($"Marker: {htIdx}");
			Console.WriteLine($"Marker: {htType}");

			Span<byte> lengths = stackalloc byte[16];
			reader.Read(lengths);

			int sum = 0;
			for (int i = 0; i < lengths.Length; i++)
			{
				Console.Write($"{lengths[i]}, ");
				sum += lengths[i];
			}
			Console.WriteLine();

			Span<byte> elements = stackalloc byte[sum];
			reader.Read(elements);
			Console.WriteLine($"Length: {elements.Length}");

			//var table = new HuffmanTable(stackalloc HuffmanTable.Node[elements.Length]);
			//var table = new HuffmanTable(ArrayPool<HuffmanTable.Node>.Shared.Rent(10));
			return default;
		}
	}

	struct HuffmanTable<T>
	{
		public struct Node
		{
			public T value;
			public bool isLeaf;
			public int right;
			public int left;
		}

		Memory<Node> nodes;

		public HuffmanTable(Memory<Node> nodes)
		{
			this.nodes = nodes;
		}

		public static HuffmanTable<T> FromDHT(int totalNodes, scoped Span<byte> lengths, scoped Span<byte> elements)
		{
			Memory<Node> nodes = new Node[totalNodes];

			for (int i = 0; i < lengths.Length; i++)
			{

			}

			return default;
		}
	}
}
