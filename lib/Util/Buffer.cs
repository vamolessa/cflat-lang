using System.Diagnostics;

namespace cflat
{
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
				var previousCount = count - size;
				var newLength = buffer.Length << 1;
				while (newLength < count)
					newLength <<= 1;
				var temp = new T[newLength];
				System.Array.Copy(buffer, temp, previousCount);
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
				var temp = new T[buffer.Length << 1];
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
			buffer[index] = buffer[--count];
		}

		public T[] ToArray()
		{
			if (buffer != null && count > 0)
			{
				var array = new T[count];
				System.Array.Copy(buffer, 0, array, 0, array.Length);
				return array;
			}

			return new T[0];
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
}