using System.Diagnostics;

namespace cflat
{
	[DebuggerTypeProxy(typeof(MemoryDebugView))]
	public struct Memory
	{
		public ValueData[] values;
		public int stackCount;
		public int heapStart;

		public Memory(int capacity)
		{
			values = new ValueData[capacity];
			stackCount = 0;
			heapStart = values.Length;
		}

		public void Reset()
		{
			stackCount = 0;
			heapStart = values.Length;
		}

		public void GrowStack(int size)
		{
			stackCount += size;

			if (stackCount > heapStart)
			{
				var temp = new ValueData[values.Length << 1];
				var previousStackCount = stackCount - size;
				System.Array.Copy(values, 0, temp, 0, previousStackCount);
				var heapCount = values.Length - heapStart;
				var newHeapStart = temp.Length - heapCount;
				System.Array.Copy(values, heapStart, temp, newHeapStart, heapCount);
				values = temp;
				heapStart = newHeapStart;
			}
		}

		public void PushBackStack(ValueData value)
		{
			if (stackCount < heapStart)
			{
				values[stackCount++] = value;
			}
			else
			{
				GrowStack(1);
				values[stackCount - 1] = value;
			}
		}

		public void GrowHeap(int size)
		{
			heapStart -= size;

			if (stackCount > heapStart)
			{
				heapStart += size;
				var heapCount = values.Length - heapStart;
				var totalCount = heapCount + size + stackCount;
				var newLength = values.Length << 1;
				while (newLength < totalCount)
					newLength <<= 1;
				var temp = new ValueData[newLength];

				System.Array.Copy(values, 0, temp, 0, stackCount);
				var newHeapStart = newLength - heapCount;
				System.Array.Copy(values, heapStart, temp, newHeapStart, heapCount);
				values = temp;
				heapStart = newHeapStart - size;
			}
		}
	}

	public sealed class MemoryDebugView
	{
		public readonly ValueData[] stack;
		public readonly ValueData[] heap;

		public MemoryDebugView(Memory memory)
		{
			stack = new ValueData[memory.stackCount];
			System.Array.Copy(memory.values, stack, stack.Length);
			heap = new ValueData[memory.values.Length - memory.heapStart];
			System.Array.Copy(memory.values, memory.heapStart, heap, 0, heap.Length);
		}
	}
}