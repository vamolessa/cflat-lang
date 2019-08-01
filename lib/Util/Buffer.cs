public struct Buffer<T>
{
	public int count;
	public T[] buffer;

	public Buffer(int capacity)
	{
		buffer = new T[capacity];
		count = 0;
	}

	public void PushBack(T element)
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