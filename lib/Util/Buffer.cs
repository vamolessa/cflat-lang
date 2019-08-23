using System.Diagnostics;

[DebuggerTypeProxy(typeof(BufferDebugView<>))]
public struct Buffer<T>
{
	public const int MinCapacity = 2;

	public int count;
	public T[] buffer;

	public Buffer(int capacity)
	{
		buffer = new T[capacity >= MinCapacity ? capacity : MinCapacity];
		count = 0;
	}

	public void Grow(int size)
	{
		if (buffer == null)
			buffer = new T[MinCapacity];
		GrowUnchecked(size);
	}

	public void GrowUnchecked(int size)
	{
		count += size;

		if (count > buffer.Length)
		{
			var temp = new T[buffer.Length * 2];
			System.Array.Copy(buffer, temp, buffer.Length);
			buffer = temp;
		}
	}

	public void PushBack(T element)
	{
		if (buffer == null)
			buffer = new T[MinCapacity];
		PushBackUnchecked(element);
	}

	public void PushBackUnchecked(T element)
	{
		if (count >= buffer.Length)
		{
			var temp = new T[buffer.Length * 2];
			System.Array.Copy(buffer, temp, buffer.Length);
			buffer = temp;
		}

		buffer[count++] = element;
	}

	public T PopLast()
	{
		return buffer[--count];
	}

	public void SwapRemove(int index)
	{
		buffer[--count] = buffer[index];
	}
}

public sealed class BufferDebugView<T>
{
	public readonly T[] elements;

	public BufferDebugView(Buffer<T> buffer)
	{
		elements = new T[buffer.count];
		System.Array.Copy(buffer.buffer, elements, buffer.count);
	}
}
