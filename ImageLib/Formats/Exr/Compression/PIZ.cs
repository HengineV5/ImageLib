using System.Buffers;
using System.Buffers.Binary;
using ZlibNGSharpMinimal.Deflate;
using ZlibNGSharpMinimal.Inflate;

namespace ImageLib.Exr.Compression
{
	struct HufDec
	{
		public byte length;
		public int lit;
		public int p;
	}

	public static class PIZ
	{
		const int BITMAP_SIZE = 8192;

		const int HUF_ENCBITS = 16;  // literal (value) bit length
		const int HUF_DECBITS = 14;  // decoding bit size (>= 8)

		const int HUF_ENCSIZE = (1 << HUF_ENCBITS) + 1;  // encoding table size
		const int HUF_DECSIZE = 1 << HUF_DECBITS;        // decoding table size
		const int HUF_DECMASK = HUF_DECSIZE - 1;

		public static void Compress(scoped ReadOnlySpan<byte> input, scoped Span<byte> output)
		{

		}

		public static ulong Decompress(scoped ReadOnlySpan<byte> input, scoped Span<byte> output)
		{
			if (input.Length < 4)
				throw new Exception();

			SpanReader reader = new(input, true);

			Console.WriteLine($"Rem: {reader.Remaining}");
			ushort minNonZero = reader.ReadUInt16();
			ushort maxNonZero = reader.ReadUInt16();

			Console.WriteLine($"Rem: {reader.Remaining}");
			Console.WriteLine($"MinNonZero: {minNonZero}");
			Console.WriteLine($"MaxNonZero: {maxNonZero}");
			Console.WriteLine($"Eq: {maxNonZero - minNonZero + 1}");
			int delta = maxNonZero - minNonZero + 1;

			if (maxNonZero >= BITMAP_SIZE)
				throw new Exception();

			using var bitmap = MemoryPool<byte>.Shared.Rent(BITMAP_SIZE);
			if (delta > 0)
			{
				if (maxNonZero - minNonZero + 1 > reader.Remaining)
					throw new Exception();

				reader.Read(bitmap.Memory.Span.Slice(minNonZero, maxNonZero - minNonZero + 1));
			}
			else
			{
				if (minNonZero != BITMAP_SIZE - 1 || maxNonZero == 0)
					throw new Exception();

				// All pixels are zero.
			}

			using var lut = MemoryPool<ushort>.Shared.Rent(ushort.MaxValue + 1);
			ushort maxValue = ReverseLutFromBitmap(bitmap.Memory.Span, lut.Memory.Span.Slice(0, BITMAP_SIZE));
			Console.WriteLine($"Max Value: {maxValue}");

			int length = reader.ReadInt32();
			Console.WriteLine($"Length: {length}");

			using var dataBuff = MemoryPool<byte>.Shared.Rent(length * 100);
			HuffmanUncompress(reader, length, dataBuff.Memory.Span);

			return 0;
		}

		static ushort ReverseLutFromBitmap(scoped ReadOnlySpan<byte> bitmap, scoped Span<ushort> lut)
		{
			ushort k = 0;

			lut.Clear();
			for (int i = 0; i < ushort.MaxValue; i++)
			{
				if (i == 0 || ((bitmap[i >> 3] & (1 << (i & 7))) != 0))
					lut[k++] = (ushort)i;
			}

			return k--;
		}

		static bool HuffmanUncompress(scoped SpanReader reader, int length, scoped Span<byte> ouput)
		{
			if (length == 0)
				return false;

			uint min = reader.ReadUInt32();
			uint max = reader.ReadUInt32();
			uint tableLength = reader.ReadUInt32();
			uint bits = reader.ReadUInt32();
			uint _ = reader.ReadUInt32(); // TODO: What is this?

			if (min >= HUF_ENCSIZE || max >= HUF_ENCSIZE)
				return false;

			using var freq = MemoryPool<int>.Shared.Rent(HUF_ENCSIZE);
			using var hdec = MemoryPool<HufDec>.Shared.Rent(HUF_ENCSIZE);

			return true;
		}
	}
}
