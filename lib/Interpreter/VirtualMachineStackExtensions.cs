public interface IMarshalable
{
	void Read(ref StackReader reader);
	void Push(VirtualMachine vm);
	void Pop(VirtualMachine vm);
}

public static class VirtualMachineStackExtensions
{
	public static void PushUnit(this VirtualMachine vm)
	{
		vm.typeStack.PushBack(new ValueType(TypeKind.Unit));
		vm.valueStack.PushBack(new ValueData());
	}

	public static void Push(this VirtualMachine vm, ValueData value, ValueType type)
	{
		vm.typeStack.PushBack(type);
		vm.valueStack.PushBack(value);
	}

	public static ValueData Pop(this VirtualMachine vm)
	{
		vm.typeStack.count -= 1;
		return vm.valueStack.PopLast();
	}

	public static void Pop(this VirtualMachine vm, out ValueData value, out ValueType type)
	{
		type = vm.typeStack.PopLast();
		value = vm.valueStack.PopLast();
	}

	public static void PushBool(this VirtualMachine vm, bool value)
	{
		vm.Push(new ValueData(value), new ValueType(TypeKind.Bool));
	}

	public static void PushInt(this VirtualMachine vm, int value)
	{
		vm.Push(new ValueData(value), new ValueType(TypeKind.Int));
	}

	public static void PushFloat(this VirtualMachine vm, float value)
	{
		vm.Push(new ValueData(value), new ValueType(TypeKind.Float));
	}

	public static void PushString(this VirtualMachine vm, string value)
	{
		vm.Push(
			new ValueData(vm.heap.count),
			new ValueType(TypeKind.String)
		);
		vm.heap.PushBack(value);
	}

	public static void PushStruct<T>(this VirtualMachine vm, T value) where T : struct, IMarshalable
	{
		value.Push(vm);
	}

	public static bool PopBool(this VirtualMachine vm)
	{
		return vm.Pop().asBool;
	}

	public static int PopInt(this VirtualMachine vm)
	{
		return vm.Pop().asInt;
	}

	public static float PopFloat(this VirtualMachine vm)
	{
		return vm.Pop().asFloat;
	}

	public static string PopString(this VirtualMachine vm)
	{
		return vm.heap.buffer[vm.Pop().asInt] as string;
	}

	public static T PopStruct<T>(this VirtualMachine vm) where T : struct, IMarshalable
	{
		var value = default(T);
		value.Pop(vm);
		return value;
	}

	public static StackReader ReadArgs(this VirtualMachine vm)
	{
		return new StackReader(
			vm,
			vm.callframeStack.buffer[vm.callframeStack.count - 1].baseStackIndex
		);
	}
}