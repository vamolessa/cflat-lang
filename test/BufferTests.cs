using Xunit;
using cflat;

public sealed class BufferTests
{
	[Theory]
	[InlineData(new[] { 0, 1, 2, 3 }, 0, new[] { 3, 1, 2 })]
	[InlineData(new[] { 0, 1, 2, 3 }, 1, new[] { 0, 3, 2 })]
	[InlineData(new[] { 0, 1, 2, 3 }, 2, new[] { 0, 1, 3 })]
	[InlineData(new[] { 0, 1, 2, 3 }, 3, new[] { 0, 1, 2 })]
	public void SwapRemoveTest(int[] array, int indexToRemove, int[] expectedArray)
	{
		var buffer = new Buffer<int>(array.Length);
		foreach (var e in array)
			buffer.PushBack(e);

		Assert.Equal(array, buffer.ToArray());
		buffer.SwapRemove(indexToRemove);
		Assert.Equal(expectedArray, buffer.ToArray());
	}
}