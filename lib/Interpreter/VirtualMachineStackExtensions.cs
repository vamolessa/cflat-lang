public static class VirtualMachineStackExtensions
{
	public static void PushSimple(this VirtualMachine vm, ValueData value, ValueType type)
	{
		vm.typeStack.PushBack(type);
		vm.valueStack.PushBack(value);
	}

	public static void PopSimple(this VirtualMachine vm, out ValueData value, out ValueType type)
	{
		type = vm.typeStack.PopLast();
		value = vm.valueStack.PopLast();
	}
}