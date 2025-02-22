using ImageLib.Png;
using ImageLib.Hdr;
using MathLib;
using System.Buffers;
using ImageLib.Jpg;

namespace ImageLib.Test
{
	public class FormatTests
	{
		Memory<byte> img_png_rgba32;
		Memory<byte> img_png_rgb24;
		Memory<byte> img_hdr;
		Memory<byte> img_jpg;

		[SetUp]
		public void Setup()
		{
			img_png_rgba32 = File.ReadAllBytes("TestImages/Png/Test_RGBA32.png");
			img_png_rgb24 = File.ReadAllBytes("TestImages/Png/Test_RGB24.png");
			img_hdr = File.ReadAllBytes("TestImages/Hdr/Test.hdr");
			img_jpg = File.ReadAllBytes("TestImages/Jpg/Test.jpg");
		}

		[Test]
		public void TestPng()
		{
			TestHelpers.TestFormat<Rgba32, PngConfig>(img_png_rgba32.Span, AssertImage);
			TestHelpers.TestFormat<Rgba64, PngConfig>(img_png_rgba32.Span, AssertImage);
			TestHelpers.TestFormat<Rgba32, Rgb24, PngConfig>(img_png_rgba32.Span, AssertImage);
			TestHelpers.TestFormat<Rgb24, Rgba32, PngConfig>(img_png_rgba32.Span, AssertImage);

			TestHelpers.TestFormat<Rgb24, PngConfig>(img_png_rgb24.Span, AssertImage);
			TestHelpers.TestFormat<Rgb24, Rgba32, PngConfig>(img_png_rgba32.Span, AssertImage);
			TestHelpers.TestFormat<Rgba32, Rgb24, PngConfig>(img_png_rgba32.Span, AssertImage);

			TestHelpers.TestFormat<Rgba64, Rgb24, PngConfig>(img_png_rgba32.Span, AssertImage);
			TestHelpers.TestFormat<Rgba64, Rgb48, PngConfig>(img_png_rgba32.Span, AssertImage);
		}

		[Test]
		public void TestHdr()
		{
			var pixel1 = new Rgba64(44287, 33791, 0, 0);
			var pixel2 = new Rgba64(16895, 12543, 35839, 0);

			TestHelpers.TestFormatLoad<Rgba64, HdrConfig>(img_hdr.Span, x => {
				AssertPixel(in x[10, 15], in pixel1);
				AssertPixel(in x[23, 25], in pixel2);
			});
		}

		[Test]
		public void TestJpg()
		{
			var pixel1 = new Rgb24(161, 113, 137);
			var pixel2 = new Rgb24(148, 97, 127);

			var margin = new Rgb24(2, 2, 2);

			TestHelpers.TestFormatLoad<Rgb24, JpgConfig>(img_jpg.Span, x => {
				AssertPixel(in x[10, 15], in pixel1, in margin);
				AssertPixel(in x[23, 25], in pixel2, in margin);
			});

			TestHelpers.TestFormatLoad<Rgb48, JpgConfig>(img_jpg.Span, x => {
				AssertPixel(in x[10, 15], in pixel1, in margin);
				AssertPixel(in x[23, 25], in pixel2, in margin);
			});
		}

		static void AssertImage(ImageMemory<Rgba32> oldImg, ImageMemory<Rgba32> newImg)
		{
			for (int y = 0; y < oldImg.Height; y++)
			{
				for (int x = 0; x < oldImg.Width; x++)
				{
					ref var oldPixel = ref oldImg[x, y];
					ref var newPixel = ref newImg[x, y];

					Assert.That(oldPixel.r == newPixel.r, Is.True);
					Assert.That(oldPixel.g == newPixel.g, Is.True);
					Assert.That(oldPixel.b == newPixel.b, Is.True);
					Assert.That(oldPixel.a == newPixel.a, Is.True);
				}
			}
		}

		static void AssertImage(ImageMemory<Rgba64> oldImg, ImageMemory<Rgba64> newImg)
		{
			for (int y = 0; y < oldImg.Height; y++)
			{
				for (int x = 0; x < oldImg.Width; x++)
				{
					ref var oldPixel = ref oldImg[x, y];
					ref var newPixel = ref newImg[x, y];

					Assert.That(oldPixel.r == newPixel.r, Is.True);
					Assert.That(oldPixel.g == newPixel.g, Is.True);
					Assert.That(oldPixel.b == newPixel.b, Is.True);
					Assert.That(oldPixel.a == newPixel.a, Is.True);
				}
			}
		}

		static void AssertImage(ImageMemory<Rgb24> oldImg, ImageMemory<Rgb24> newImg)
		{
			for (int y = 0; y < oldImg.Height; y++)
			{
				for (int x = 0; x < oldImg.Width; x++)
				{
					ref var oldPixel = ref oldImg[x, y];
					ref var newPixel = ref newImg[x, y];

					Assert.That(oldPixel.r == newPixel.r, Is.True);
					Assert.That(oldPixel.g == newPixel.g, Is.True);
					Assert.That(oldPixel.b == newPixel.b, Is.True);
				}
			}
		}

		static void AssertImage(ImageMemory<Rgb24> oldImg, ImageMemory<Rgba32> newImg)
			=> AssertImage(newImg, oldImg);

		static void AssertImage(ImageMemory<Rgba32> oldImg, ImageMemory<Rgb24> newImg)
		{
			for (int y = 0; y < oldImg.Height; y++)
			{
				for (int x = 0; x < oldImg.Width; x++)
				{
					ref var oldPixel = ref oldImg[x, y];
					ref var newPixel = ref newImg[x, y];

					Assert.That(oldPixel.r == newPixel.r, Is.True);
					Assert.That(oldPixel.g == newPixel.g, Is.True);
					Assert.That(oldPixel.b == newPixel.b, Is.True);
				}
			}
		}

		static void AssertImage(ImageMemory<Rgba64> oldImg, ImageMemory<Rgb24> newImg)
		{
			for (int y = 0; y < oldImg.Height; y++)
			{
				for (int x = 0; x < oldImg.Width; x++)
				{
					ref var oldPixel = ref oldImg[x, y];
					ref var newPixel = ref newImg[x, y];

					Assert.That(oldPixel.r == newPixel.r, Is.True);
					Assert.That(oldPixel.g == newPixel.g, Is.True);
					Assert.That(oldPixel.b == newPixel.b, Is.True);
				}
			}
		}

		static void AssertImage(ImageMemory<Rgba64> oldImg, ImageMemory<Rgb48> newImg)
		{
			for (int y = 0; y < oldImg.Height; y++)
			{
				for (int x = 0; x < oldImg.Width; x++)
				{
					ref var oldPixel = ref oldImg[x, y];
					ref var newPixel = ref newImg[x, y];

					Assert.That(oldPixel.r == newPixel.r, Is.True);
					Assert.That(oldPixel.g == newPixel.g, Is.True);
					Assert.That(oldPixel.b == newPixel.b, Is.True);
				}
			}
		}

		static void AssertPixel(ref readonly Rgba64 pixel1, ref readonly Rgba64 pixel2)
		{
			Assert.That(pixel1.r == pixel2.r, Is.True);
			Assert.That(pixel1.g == pixel2.g, Is.True);
			Assert.That(pixel1.b == pixel2.b, Is.True);
			Assert.That(pixel1.a == pixel2.a, Is.True);
		}

		static void AssertPixel(ref readonly Rgb24 pixel1, ref readonly Rgb24 pixel2, ref readonly Rgb24 margin)
		{
			Assert.That(int.Abs(pixel1.r - pixel2.r) < margin.r, Is.True);
			Assert.That(int.Abs(pixel1.g - pixel2.g) < margin.g, Is.True);
			Assert.That(int.Abs(pixel1.b - pixel2.b) < margin.b, Is.True);
		}

		static void AssertPixel(ref readonly Rgb48 pixel1, ref readonly Rgb24 pixel2, ref readonly Rgb24 margin)
		{
			Assert.That(int.Abs(pixel1.r - pixel2.r) < margin.r, Is.True);
			Assert.That(int.Abs(pixel1.g - pixel2.g) < margin.g, Is.True);
			Assert.That(int.Abs(pixel1.b - pixel2.b) < margin.b, Is.True);
		}
	}

	static class TestHelpers
	{
		public static void TestFormat<TPixel, TConfig>(scoped ReadOnlySpan<byte> imgData, Action<ImageMemory<TPixel>, ImageMemory<TPixel>> assert) where TPixel : unmanaged, IPixel<TPixel> where TConfig : struct, IFormatConfig<TConfig>
			=> TestFormat<TPixel, TPixel, TConfig>(imgData, assert);

		public static void TestFormat<TReadPixel, TWritePixel, TConfig>(scoped ReadOnlySpan<byte> imgData, Action<ImageMemory<TReadPixel>, ImageMemory<TWritePixel>> assert)
			where TReadPixel : unmanaged, IPixel<TReadPixel>
			where TWritePixel : unmanaged, IPixel<TWritePixel>
			where TConfig : struct, IFormatConfig<TConfig>
		{
			var img = Image.Load<TReadPixel, TConfig>(imgData);

			int bytePerPixel = TWritePixel.Channels * (TWritePixel.BitDepth / 8);

			using var tmpImg = MemoryPool<byte>.Shared.Rent(bytePerPixel * img.Width * img.Height * 2);
			Image.Save(tmpImg.Memory.Span, img.Span, TConfig.FromPixel<TWritePixel>());

			var newImg = Image.Load<TWritePixel, TConfig>(tmpImg.Memory.Span);

			assert(img, newImg);
		}

		public static void TestFormatLoad<TReadPixel, TConfig>(scoped ReadOnlySpan<byte> imgData, Action<ImageMemory<TReadPixel>> assert)
			where TReadPixel : unmanaged, IPixel<TReadPixel>
			where TConfig : struct, IFormatConfig<TConfig>
		{
			var img = Image.Load<TReadPixel, TConfig>(imgData);

			assert(img);
		}
	}
}
