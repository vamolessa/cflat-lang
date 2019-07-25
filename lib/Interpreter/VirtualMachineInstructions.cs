using VT = Value.Type;

internal static class VirtualMachineInstructions
{
	public static bool Tick(VirtualMachine vm)
	{
		var nextInstruction = (Instruction)vm.chunk.bytes.buffer[vm.programCount++];
		switch (nextInstruction)
		{
		case Instruction.Return:
			System.Console.WriteLine(vm.PopValue().ToString());
			return true;
		case Instruction.LoadNil:
			vm.PushValue(new Value());
			break;
		case Instruction.LoadTrue:
			vm.PushValue(new Value(true));
			break;
		case Instruction.LoadFalse:
			vm.PushValue(new Value(false));
			break;
		case Instruction.LoadConstant:
			var index = vm.chunk.bytes.buffer[vm.programCount++];
			vm.PushValue(vm.chunk.constants.buffer[index]);
			break;
		case Instruction.Negate:
			{
				var value = vm.PopValue();
				if (value.type == VT.Int)
					vm.PushValue(new Value(-value.data.asInt));
				else if (value.type == VT.Float)
					vm.PushValue(new Value(-value.data.asFloat));
				else
					return vm.Error("Operand must be a number");
				break;
			}
		case Instruction.Add:
			{
				var b = vm.PopValue();
				var a = vm.PopValue();

				if (a.type == VT.Int && b.type == VT.Int)
					vm.PushValue(new Value(a.data.asInt + b.data.asInt));
				else if (a.type == VT.Int && b.type == VT.Float)
					vm.PushValue(new Value(a.data.asInt + b.data.asFloat));
				else if (a.type == VT.Float && b.type == VT.Int)
					vm.PushValue(new Value(a.data.asFloat + b.data.asInt));
				else if (a.type == VT.Float && b.type == VT.Float)
					vm.PushValue(new Value(a.data.asFloat + b.data.asFloat));
				else
					return vm.Error("Operands must be a number");
				break;
			}
		case Instruction.Subtract:
			{
				var b = vm.PopValue();
				var a = vm.PopValue();

				if (a.type == VT.Int && b.type == VT.Int)
					vm.PushValue(new Value(a.data.asInt - b.data.asInt));
				else if (a.type == VT.Int && b.type == VT.Float)
					vm.PushValue(new Value(a.data.asInt - b.data.asFloat));
				else if (a.type == VT.Float && b.type == VT.Int)
					vm.PushValue(new Value(a.data.asFloat - b.data.asInt));
				else if (a.type == VT.Float && b.type == VT.Float)
					vm.PushValue(new Value(a.data.asFloat - b.data.asFloat));
				else
					return vm.Error("Operands must be a number");
				break;
			}
		case Instruction.Multiply:
			{
				var b = vm.PopValue();
				var a = vm.PopValue();

				if (a.type == VT.Int && b.type == VT.Int)
					vm.PushValue(new Value(a.data.asInt * b.data.asInt));
				else if (a.type == VT.Int && b.type == VT.Float)
					vm.PushValue(new Value(a.data.asInt * b.data.asFloat));
				else if (a.type == VT.Float && b.type == VT.Int)
					vm.PushValue(new Value(a.data.asFloat * b.data.asInt));
				else if (a.type == VT.Float && b.type == VT.Float)
					vm.PushValue(new Value(a.data.asFloat * b.data.asFloat));
				else
					return vm.Error("Operands must be a number");
				break;
			}
		case Instruction.Divide:
			{
				var b = vm.PopValue();
				var a = vm.PopValue();

				if (a.type == VT.Int && b.type == VT.Int)
					vm.PushValue(new Value(a.data.asInt / b.data.asInt));
				else if (a.type == VT.Int && b.type == VT.Float)
					vm.PushValue(new Value(a.data.asInt / b.data.asFloat));
				else if (a.type == VT.Float && b.type == VT.Int)
					vm.PushValue(new Value(a.data.asFloat / b.data.asInt));
				else if (a.type == VT.Float && b.type == VT.Float)
					vm.PushValue(new Value(a.data.asFloat / b.data.asFloat));
				else
					return vm.Error("Operands must be a number");
				break;
			}
		case Instruction.Not:
			vm.PushValue(new Value(vm.PopValue().IsFalsey()));
			break;
		case Instruction.Equal:
			vm.PushValue(new Value(Value.AreEqual(vm.PopValue(), vm.PopValue())));
			break;
		case Instruction.Greater:
			{
				var b = vm.PopValue();
				var a = vm.PopValue();

				if (a.type == VT.Int && b.type == VT.Int)
					vm.PushValue(new Value(a.data.asInt > b.data.asInt));
				else if (a.type == VT.Int && b.type == VT.Float)
					vm.PushValue(new Value(a.data.asInt > b.data.asFloat));
				else if (a.type == VT.Float && b.type == VT.Int)
					vm.PushValue(new Value(a.data.asFloat > b.data.asInt));
				else if (a.type == VT.Float && b.type == VT.Float)
					vm.PushValue(new Value(a.data.asFloat > b.data.asFloat));
				else
					return vm.Error("Operands must be a number");
				break;
			}
		case Instruction.Less:
			{
				var b = vm.PopValue();
				var a = vm.PopValue();

				if (a.type == VT.Int && b.type == VT.Int)
					vm.PushValue(new Value(a.data.asInt < b.data.asInt));
				else if (a.type == VT.Int && b.type == VT.Float)
					vm.PushValue(new Value(a.data.asInt < b.data.asFloat));
				else if (a.type == VT.Float && b.type == VT.Int)
					vm.PushValue(new Value(a.data.asFloat < b.data.asInt));
				else if (a.type == VT.Float && b.type == VT.Float)
					vm.PushValue(new Value(a.data.asFloat < b.data.asFloat));
				else
					return vm.Error("Operands must be a number");
				break;
			}
		default:
			break;
		}

		return false;
	}
}