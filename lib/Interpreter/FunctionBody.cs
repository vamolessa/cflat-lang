public struct FunctionBodyUnit
{
	private VirtualMachine vm;

	public FunctionBodyUnit(VirtualMachine vm)
	{
		this.vm = vm;
	}

	public void Return()
	{
		vm.valueStack.PushBack(new ValueData());
	}
}

public struct FunctionBodyBool
{
	private VirtualMachine vm;

	public FunctionBodyBool(VirtualMachine vm)
	{
		this.vm = vm;
	}

	public void Return(bool value)
	{
		vm.valueStack.PushBack(new ValueData(value));
	}
}

public struct FunctionBodyInt
{
	private VirtualMachine vm;

	public FunctionBodyInt(VirtualMachine vm)
	{
		this.vm = vm;
	}

	public void Return(int value)
	{
		vm.valueStack.PushBack(new ValueData(value));
	}
}

public struct FunctionBodyFloat
{
	private VirtualMachine vm;

	public FunctionBodyFloat(VirtualMachine vm)
	{
		this.vm = vm;
	}

	public void Return(float value)
	{
		vm.valueStack.PushBack(new ValueData(value));
	}
}

public struct FunctionBodyString
{
	private VirtualMachine vm;

	public FunctionBodyString(VirtualMachine vm)
	{
		this.vm = vm;
	}

	public void Return(string value)
	{
		vm.valueStack.PushBack(new ValueData(vm.heap.count));
		vm.heap.PushBack(value);
	}
}

public struct FunctionBodyStruct<T> where T : struct, IMarshalable
{
	private VirtualMachine vm;

	public FunctionBodyStruct(VirtualMachine vm)
	{
		this.vm = vm;
	}

	public void Return(T value)
	{
		var stackIndex = vm.valueStack.count;
		vm.valueStack.Grow(value.Size);
		var marshal = new RuntimeMarshal(vm, stackIndex);
		value.Write(ref marshal);
	}
}