using Engine.Utils;
using ImageLib.Hdr;
using MathLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageLib.Exr
{
	struct ExrVersion
	{
		public byte version;

		public bool isSinglePart;
		public bool hasLongNames;
		public bool hasDeepData;
		public bool isMultiPart;

		public ExrVersion(byte version, bool isSinglePart, bool hasLongNames, bool hasDeepData, bool isMultiPart)
		{
			this.version = version;
			this.isSinglePart = isSinglePart;
			this.hasLongNames = hasLongNames;
			this.hasDeepData = hasDeepData;
			this.isMultiPart = isMultiPart;
		}
	}

	struct ExrHeader
	{

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
			throw new NotImplementedException();
		}

		public void Decode<TPixel>(Stream stream, scoped ImageSpan<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>
		{
			using DataReader reader = new DataReader(stream, true);

			Span<byte> signature = stackalloc byte[4];
			reader.Read(signature);

			if (!signature.SequenceEqual(EXR_SIGNATURE))
				throw new Exception("Invalid png signature");

			ExrVersion version = ReadVersion(reader);

			Console.WriteLine(version.isSinglePart);
			Console.WriteLine(version.hasLongNames);
			Console.WriteLine(version.hasDeepData);
			Console.WriteLine(version.isMultiPart);

			ReadHeader(reader);
		}

		public void Encode<TPixel>(Stream stream, ImageSpan<TPixel> image, ref readonly ExrConfig config) where TPixel : unmanaged, IPixel<TPixel>
		{
			throw new NotImplementedException();
		}

		static ExrVersion ReadVersion(DataReader reader)
		{
			byte version1 = reader.ReadByte();

			bool isSinglePart;
			bool hasLongNames;
			bool hasDeepData;
			bool isMultiPart;

			byte version2 = reader.ReadByte();
			isSinglePart = (version2 & 1) == 1;
			hasLongNames = (version2 & (1 << 1)) == 1;
			hasDeepData = (version2 & (1 << 2)) == 1;
			isMultiPart = (version2 & (1 << 3)) == 1;

			byte version3 = reader.ReadByte();
			byte version4 = reader.ReadByte();

			return new(version1, isSinglePart, hasLongNames, hasDeepData, isMultiPart);
		}

		static ExrHeader ReadHeader(DataReader reader)
		{
			Span<byte> strBuff = stackalloc byte[31];
			int strLength = ExrHelpers.ReadString(reader, strBuff);

			Console.WriteLine($"{strLength}: {Encoding.ASCII.GetString(strBuff.Slice(0, strLength))}");

			strLength = ExrHelpers.ReadString(reader, strBuff);

			Console.WriteLine($"{strLength}: {Encoding.ASCII.GetString(strBuff.Slice(0, strLength))}");

			var size = reader.ReadInt32();
			Console.WriteLine(size);

			while (true)
			{
				strLength = ExrHelpers.ReadString(reader, strBuff);
				if (strLength == 0) break;

				Console.WriteLine($"{strLength}: {Encoding.ASCII.GetString(strBuff.Slice(0, strLength))}");

				var pixelType = reader.ReadInt32();
				Console.WriteLine($"Pixel Type: {pixelType}");

				var linear = reader.ReadByte();
				Console.WriteLine($"Linear: {linear}");

				var val2 = reader.ReadByte();
				Console.WriteLine(val2);

				var p1 = reader.ReadByte();
				Console.WriteLine(p1);

				var p2 = reader.ReadByte();
				Console.WriteLine(p2);

				var xSampeling = reader.ReadInt32();
				Console.WriteLine($"X sampeling: {xSampeling}");

				var ySampeling = reader.ReadInt32();
				Console.WriteLine($"Y sampeling: {ySampeling}");
			}

			strLength = ExrHelpers.ReadString(reader, strBuff);

			Console.WriteLine($"{strLength}: {Encoding.ASCII.GetString(strBuff.Slice(0, strLength))}");

			strLength = ExrHelpers.ReadString(reader, strBuff);

			Console.WriteLine($"{strLength}: {Encoding.ASCII.GetString(strBuff.Slice(0, strLength))}");

			return new();
		}
	}

	static class ExrHelpers
	{
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
	}
}
