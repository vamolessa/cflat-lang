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

	public static Marshal MarshalArgs(this VirtualMachine vm)
	{
		var baseStackIndex = vm.callframeStack.buffer[vm.callframeStack.count - 1].baseStackIndex;
		return new Marshal(vm, baseStackIndex);
	}

	public static Marshal Marshal(this VirtualMachine vm)
	{
		return new Marshal(vm, vm.valueStack.count);
	}
}