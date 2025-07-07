using MathLib;

namespace ImageLib.Test
{
	public class PixelOperationTests
	{
		[SetUp]
		public void Setup()
		{
		}

		[Test]
		public unsafe void ReadWritePixel()
		{
			Rgba32 p0 = new(255, 0, 255, 0);

			Span<byte> buffer = stackalloc byte[sizeof(Rgba32)];
			PixelOperations.Write(in p0, PixelFormat.FromPixel<Rgba32>(), buffer);

			Rgba32 p1 = new();
			PixelOperations.Read(ref p1, PixelFormat.FromPixel<Rgba32>(), buffer);
			AssertPixel(p0, p1);

			Rgb24 p2 = new();
			PixelOperations.Read(ref p2, PixelFormat.FromPixel<Rgba32>(), buffer);
			AssertPixel(p0, p2);

			Rgb24 p3 = new(0, 255, 0);
			PixelOperations.Write(in p3, PixelFormat.FromPixel<Rgb24>(), buffer);

			PixelOperations.Read(ref p0, PixelFormat.FromPixel<Rgb24>(), buffer);
			AssertPixel(p0, p3);
		}

		[Test]
		public unsafe void ConvertFrom32To64BitDepth()
		{
			// Same color data in different bit depths: RGBA = (255, 0, 255, 0)
			Rgba32 p0 = new(byte.MaxValue, 0, byte.MaxValue, 0);
			Rgba64 p1 = new(byte.MaxValue, 0, byte.MaxValue, 0);

			// Write 32bpp pixel to a 64bpp buffer, read it back as 64bpp
			Span<byte> buffer = stackalloc byte[sizeof(Rgba64)];
			PixelOperations.Write(in p0, PixelFormat.FromPixel<Rgba32>(), buffer);
			Rgba64 p2 = new();
			PixelOperations.Read(ref p2, PixelFormat.FromPixel<Rgba32>(), buffer);

			// Verify 32->64 conversion worked, with values unchanged
			AssertPixel(p1, p2); // Verify 32->64 bit depth scaling worked
		}

		[Test]
		public unsafe void ConvertFrom64To32BitDepth()
		{
			// Same color data in different bit depths: RGBA = (255, 0, 255, 0)
			Rgba32 p0 = new(byte.MaxValue, 0, byte.MaxValue, 0);
			Rgba64 p1 = new(byte.MaxValue, 0, byte.MaxValue, 0);
			
			// Write 64bpp pixel to 64bpp buffer, read it back as 32bpp
			Span<byte> buffer = stackalloc byte[sizeof(Rgba64)];
			PixelOperations.Write(in p1, PixelFormat.FromPixel<Rgba64>(), buffer);
			Rgba32 p2 = new();
			PixelOperations.Read(ref p2, PixelFormat.FromPixel<Rgba64>(), buffer);

			// Verify 64->32 conversion worked, with values unchanged (only truncated)
			AssertPixel(p0, p2);
		}

		void AssertPixel(Rgba32 p1, Rgba32 p2)
		{
			Assert.That(p1, Is.EqualTo(p2),
				$"Pixel mismatch: Expected RGBA({p1.r}, {p1.g}, {p1.b}, {p1.a}) " +
				$"but got RGBA({p2.r}, {p2.g}, {p2.b}, {p2.a})");
		}
		void AssertPixel(Rgba64 p1, Rgba64 p2)
		{
			Assert.That(p1, Is.EqualTo(p2),
				$"Pixel mismatch: Expected RGBA({p1.r}, {p1.g}, {p1.b}, {p1.a}) " +
				$"but got RGBA({p2.r}, {p2.g}, {p2.b}, {p2.a})");
		}

		void AssertPixel(Rgba32 p1, Rgb24 p2)
		{
			Assert.That(p1.r, Is.EqualTo(p2.r), "Red channel mismatch");
			Assert.That(p1.g, Is.EqualTo(p2.g), "Green channel mismatch");
			Assert.That(p1.b, Is.EqualTo(p2.b), "Blue channel mismatch");
		}
	}
}
