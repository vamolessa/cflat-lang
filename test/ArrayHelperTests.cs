using Xunit;

public sealed class ArrayHelperTests
{
	[Theory]
	[InlineData(new int[] { 0, 1, 2, 3, 4 }, 0, 5, new int[] { 4, 3, 2, 1, 0 })]
	[InlineData(new int[] { 0, 1, 2, 3, 4 }, 1, 3, new int[] { 0, 3, 2, 1, 4 })]
	[InlineData(new int[] { 0, 1, 2, 3 }, 0, 4, new int[] { 3, 2, 1, 0 })]
	[InlineData(new int[] { 0, 1, 2, 3 }, 1, 2, new int[] { 0, 2, 1, 3 })]
	public void ReverseTest(int[] array, int index, int count, int[] expected)
	{
		array.Reverse(index, count);
		Assert.Equal(expected, array);
	}

	[Theory]
	[InlineData(new int[] { 1, 2, 91, 92, 93 }, 2, new int[] { 91, 92, 93, 1, 2 })]
	[InlineData(new int[] { 1, 2, 3, 91, 92, 93 }, 3, new int[] { 91, 92, 93, 1, 2, 3 })]
	public void SwapRangesTest(int[] array, int pivot, int[] expected)
	{
		array.SwapRanges(0, pivot, array.Length);
		Assert.Equal(expected, array);
	}
}