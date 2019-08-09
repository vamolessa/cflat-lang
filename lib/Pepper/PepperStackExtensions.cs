public static class PepperStackExtensions
{
	public static void PopSimple(this Pepper pepper, out ValueData value, out ValueType type)
	{
		value = pepper.virtualMachine.valueStack.PopLast();
		type = pepper.virtualMachine.typeStack.PopLast();
	}

	public static void PushSimple(this Pepper pepper, ValueData value, ValueType type)
	{
		pepper.virtualMachine.valueStack.PushBack(value);
		pepper.virtualMachine.typeStack.PushBack(type);
	}
}