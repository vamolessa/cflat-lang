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

	public static ValueData GetAt(this VirtualMachine vm, int index)
	{
		var baseIndex = vm.callframeStack.buffer[vm.callframeStack.count - 1].baseStackIndex;
		return vm.valueStack.buffer[baseIndex + index];
	}

	public static ValueData GetArgs(this VirtualMachine vm)
	{
		return new ValueData();
	}
}