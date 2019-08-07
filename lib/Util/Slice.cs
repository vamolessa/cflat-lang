public readonly struct Slice
{
	public readonly int index;
	public readonly int length;

	public Slice(int index, int length)
	{
		this.index = index;
		this.length = length;
	}
}