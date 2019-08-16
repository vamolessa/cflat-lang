public interface IMarshalable
{
	int Size { get; }
	void Read(ref Marshal marshal);
	void Write(ref Marshal marshal);
}

public struct Marshal
{
	private VirtualMachine virtualMachine;
	private int stackIndex;

	public Marshal(VirtualMachine virtualMachine, int stackIndex)
	{
		this.virtualMachine = virtualMachine;
		this.stackIndex = stackIndex;
	}

	public void Read(out bool value)
	{
		value = IsStackIndexValid() ?
			virtualMachine.valueStack.buffer[stackIndex++].asBool :
			default;
	}

	public void Read(out int value)
	{
		value = IsStackIndexValid() ?
			value = virtualMachine.valueStack.buffer[stackIndex++].asInt :
			default;
	}

	public void Read(out float value)
	{
		value = IsStackIndexValid() ?
			virtualMachine.valueStack.buffer[stackIndex++].asFloat :
			default;
	}

	public void Read(out string value)
	{
		value = IsStackIndexValid() ?
			virtualMachine.heap.buffer[virtualMachine.valueStack.buffer[stackIndex++].asInt] as string :
			default;
	}

	public void Read<T>(out T value) where T : struct, IMarshalable
	{
		value = default;
		if (IsStackIndexValid())
			value.Read(ref this);
	}

	public void Write(bool value)
	{
		if (IsStackIndexValidForSize(1))
		{
			virtualMachine.typeStack.buffer[stackIndex] = new ValueType(TypeKind.Bool);
			virtualMachine.valueStack.buffer[stackIndex++].asBool = value;
		}
	}

	public void Write(int value)
	{
		if (IsStackIndexValidForSize(1))
		{
			virtualMachine.typeStack.buffer[stackIndex] = new ValueType(TypeKind.Int);
			virtualMachine.valueStack.buffer[stackIndex++].asInt = value;
		}
	}

	public void Write(float value)
	{
		if (IsStackIndexValidForSize(1))
		{
			virtualMachine.typeStack.buffer[stackIndex] = new ValueType(TypeKind.Float);
			virtualMachine.valueStack.buffer[stackIndex++].asFloat = value;
		}
	}

	public void Write(string value)
	{
		if (IsStackIndexValidForSize(1))
		{
			virtualMachine.typeStack.buffer[stackIndex] = new ValueType(TypeKind.String);
			virtualMachine.heap.buffer[virtualMachine.valueStack.buffer[stackIndex++].asInt] = value;
		}
	}

	public void Write<T>(T value) where T : struct, IMarshalable
	{
		if (IsStackIndexValidForSize(value.Size))
			value.Write(ref this);
	}

	public void Pop(out bool value)
	{
		value = virtualMachine.Pop().asBool;
	}

	public void Pop(out int value)
	{
		value = virtualMachine.Pop().asInt;
	}

	public void Pop(out float value)
	{
		value = virtualMachine.Pop().asFloat;
	}

	public void Pop(out string value)
	{
		value = virtualMachine.heap.buffer[virtualMachine.Pop().asInt] as string;
	}

	public void Pop<T>(out T value) where T : struct, IMarshalable
	{
		value = default;
		var size = value.Size;
		stackIndex = virtualMachine.valueStack.count - size;
		value.Read(ref this);
		virtualMachine.typeStack.count -= size;
		virtualMachine.valueStack.count -= size;
	}

	public void Push(bool value)
	{
		virtualMachine.Push(new ValueData(value), new ValueType(TypeKind.Bool));
	}

	public void Push(int value)
	{
		virtualMachine.Push(new ValueData(value), new ValueType(TypeKind.Int));
	}

	public void Push(float value)
	{
		virtualMachine.Push(new ValueData(value), new ValueType(TypeKind.Float));
	}

	public void Push(string value)
	{
		virtualMachine.Push(
			new ValueData(virtualMachine.heap.count),
			new ValueType(TypeKind.String)
		);
		virtualMachine.heap.PushBack(value);
	}

	public void Push<T>(T value) where T : struct, IMarshalable
	{
		var size = value.Size;
		virtualMachine.typeStack.Grow(size);
		virtualMachine.valueStack.Grow(size);
		value.Write(ref this);
	}

	private bool IsStackIndexValid()
	{
		if (stackIndex < virtualMachine.valueStack.count)
			return true;
		virtualMachine.Error("Tried to read/write value out of stack bounds");
		return false;
	}

	private bool IsStackIndexValidForSize(int size)
	{
		if (stackIndex + size <= virtualMachine.valueStack.count)
			return true;
		virtualMachine.Error("Tried to read/write value out of stack bounds");
		return false;
	}
}
