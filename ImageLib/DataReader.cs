using Engine.Utils;
using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ImageLib
{
	internal class DataReader : IDisposable
	{
		public long Position => reader.BaseStream.Position;

		BinaryReader reader;
		bool invertEndianess;

		public DataReader(Stream stream, bool isLittleEndian = false)
		{
			reader = new BinaryReader(stream);
			this.invertEndianess = isLittleEndian != BitConverter.IsLittleEndian; // If system and data endianess is mismatched invert endianess
		}

		public void Dispose()
		{
			reader.Dispose();
		}

		public ulong ReadUInt64()
			=> invertEndianess ? BinaryPrimitives.ReverseEndianness(reader.ReadUInt64()) : reader.ReadUInt64();

		public long ReadInt64()
			=> invertEndianess ? BinaryPrimitives.ReverseEndianness(reader.ReadInt64()) : reader.ReadInt64();

		public uint ReadUInt32()
			=> invertEndianess ? BinaryPrimitives.ReverseEndianness(reader.ReadUInt32()) : reader.ReadUInt32();

		public int ReadInt32()
			=> invertEndianess ? BinaryPrimitives.ReverseEndianness(reader.ReadInt32()) : reader.ReadInt32();

		public ushort ReadUInt16()
			=> invertEndianess ? BinaryPrimitives.ReverseEndianness(reader.ReadUInt16()) : reader.ReadUInt16();

		public short ReadInt16()
			=> invertEndianess ? BinaryPrimitives.ReverseEndianness(reader.ReadInt16()) : reader.ReadInt16();

		public Half ReadHalf()
			=> reader.ReadHalf();

		public float ReadFloat()
			=> reader.ReadSingle();

		public double ReadDouble()
			=> reader.ReadDouble();

		public byte ReadByte()
			=> reader.ReadByte();

		public char ReadChar()
			=> reader.ReadChar();

		public sbyte ReadSByte()
			=> reader.ReadSByte();

		public int Read(scoped Span<byte> buffer)
			=> reader.Read(buffer);

		public int Read(scoped Span<char> buffer)
			=> reader.Read(buffer);

		public int ReadUntill(scoped SpanList<byte> buffer, byte endByte)
		{
			byte c;
			int read = 0;

			do
			{
				c = reader.ReadByte();
				buffer.Add(c);
				read++;

			} while (c != endByte && !buffer.IsFull);

			return read;
		}

		public void Seek(long position)
		{
			reader.BaseStream.Position = position;
		}
	}

	public ref struct SpanReader
	{
		public int Remaining => data.Length - idx;

		ReadOnlySpan<byte> data;
		int idx = 0;
		bool invertEndianess;

		public SpanReader(ReadOnlySpan<byte> data, bool isLittleEndian = false)
		{
			this.data = data;
			this.invertEndianess = isLittleEndian != BitConverter.IsLittleEndian; // If system and data endianess is mismatched invert endianess
		}

		public ulong ReadUInt64()
			=> invertEndianess ? BinaryPrimitives.ReverseEndianness(Read<ulong>()) : Read<ulong>();

		public long ReadInt64()
			=> invertEndianess ? BinaryPrimitives.ReverseEndianness(Read<long>()) : Read<long>();

		public uint ReadUInt32()
			=> invertEndianess ? BinaryPrimitives.ReverseEndianness(Read<uint>()) : Read<uint>();

		public int ReadInt32()
			=> invertEndianess ? BinaryPrimitives.ReverseEndianness(Read<int>()) : Read<int>();

		public ushort ReadUInt16()
			=> invertEndianess ? BinaryPrimitives.ReverseEndianness(Read<ushort>()) : Read<ushort>();

		public short ReadInt16()
			=> invertEndianess ? BinaryPrimitives.ReverseEndianness(Read<short>()) : Read<short>();

		public Half ReadHalf()
			=> Read<Half>();

		public float ReadFloat()
			=> Read<float>();

		public double ReadDouble()
			=> Read<double>();

		public byte ReadByte()
			=> Read<byte>();

		public char ReadChar()
			=> Read<char>();

		public sbyte ReadSByte()
			=> Read<sbyte>();

		public int Read(scoped Span<byte> buffer)
		{
			if (buffer.Length > data.Length - idx)
			{
				data.Slice(idx, data.Length - idx).TryCopyTo(buffer);
				idx += data.Length - idx;
				return data.Length - idx;
			}
			else
			{
				data.Slice(idx, buffer.Length).TryCopyTo(buffer);
				idx += buffer.Length;
				return buffer.Length;
			}
		}

		unsafe T Read<T>() where T : unmanaged
		{
			idx += sizeof(T);
			return MemoryMarshal.AsRef<T>(data.Slice(idx - sizeof(T), sizeof(T)));
		}

		public static implicit operator SpanReader(ReadOnlySpan<byte> span) => new(span);
	}
}
