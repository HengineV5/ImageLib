using ImageLib.Png;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageLib
{
	public static class ImageLibLog
	{
		internal static ILogger pngLogger = NullLogger.Instance;

		public static void SetLoggerFactory(ILoggerFactory loggerFactory)
		{
			ImageLibLog.pngLogger = loggerFactory.CreateLogger<PngFormat>();
		}
	}
}
