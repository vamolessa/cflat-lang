using VT = ValueType;

internal static class VirtualMachineInstructions
{
	public static bool Tick(VirtualMachine vm)
	{
		var nextInstruction = (Instruction)vm.chunk.bytes.buffer[vm.programCount++];
		switch (nextInstruction)
		{
		case Instruction.Return:
			System.Console.WriteLine(VirtualMachineHelper.PopToString(vm));
			return true;
		case Instruction.LoadNil:
			vm.PushValue(new ValueData(), ValueType.Nil);
			break;
		case Instruction.LoadTrue:
			vm.PushValue(new ValueData(true), ValueType.Bool);
			break;
		case Instruction.LoadFalse:
			vm.PushValue(new ValueData(false), ValueType.Bool);
			break;
		case Instruction.LoadLiteral:
			{
				var index = vm.chunk.bytes.buffer[vm.programCount++];
				vm.PushValue(
					vm.chunk.literalData.buffer[index],
					vm.chunk.literalTypes.buffer[index]
				);
				break;
			}
		case Instruction.NegateInt:
			vm.Peek().asInt = -vm.Peek().asInt;
			break;
		case Instruction.NegateFloat:
			vm.Peek().asFloat = -vm.Peek().asFloat;
			break;
		case Instruction.AddInt:
			vm.PeekBefore().asInt += vm.PopValue().asInt;
			break;
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
			vm.PushValue(new Value(!vm.PopValue().IsTruthy()));
			break;
		case Instruction.Equal:
			vm.PushValue(new Value(Value.AreEqual(vm.heap.buffer, vm.PopValue(), vm.PopValue())));
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