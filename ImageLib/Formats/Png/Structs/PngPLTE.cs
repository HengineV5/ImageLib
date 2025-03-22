namespace ImageLib.Png
{
	ref struct PngPLTE
	{
		public Span<PngPalletEntry> pallet;
	}

	struct PngPalletEntry
	{
		public byte red;
		public byte green;
		public byte blue;
	}
}
