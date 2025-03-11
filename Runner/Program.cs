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
using MathLib;
using System.Runtime.Intrinsics;
using System.Runtime.CompilerServices;
using ImageLib.Jpg;

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
			//var img = Image.Load<Rgb24>("Images/Png/PNG_test.png");
			//var img = Image.Load<Rgb48>("Images/Png/Big.png");
			var img = Image.Load<Rgba64>("Images/Png/cubemap.png");
			//var img = Image.Load<Rgba32>("Images/Exr/AllHalfValues.exr");
			//var img = Image.Load<Rgba32, JpgConfig>("Images/Jpg/Known.jpg");
			//var img = Image.Load<Rgba32, JpgConfig>("Images/Jpg/Test.jpeg");
			//var img = Image.Load<Rgba32, JpgConfig>("Images/Jpg/Profile.jpg");
			//var img = Image.Load<Rgba32>("Images/Exr/Sample.exr");
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

			//Console.WriteLine($"Image: {img[70, 250]}");

			/*
			for (int x = 0; x < img.Width; x++)
			{
				for (int y = 0; y < img.Height; y++)
				{
					img[x, y].a = 255;
				}
			}
			*/

			var pix = img.Span[512, 0];
			Rgba32 pix32 = new((byte)(pix.r / 255), (byte)(pix.g / 255), (byte)(pix.b / 255), (byte)(pix.a / 255));

			Console.WriteLine(pix);
			Console.WriteLine(pix32);
			Console.WriteLine(img.Span[509, 0]);

			Image.Save("Test.png", img.Span);
			Console.WriteLine("Decoded");

			/*
			img.Span.FlipVertical();

			var section = img.Span.Slice(100, 100, 500, 500);
			var copy = img.Span.Slice(100, 100, 1000, 2000);
			copy.Fill(new Rgb24(byte.MaxValue, 0, 0));
			section.Fill(copy);
			//section.Fill(new Rgb24(byte.MaxValue, 0, 0));
			img.Span.FlipVertical();
			img.Span.FlipHorizontal();
			*/

			//var mipmap = Image.CreateEmpty<Rgb24>(img.Width + img.Width / 2, img.Height);
			//mipmap.Span.MipMap(img.Span);

			//using var tmpImgData = MemoryPool<byte>.Shared.Rent(pngData.Length);

			Console.WriteLine("Written");

			//var img = Image.Load<Rgb32>("Test.png");
			//Image.Save("Test.png", mipmap.Span, PngConfig.FromPixel<Rgb24>());
			//Image.Save(tmpImgData.Memory.Span, img.Span, PngConfig.FromPixel<Rgba32>());

			Console.WriteLine("Saved");

			/*
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

		static void ProcessPaethScanline(scoped Span<byte> scanlineCurr, scoped Span<byte> scanlinePrev)
		{
			Vector64<byte> a = Vector64<byte>.Zero;
			Vector64<byte> b = Vector64<byte>.Zero;
			Vector64<byte> c = Vector64<byte>.Zero;

			Vector64<short> ass = Vector64<short>.Zero;
			Vector64<short> bs = Vector64<short>.Zero;
			Vector64<short> cs = Vector64<short>.Zero;

			Vector64<short> p = Vector64<short>.Zero;

			Vector64<short> pa = Vector64<short>.Zero;
			Vector64<short> pb = Vector64<short>.Zero;
			Vector64<short> pc = Vector64<short>.Zero;

			for (int i = 4; i < scanlineCurr.Length; i += 4)
			{
				/*
				a = Vector64.LoadUnsafe(ref scanlineCurr[i - 4]);
				b = Vector64.LoadUnsafe(ref scanlinePrev[i]);
				c = Vector64.LoadUnsafe(ref scanlinePrev[i - 4]);

				ass = Vector64.Create(a[0], a[1], a[2], a[3]);
				bs = Vector64.Create(b[0], b[1], b[2], b[3]);
				cs = Vector64.Create(c[0], c[1], c[2], c[3]);

				p = Vector64.Subtract(Vector64.Add(ass, bs), cs);

				pa = Vector64.Abs(Vector64.Subtract(p, ass));
				pb = Vector64.Abs(Vector64.Subtract(p, bs));
				pc = Vector64.Abs(Vector64.Subtract(p, cs));
				*/

				ass = Vector64.Create(scanlineCurr[i - 4], scanlineCurr[i - 4 + 1], scanlineCurr[i - 4 + 2], scanlineCurr[i - 4 + 3]);
				bs = Vector64.Create(scanlinePrev[i], scanlinePrev[i + 1], scanlinePrev[i + 2], scanlinePrev[i + 3]);
				cs = Vector64.Create(scanlinePrev[i - 4], scanlinePrev[i - 4 + 1], scanlinePrev[i - 4 + 2], scanlinePrev[i - 4 + 3]);

				pa = Vector64.Abs(bs - cs);
				pb = Vector64.Abs(ass - cs);
				pc = Vector64.Abs(ass + bs - (2 * cs));

				//Vector64<short> result = Vector64.ConditionalSelect(Vector64.BitwiseAnd(Vector64.LessThanOrEqual(pa, pb), Vector64.LessThanOrEqual(pa, pc)), ass, Vector64.ConditionalSelect(Vector64.LessThanOrEqual(pb, pc), bs, cs));
				Vector64<short> result = Vector64.ConditionalSelect(Vector64.BitwiseAnd(Vector64.LessThanOrEqual(pa, pb), Vector64.LessThanOrEqual(pa, pc)), ass, Vector64.ConditionalSelect(Vector64.LessThanOrEqual(pb, pc), bs, cs));

				scanlineCurr[i] = (byte)result[0];
				scanlineCurr[i + 1] = (byte)result[1];
				scanlineCurr[i + 2] = (byte)result[2];
				scanlineCurr[i + 3] = (byte)result[3];
			}
		}

		public static byte PaethPredictor(byte a, byte b, byte c)
		{
			int p = a + b - c;
			int pa = int.Abs(p - a);
			int pb = int.Abs(p - b);
			int pc = int.Abs(p - c);

			if (pa <= pb && pa <= pc)
			{
				Console.WriteLine("a");
				return a;
			}

			if (pb <= pc)
			{
				Console.WriteLine("b");
				return b;
			}

			Console.WriteLine("c");
			return c;
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

		//[Benchmark]
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
