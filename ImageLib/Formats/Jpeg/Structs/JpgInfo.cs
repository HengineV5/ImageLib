using UtilLib.Span;

namespace ImageLib.Jpg
{
	ref struct JpgInfo
	{
		public SpanList<long> huffmanTables;
		public SpanList<long> quantTables;
	}
}
