using Xunit;

public sealed class MemoryTests
{
	[Fact]
	public void HeapTest()
	{
		var memory = new Memory(4);
		Assert.Equal(4, memory.values.Length);
		Assert.Equal(0, memory.stackCount);
		Assert.Equal(4, memory.heapStart);

		memory.GrowHeap(3);
		Assert.Equal(4, memory.values.Length);
		Assert.Equal(1, memory.heapStart);

		memory.GrowHeap(1);
		Assert.Equal(4, memory.values.Length);
		Assert.Equal(0, memory.heapStart);

		memory.GrowHeap(2);
		Assert.Equal(8, memory.values.Length);
		Assert.Equal(2, memory.heapStart);
	}
}