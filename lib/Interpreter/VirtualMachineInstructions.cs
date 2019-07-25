using VT = Value.Type;

internal static class VirtualMachineInstructions
{
	public static bool Tick(VirtualMachine vm)
	{
		var nextInstruction = (Instruction)vm.chunk.bytes.buffer[vm.programCount++];
		switch (nextInstruction)
		{
		case Instruction.Return:
			{
				var value = vm.PopValue();
				System.Console.WriteLine(value.ToString());
				return true;
			}
		case Instruction.LoadNil:
			{
				vm.PushValue(new Value());
				break;
			}
		case Instruction.LoadTrue:
			{
				vm.PushValue(new Value(true));
				break;
			}
		case Instruction.LoadFalse:
			{
				vm.PushValue(new Value(false));
				break;
			}
		case Instruction.LoadConstant:
			{
				var index = vm.chunk.bytes.buffer[vm.programCount++];
				var value = vm.chunk.constants.buffer[index];
				vm.PushValue(value);
				break;
			}
		case Instruction.Negate:
			{
				var value = vm.PopValue();
				if (value.type == VT.IntegerNumber)
					value = new Value(-value.data.asInt);
				else if (value.type == VT.RealNumber)
					value = new Value(-value.data.asFloat);
				else
					return vm.Error("Operand must be a number");

				vm.PushValue(value);
				break;
			}
		case Instruction.Add:
			{
				var b = vm.PopValue();
				var a = vm.PopValue();

				if (a.type == VT.IntegerNumber && b.type == VT.IntegerNumber)
					a = new Value(a.data.asInt + b.data.asInt);
				else if (a.type == VT.IntegerNumber && b.type == VT.RealNumber)
					a = new Value(a.data.asInt + b.data.asFloat);
				else if (a.type == VT.RealNumber && b.type == VT.IntegerNumber)
					a = new Value(a.data.asFloat + b.data.asInt);
				else if (a.type == VT.RealNumber && b.type == VT.RealNumber)
					a = new Value(a.data.asFloat + b.data.asFloat);
				else
					return vm.Error("Operands must be a number");

				vm.PushValue(a);
				break;
			}
		case Instruction.Subtract:
			{
				var b = vm.PopValue();
				var a = vm.PopValue();

				if (a.type == VT.IntegerNumber && b.type == VT.IntegerNumber)
					a = new Value(a.data.asInt - b.data.asInt);
				else if (a.type == VT.IntegerNumber && b.type == VT.RealNumber)
					a = new Value(a.data.asInt - b.data.asFloat);
				else if (a.type == VT.RealNumber && b.type == VT.IntegerNumber)
					a = new Value(a.data.asFloat - b.data.asInt);
				else if (a.type == VT.RealNumber && b.type == VT.RealNumber)
					a = new Value(a.data.asFloat - b.data.asFloat);
				else
					return vm.Error("Operands must be a number");

				vm.PushValue(a);
				break;
			}
		case Instruction.Multiply:
			{
				var b = vm.PopValue();
				var a = vm.PopValue();

				if (a.type == VT.IntegerNumber && b.type == VT.IntegerNumber)
					a = new Value(a.data.asInt * b.data.asInt);
				else if (a.type == VT.IntegerNumber && b.type == VT.RealNumber)
					a = new Value(a.data.asInt * b.data.asFloat);
				else if (a.type == VT.RealNumber && b.type == VT.IntegerNumber)
					a = new Value(a.data.asFloat * b.data.asInt);
				else if (a.type == VT.RealNumber && b.type == VT.RealNumber)
					a = new Value(a.data.asFloat * b.data.asFloat);
				else
					return vm.Error("Operands must be a number");

				vm.PushValue(a);
				break;
			}
		case Instruction.Divide:
			{
				var b = vm.PopValue();
				var a = vm.PopValue();

				if (a.type == VT.IntegerNumber && b.type == VT.IntegerNumber)
					a = new Value(a.data.asInt / b.data.asInt);
				else if (a.type == VT.IntegerNumber && b.type == VT.RealNumber)
					a = new Value(a.data.asInt / b.data.asFloat);
				else if (a.type == VT.RealNumber && b.type == VT.IntegerNumber)
					a = new Value(a.data.asFloat / b.data.asInt);
				else if (a.type == VT.RealNumber && b.type == VT.RealNumber)
					a = new Value(a.data.asFloat / b.data.asFloat);
				else
					return vm.Error("Operands must be a number");

				vm.PushValue(a);
				break;
			}
		default:
			break;
		}

		return false;
	}
}