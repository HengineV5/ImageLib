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
		public unsafe void BitDepthScaling()
		{
			Rgba32 p0 = new(byte.MaxValue, 0, byte.MaxValue, 0);
			Rgba64 p1 = new(byte.MaxValue, 0, byte.MaxValue, 0);

			Span<byte> buffer = stackalloc byte[sizeof(Rgba64)];
			PixelOperations.Write(in p0, PixelFormat.FromPixel<Rgba32>(), buffer);

			Rgba64 p2 = new();
			PixelOperations.Read(ref p2, PixelFormat.FromPixel<Rgba32>(), buffer);
			AssertPixel(p1, p2);

			PixelOperations.Write(in p1, PixelFormat.FromPixel<Rgba64>(), buffer);

			Rgba32 p3 = new(byte.MaxValue, 0, byte.MaxValue, 0);
			PixelOperations.Read(ref p3, PixelFormat.FromPixel<Rgba64>(), buffer);
			AssertPixel(p0, p3);
		}

		void AssertPixel(Rgba32 p1, Rgba32 p2)
		{
			Assert.That(p1.r == p2.r, Is.True);
			Assert.That(p1.g == p2.g, Is.True);
			Assert.That(p1.b == p2.b, Is.True);
		}
		void AssertPixel(Rgba64 p1, Rgba64 p2)
		{
			Assert.That(p1.r == p2.r, Is.True);
			Assert.That(p1.g == p2.g, Is.True);
			Assert.That(p1.b == p2.b, Is.True);
			Assert.That(p1.a == p2.a, Is.True);
		}

		void AssertPixel(Rgba32 p1, Rgb24 p2)
		{
			Assert.That(p1.r == p2.r, Is.True);
			Assert.That(p1.g == p2.g, Is.True);
			Assert.That(p1.b == p2.b, Is.True);
		}
	}
}
