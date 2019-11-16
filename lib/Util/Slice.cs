namespace cflat
{
	public readonly struct Slice
	{
		public readonly ushort index;
		public readonly ushort length;

		public static Slice FromTo(Slice a, Slice b)
		{
			return new Slice(
				a.index,
				b.index + b.length - a.index
			);
		}

		public Slice(ushort index, ushort length)
		{
			this.index = index;
			this.length = length;
		}

		public Slice(int index, int length)
		{
			this.index = (ushort)index;
			this.length = (ushort)length;
		}
	}
}