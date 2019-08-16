public struct Point : IMarshalable
{
	public int x;
	public int y;
	public int z;

	public void Read(ref Marshaler marshaler)
	{
		marshaler.Read(out x);
		marshaler.Read(out y);
		marshaler.Read(out z);
	}
}

public interface IMarshalable
{
	void Read(ref Marshaler marshaler);
}

public struct Marshaler
{
	private VirtualMachine vm;
	private int index;

	public Marshaler Read(out bool value)
	{
		value = vm.valueStack.buffer[index++].asBool;
		return this;
	}

	public Marshaler Read(out int value)
	{
		value = vm.valueStack.buffer[index++].asInt;
		return this;
	}

	public Marshaler Read(out float value)
	{
		value = vm.valueStack.buffer[index++].asFloat;
		return this;
	}

	public Marshaler Read(out string value)
	{
		value = vm.heap.buffer[vm.valueStack.buffer[index++].asInt] as string;
		return this;
	}

	public Marshaler Read<T>(out T value) where T : struct, IMarshalable
	{
		value = default(T);
		value.Read(ref this);
		return this;
	}
}