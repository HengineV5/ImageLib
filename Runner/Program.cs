using ImageLib;

using Rgba32 = MathLib.Rgba<MathLib.UInt8, MathLib.Rgba_Ops_Generic<MathLib.UInt8>>;
using Rgba64 = MathLib.Rgba<MathLib.UInt16, MathLib.Rgba_Ops_Generic<MathLib.UInt16>>;
using Rgb24 = MathLib.Rgb<MathLib.UInt8, MathLib.Rgb_Ops_Generic<MathLib.UInt8>>;
using Rgb48 = MathLib.Rgb<MathLib.UInt16, MathLib.Rgb_Ops_Generic<MathLib.UInt16>>;
using ImageLib.Png;
using System.Buffers;
using System.IO.Pipelines;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Engine.Utils;
using MathLib;

namespace Runner
{
	internal class Program
	{
		static void Main(string[] args)
		{
#if RELEASE
			BenchmarkRunner.Run<ImageBenchmarks>();
#else
			//PngReader reader = new(new FileStream("Images/Test.png", FileMode.Open));
			//var pngData = File.ReadAllBytes("Images/Png/Test2.png");

			//var img = Image.Load<Rgba64>("Images/Png/Test2.png");
			//var img = Image.Load<Rgba64>("Images/Hdr/Test.hdr");
			//var img = Image.Load<Rgba64>("Images/Hdr/Big.hdr");
			var img = Image.Load<Rgb24>("Images/Png/PNG_test.png");
			//var img = Image.Load<Rgb48>("Images/Png/Big.png");
			//var img = Image.Load<Rgba32>("Images/Exr/Test.exr");
			//var img = Image.Load<Rgb24, PngConfig>(imgData);
			/*
			for (int x = 0; x < img.Width; x++)
			{
				for (int y = 0; y < img.Height; y++)
				{
					//img[x, y].a = byte.MaxValue;
					img[x, y].r *= 7;
					img[x, y].g *= 7;
					img[x, y].b *= 7;
				}
			}
			*/

			/*
			using var tmpImgData = MemoryPool<byte>.Shared.Rent(pngData.Length);

			Console.WriteLine("Decoded");

			//var img = Image.Load<Rgb32>("Test.png");
			//Image.Save("Test.png", img.Span, PngConfig.FromPixel<Rgb48>());
			//Image.Save(tmpImgData.Memory.Span, img.Span, PngConfig.FromPixel<Rgba32>());

			int pixel = 0;
			int row = 0;

			Console.WriteLine($"Pixel {pixel}:");
			Console.WriteLine($"\t{img[pixel, row]}");

			pixel++;

			Console.WriteLine($"Pixel {pixel}:");
			Console.WriteLine($"\t{img[pixel, row]}");
			
			pixel++;

			Console.WriteLine($"Pixel {pixel}:");
			Console.WriteLine($"\t{img[pixel, row]}");
			*/
#endif
		}
	}

	[SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.Net90)]
	public class ImageBenchmarks
	{
		Memory<byte> imageData;

		[GlobalSetup]
		public void Setup()
		{
			imageData = File.ReadAllBytes("C:\\Users\\henst\\source\\repos\\ImageLib\\Runner\\Images\\Png\\PNG_Test.png");
		}

		[Benchmark]
		public void ImageSharpLoad()
		{
			var img = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(imageData.Span);
		}

		[Benchmark]
		public void CustomLoad()
		{
			var img = Image.Load<Rgba32, PngConfig>(imageData.Span);
		}
	}
}
