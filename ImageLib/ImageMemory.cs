using ImageLib.Exr;
using ImageLib.Hdr;
using ImageLib.Jpg;
using ImageLib.Jxl;
using ImageLib.Png;
using MathLib;
using System.IO;
using static System.Net.Mime.MediaTypeNames;

namespace ImageLib
{
	static class FormatStorage
	{
		public static void AddFormat<TConfig>(IFormatHandler<TConfig> formatHandler) where TConfig : struct, IFormatConfig<TConfig>
		{
			FormatStorage<TConfig>.formatHandler = formatHandler;
		}

		// TODO: Replace with source generator, extensions should be attributes
		public static ImageMetadata GetMetadata<TPixel>(scoped ReadOnlySpan<char> ext, Stream stream) where TPixel : unmanaged, IPixel<TPixel>
		{
			switch (ext)
			{
				case ".png":
					return FormatStorage<PngConfig>.formatHandler.GetMetadata(stream);
				case ".hdr":
					return FormatStorage<HdrConfig>.formatHandler.GetMetadata(stream);
				case ".exr":
					return FormatStorage<ExrConfig>.formatHandler.GetMetadata(stream);
				case ".jpeg":
				case ".jpg":
					return FormatStorage<JpgConfig>.formatHandler.GetMetadata(stream);
				case ".jxl":
					return FormatStorage<JxlConfig>.formatHandler.GetMetadata(stream);
				default:
					throw new Exception("Image format not supported");
			}
		}

		public static void Decode<TPixel>(scoped ReadOnlySpan<char> ext, Stream stream, scoped ImageSpan<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>
		{
			switch (ext)
			{
				case ".png":
					FormatStorage<PngConfig>.formatHandler.Decode(stream, image);
					break;
				case ".hdr":
					FormatStorage<JpgConfig>.formatHandler.Decode(stream, image);
					break;
				case ".exr":
					FormatStorage<ExrConfig>.formatHandler.Decode(stream, image);
					break;
				case ".jpeg":
				case ".jpg":
					FormatStorage<JpgConfig>.formatHandler.Decode(stream, image);
					break;
				case ".jxl":
					FormatStorage<JxlConfig>.formatHandler.Decode(stream, image);
					break;
				default:
					throw new Exception("Image format not supported");
			}
		}

		public static void Encode<TPixel>(scoped ReadOnlySpan<char> ext, scoped ImageSpan<TPixel> image, Stream stream) where TPixel : unmanaged, IPixel<TPixel>
		{
			switch (ext)
			{
				case ".png":
					FormatStorage<PngConfig>.formatHandler.Encode(stream, image, PngConfig.FromPixel<TPixel>());
					break;
				case ".exr":
					FormatStorage<ExrConfig>.formatHandler.Encode(stream, image, ExrConfig.FromPixel<TPixel>());
					break;
				case ".jpeg":
				case ".jpg":
					FormatStorage<JpgConfig>.formatHandler.Encode(stream, image, JpgConfig.FromPixel<TPixel>());
					break;
				case ".jxl":
					FormatStorage<JxlConfig>.formatHandler.Encode(stream, image, JxlConfig.FromPixel<TPixel>());
					break;
				default:
					throw new Exception("Image format not supported");
			}
		}
	}

	static class FormatStorage<TConfig> where TConfig : struct, IFormatConfig<TConfig>
	{
		public static IFormatHandler<TConfig> formatHandler;
	}

	public static class Image
	{
		static Image()
		{
			FormatStorage.AddFormat(PngFormat.Instance);
			FormatStorage.AddFormat(HdrFormat.Instance);
			FormatStorage.AddFormat(ExrFormat.Instance);
			FormatStorage.AddFormat(JpgFormat.Instance);
			FormatStorage.AddFormat(JxlFormat.Instance);
		}

		// TODO: The way metadata is gathered from files is a bit akward, can probably be done better.
		public static ImageMemory<TPixel> Load<TPixel>(scoped ReadOnlySpan<char> path) where TPixel : unmanaged, IPixel<TPixel>
		{
			using FileStream fs = new FileStream(path.ToString(), FileMode.Open, FileAccess.Read);
			var ext = path.Slice(path.LastIndexOf('.'));

			var metadata = FormatStorage.GetMetadata<TPixel>(ext, fs);
			var image = CreateEmpty<TPixel>(metadata.Width, metadata.Height);
			Load<TPixel>(path, image);

			return image;
		}

		public static void Load<TPixel>(scoped ReadOnlySpan<char> path, scoped ImageSpan<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>
		{
			using FileStream fs = new FileStream(path.ToString(), FileMode.Open, FileAccess.Read);
			var ext = path.Slice(path.LastIndexOf('.'));

			FormatStorage.Decode(ext, fs, image);
		}

		public static ImageMemory<TPixel> Load<TPixel, TConfig>(scoped ReadOnlySpan<char> path) where TPixel : unmanaged, IPixel<TPixel> where TConfig : struct, IFormatConfig<TConfig>
		{
			using FileStream fs = new FileStream(path.ToString(), FileMode.Open, FileAccess.Read);

			var metadata = FormatStorage<TConfig>.formatHandler.GetMetadata(fs);
			var image = CreateEmpty<TPixel>(metadata.Width, metadata.Height);
			Load<TPixel, TConfig>(path, image);

			return image;
		}

		public static void Load<TPixel, TConfig>(scoped ReadOnlySpan<char> path, scoped ImageSpan<TPixel> image) where TPixel : unmanaged, IPixel<TPixel> where TConfig : struct, IFormatConfig<TConfig>
		{
			using FileStream fs = new FileStream(path.ToString(), FileMode.Open, FileAccess.Read);

			FormatStorage<TConfig>.formatHandler.Decode(fs, image);
		}
		
		public unsafe static ImageMemory<TPixel> Load<TPixel, TConfig>(scoped ReadOnlySpan<byte> data) where TPixel : unmanaged, IPixel<TPixel> where TConfig : struct, IFormatConfig<TConfig>
		{
			fixed (byte* dataPtr = data)
			{
				UnmanagedMemoryStream ms = new UnmanagedMemoryStream(dataPtr, data.Length);

				var metadata = FormatStorage<TConfig>.formatHandler.GetMetadata(ms);
				var image = CreateEmpty<TPixel>(metadata.Width, metadata.Height);
				Load<TPixel, TConfig>(data, image);

				return image;
			}
		}
		
		public unsafe static void Load<TPixel, TConfig>(scoped ReadOnlySpan<byte> data, scoped ImageSpan<TPixel> image) where TPixel : unmanaged, IPixel<TPixel> where TConfig : struct, IFormatConfig<TConfig>
		{
			fixed (byte* dataPtr = data)
			{
				UnmanagedMemoryStream ms = new UnmanagedMemoryStream(dataPtr, data.Length);
				FormatStorage<TConfig>.formatHandler.Decode(ms, image);
			}
		}

		public static void Save<TPixel>(scoped ReadOnlySpan<char> path, scoped ImageSpan<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>
		{
			using FileStream fs = new FileStream(path.ToString(), FileMode.Create, FileAccess.Write);
			var ext = path.Slice(path.LastIndexOf('.'));

			FormatStorage.Encode(ext, image, fs);
		}

		public static void Save<TPixel, TConfig>(scoped ReadOnlySpan<char> path, scoped ImageSpan<TPixel> image, TConfig config) where TPixel : unmanaged, IPixel<TPixel> where TConfig : struct, IFormatConfig<TConfig>
		{
			using FileStream fs = new FileStream(path.ToString(), FileMode.Create, FileAccess.Write);

			FormatStorage<TConfig>.formatHandler.Encode(fs, image, in config);
		}

		public unsafe static void Save<TPixel, TConfig>(scoped Span<byte> output, scoped ImageSpan<TPixel> image, TConfig config) where TPixel : unmanaged, IPixel<TPixel> where TConfig : struct, IFormatConfig<TConfig>
		{
			fixed (byte* outputPtr = output)
			{
				UnmanagedMemoryStream ms = new UnmanagedMemoryStream(outputPtr, output.Length, output.Length, FileAccess.Write);
				FormatStorage<TConfig>.formatHandler.Encode(ms, image, in config);
			}
		}

		public static ImageMemory<TPixel> CreateEmpty<TPixel>(int width, int height) where TPixel : unmanaged, IPixel<TPixel>
		{
			return new ImageMemory<TPixel>(new TPixel[width * height], width, height);
		}
	}

	public struct ImageMemory<TPixel> where TPixel : unmanaged, IPixel<TPixel>
	{
		public static readonly ImageMemory<TPixel> Empty = new(Memory<TPixel>.Empty, 0, 0);

		public int Width { get; }

		public int Height { get; }

		public ref TPixel this[int x, int y]
		{
			get
			{
				return ref data.Span[x + y * Width];
			}
		}

		public ImageSpan<TPixel> Span => new(Width, Height, data.Span);

		Memory<TPixel> data;

		internal ImageMemory(Memory<TPixel> data, int width, int height)
		{
			this.data = data;
			this.Width = width;
			this.Height = height;
		}

		public static implicit operator ImageSpan<TPixel>(ImageMemory<TPixel> i) => i.Span;
	}
}
