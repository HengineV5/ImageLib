namespace ImageLib.Exr
{
	struct ExrVersion
	{
		public byte version;

		public bool isSinglePart;
		public bool hasLongNames;
		public bool hasDeepData;
		public bool isMultiPart;

		public ExrVersion(byte version, bool isSinglePart, bool hasLongNames, bool hasDeepData, bool isMultiPart)
		{
			this.version = version;
			this.isSinglePart = isSinglePart;
			this.hasLongNames = hasLongNames;
			this.hasDeepData = hasDeepData;
			this.isMultiPart = isMultiPart;
		}
	}
}
