using VT = Value.Type;

public static class VirtualMachineInstructions
{
	public static void Return(VirtualMachine vm)
	{
		var value = vm.PopValue();
		System.Console.WriteLine(value.ToString());
	}

	public static void LoadConstant(VirtualMachine vm)
	{
		var value = VirtualMachineHelper.ReadConstant(vm);
		vm.PushValue(value);
	}

	public static void Negate(VirtualMachine vm)
	{
		var value = vm.PopValue();
		if (value.type == VT.IntegerNumber)
			value = new Value(-value.data.asInteger);
		else if (value.type == VT.RealNumber)
			value = new Value(-value.data.asFloat);
		vm.PushValue(value);
	}

	public static void Add(VirtualMachine vm)
	{
		var b = vm.PopValue();
		var a = vm.PopValue();

		if (a.type == VT.IntegerNumber && b.type == VT.IntegerNumber)
			a = new Value(a.data.asInteger + b.data.asInteger);
		else if (a.type == VT.IntegerNumber && b.type == VT.RealNumber)
			a = new Value(a.data.asInteger + b.data.asFloat);
		else if (a.type == VT.RealNumber && b.type == VT.IntegerNumber)
			a = new Value(a.data.asFloat + b.data.asInteger);
		else if (a.type == VT.RealNumber && b.type == VT.RealNumber)
			a = new Value(a.data.asFloat + b.data.asFloat);

		vm.PushValue(a);
	}

	public static void Subtract(VirtualMachine vm)
	{
		var b = vm.PopValue();
		var a = vm.PopValue();

		if (a.type == VT.IntegerNumber && b.type == VT.IntegerNumber)
			a = new Value(a.data.asInteger - b.data.asInteger);
		else if (a.type == VT.IntegerNumber && b.type == VT.RealNumber)
			a = new Value(a.data.asInteger - b.data.asFloat);
		else if (a.type == VT.RealNumber && b.type == VT.IntegerNumber)
			a = new Value(a.data.asFloat - b.data.asInteger);
		else if (a.type == VT.RealNumber && b.type == VT.RealNumber)
			a = new Value(a.data.asFloat - b.data.asFloat);

		vm.PushValue(a);
	}

	public static void Multiply(VirtualMachine vm)
	{
		var b = vm.PopValue();
		var a = vm.PopValue();

		if (a.type == VT.IntegerNumber && b.type == VT.IntegerNumber)
			a = new Value(a.data.asInteger * b.data.asInteger);
		else if (a.type == VT.IntegerNumber && b.type == VT.RealNumber)
			a = new Value(a.data.asInteger * b.data.asFloat);
		else if (a.type == VT.RealNumber && b.type == VT.IntegerNumber)
			a = new Value(a.data.asFloat * b.data.asInteger);
		else if (a.type == VT.RealNumber && b.type == VT.RealNumber)
			a = new Value(a.data.asFloat * b.data.asFloat);

		vm.PushValue(a);
	}

	public static void Divide(VirtualMachine vm)
	{
		var b = vm.PopValue();
		var a = vm.PopValue();

		if (a.type == VT.IntegerNumber && b.type == VT.IntegerNumber)
			a = new Value(a.data.asInteger / b.data.asInteger);
		else if (a.type == VT.IntegerNumber && b.type == VT.RealNumber)
			a = new Value(a.data.asInteger / b.data.asFloat);
		else if (a.type == VT.RealNumber && b.type == VT.IntegerNumber)
			a = new Value(a.data.asFloat / b.data.asInteger);
		else if (a.type == VT.RealNumber && b.type == VT.RealNumber)
			a = new Value(a.data.asFloat / b.data.asFloat);

		vm.PushValue(a);
	}
}