public struct StackReader
{
	private VirtualMachine virtualMachine;
	private int stackIndex;

	public StackReader(VirtualMachine virtualMachine, int stackIndex)
	{
		this.virtualMachine = virtualMachine;
		this.stackIndex = stackIndex;
	}

	public bool ReadBool()
	{
		return IsStackIndexValid() ?
			virtualMachine.valueStack.buffer[stackIndex++].asBool :
			default;
	}

	public int ReadInt()
	{
		return IsStackIndexValid() ?
			virtualMachine.valueStack.buffer[stackIndex++].asInt :
			default;
	}

	public float ReadFloat()
	{
		return IsStackIndexValid() ?
			virtualMachine.valueStack.buffer[stackIndex++].asFloat :
			default;
	}

	public string ReadString()
	{
		return IsStackIndexValid() ?
			virtualMachine.heap.buffer[
				virtualMachine.valueStack.buffer[stackIndex++].asInt
			] as string :
			default;
	}

	public T ReadStruct<T>() where T : struct, IMarshalable
	{
		var value = default(T);
		value.Read(ref this);
		return value;
	}

	private bool IsStackIndexValid()
	{
		if (stackIndex < virtualMachine.valueStack.count)
			return true;
		virtualMachine.Error("Tried to read value out of stack bounds");
		return false;
	}
}