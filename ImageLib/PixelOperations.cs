using MathLib;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using System;

namespace ImageLib
{
	public static class PixelOperations
	{
		public static unsafe void Write<TPixel>(ref readonly TPixel pixel, ref readonly PixelFormat outputFormat, scoped Span<byte> output) where TPixel : unmanaged, IPixel<TPixel>
		{
			fixed (TPixel* pixelPtr = &pixel)
			{
				ReadOnlySpan<byte> pixelData = new(pixelPtr, sizeof(TPixel));
				Write(PixelFormat.FromPixel<TPixel>(), pixelData, in outputFormat, output);
			}
		}

		public static unsafe void Read<TPixel>(ref TPixel pixel, ref readonly PixelFormat inputFormat, scoped ReadOnlySpan<byte> input) where TPixel : unmanaged, IPixel<TPixel>
		{
			fixed (TPixel* pixelPtr = &pixel)
			{
				Span<byte> pixelData = new(pixelPtr, sizeof(TPixel));
				Write(in inputFormat, input, PixelFormat.FromPixel<TPixel>(), pixelData);
			}
		}

		public static void Write(ref readonly PixelFormat inputFormat, scoped ReadOnlySpan<byte> input, ref readonly PixelFormat outputFormat, scoped Span<byte> output)
		{
			if (outputFormat.channels == inputFormat.channels && outputFormat.bytesPerChannel == inputFormat.bytesPerChannel && outputFormat.channelType == inputFormat.channelType)
			{
				input.TryCopyTo(output);
				return;
			}

			if (outputFormat.channelType == ScalarType.Integer && inputFormat.channelType == ScalarType.Integer)
			{
				int channelsToWrite = int.Min(outputFormat.channels, inputFormat.channels);
				if (outputFormat.bytesPerChannel == inputFormat.bytesPerChannel)
				{
					for (int i = 0; i < channelsToWrite; i++)
					{
						input.Slice(i * inputFormat.bytesPerChannel, inputFormat.bytesPerChannel).TryCopyTo(output.Slice(i * outputFormat.bytesPerChannel, outputFormat.bytesPerChannel));
					}
				}
				else
				{
					int maxBytesPerChannel = int.Max(inputFormat.bytesPerChannel, outputFormat.bytesPerChannel);
					//Span<byte> tmpChannel = stackalloc byte[maxBytesPerChannel];
					Span<byte> tmpChannel = stackalloc byte[sizeof(long)];

					for (int i = 0; i < channelsToWrite; i++)
					{
						input.Slice(i * inputFormat.bytesPerChannel, inputFormat.bytesPerChannel).TryCopyTo(tmpChannel);

						if (outputFormat.bytesPerChannel < inputFormat.bytesPerChannel) // Only downscale channels
							ScaleChannel(outputFormat.bytesPerChannel - inputFormat.bytesPerChannel, ref MemoryMarshal.AsRef<long>(tmpChannel));

						tmpChannel.Slice(0, outputFormat.bytesPerChannel).TryCopyTo(output.Slice(i * outputFormat.bytesPerChannel, outputFormat.bytesPerChannel));
					}
				}
			}
			else if (outputFormat.channelType == ScalarType.Floating && inputFormat.channelType == ScalarType.Floating)
			{
				int channelsToWrite = int.Min(outputFormat.channels, inputFormat.channels);
				if (outputFormat.bytesPerChannel == inputFormat.bytesPerChannel)
				{
					for (int i = 0; i < channelsToWrite; i++)
					{
						input.Slice(i * inputFormat.bytesPerChannel, inputFormat.bytesPerChannel).TryCopyTo(output.Slice(i * outputFormat.bytesPerChannel, outputFormat.bytesPerChannel));
					}
				}
				else
				{
					int maxBytesPerChannel = int.Max(inputFormat.bytesPerChannel, outputFormat.bytesPerChannel);
					Span<byte> tmpChannel = stackalloc byte[maxBytesPerChannel];
				}
			}
			else
			{
				throw new Exception();
			}
		}

		static void ScaleChannel(int dir, ref long channel)
		{
			int steps = int.Abs(dir);
			if (int.Sign(dir) == 1)
			{
				for (int i = 0; i < steps; i++)
				{
					channel *= 256;
				}
			}
			else
			{
				for (int i = 0; i < steps; i++)
				{
					channel /= 256;
				}
			}
		}
	}
}
