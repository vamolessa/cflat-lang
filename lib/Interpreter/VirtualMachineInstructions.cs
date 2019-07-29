internal static class VirtualMachineInstructions
{
	public static byte NextByte(VirtualMachine vm)
	{
		return vm.chunk.bytes.buffer[vm.programCount++];
	}

	public static bool Tick(VirtualMachine vm)
	{
		vm.previousProgramCount = vm.programCount;
		var nextInstruction = (Instruction)NextByte(vm);
		switch (nextInstruction)
		{
		case Instruction.Halt:
			return true;
		case Instruction.Return:
			System.Console.WriteLine(VirtualMachineHelper.PopToString(vm));
			return true;
		case Instruction.Print:
			System.Console.WriteLine(VirtualMachineHelper.PopToString(vm));
			break;
		case Instruction.Pop:
			vm.valueStack.count -= 1;
			vm.typeStack.count -= 1;
			break;
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
				var index = NextByte(vm);
				vm.PushValue(
					vm.chunk.literalData.buffer[index],
					vm.chunk.literalTypes.buffer[index]
				);
				break;
			}
		case Instruction.AssignLocal:
			vm.valueStack.buffer[NextByte(vm)] = vm.PopValue();
			break;
		case Instruction.LoadLocal:
			{
				var index = NextByte(vm);
				vm.PushValue(
					vm.valueStack.buffer[index],
					vm.typeStack.buffer[index]
				);
				break;
			}
		case Instruction.RemoveLocals:
			{
				var count = NextByte(vm);
				var last = vm.valueStack.count - 1;

				vm.valueStack.buffer[last - count] = vm.valueStack.buffer[last];
				vm.typeStack.buffer[last - count] = vm.typeStack.buffer[last];

				vm.valueStack.count -= count;
				vm.typeStack.count -= count;
			}
			break;
		case Instruction.IntToFloat:
			vm.PushValue(new ValueData((float)vm.PopValue().asInt), ValueType.Float);
			break;
		case Instruction.FloatToInt:
			vm.PushValue(new ValueData((int)vm.PopValue().asFloat), ValueType.Int);
			break;
		case Instruction.NegateInt:
			vm.Peek().asInt = -vm.Peek().asInt;
			break;
		case Instruction.NegateFloat:
			vm.Peek().asFloat = -vm.Peek().asFloat;
			break;
		case Instruction.AddInt:
			vm.PeekBefore().asInt += vm.PopValue().asInt;
			break;
		case Instruction.AddFloat:
			vm.PeekBefore().asFloat += vm.PopValue().asFloat;
			break;
		case Instruction.SubtractInt:
			vm.PeekBefore().asInt -= vm.PopValue().asInt;
			break;
		case Instruction.SubtractFloat:
			vm.PeekBefore().asFloat -= vm.PopValue().asFloat;
			break;
		case Instruction.MultiplyInt:
			vm.PeekBefore().asInt *= vm.PopValue().asInt;
			break;
		case Instruction.MultiplyFloat:
			vm.PeekBefore().asFloat *= vm.PopValue().asFloat;
			break;
		case Instruction.DivideInt:
			vm.PeekBefore().asInt /= vm.PopValue().asInt;
			break;
		case Instruction.DivideFloat:
			vm.PeekBefore().asFloat /= vm.PopValue().asFloat;
			break;
		case Instruction.Not:
			vm.Peek().asBool = !vm.Peek().asBool;
			break;
		case Instruction.EqualBool:
			vm.PushValue(
				new ValueData(vm.PopValue().asBool == vm.PopValue().asBool),
				ValueType.Bool
			);
			break;
		case Instruction.EqualInt:
			vm.PushValue(
				new ValueData(vm.PopValue().asInt == vm.PopValue().asInt),
				ValueType.Bool
			);
			break;
		case Instruction.EqualFloat:
			vm.PushValue(
				new ValueData(vm.PopValue().asFloat == vm.PopValue().asFloat),
				ValueType.Bool
			);
			break;
		case Instruction.EqualString:
			vm.PushValue(
				new ValueData(
					(vm.heap.buffer[vm.PopValue().asInt] as string).Equals(
					vm.heap.buffer[vm.PopValue().asInt] as string)
				),
				ValueType.Bool
			);
			break;
		case Instruction.GreaterInt:
			vm.PushValue(
				new ValueData(vm.PopValue().asInt < vm.PopValue().asInt),
				ValueType.Bool
			);
			break;
		case Instruction.GreaterFloat:
			vm.PushValue(
				new ValueData(vm.PopValue().asFloat < vm.PopValue().asFloat),
				ValueType.Bool
			);
			break;
		case Instruction.LessInt:
			vm.PushValue(
				new ValueData(vm.PopValue().asInt > vm.PopValue().asInt),
				ValueType.Bool
			);
			break;
		case Instruction.LessFloat:
			vm.PushValue(
				new ValueData(vm.PopValue().asFloat > vm.PopValue().asFloat),
				ValueType.Bool
			);
			break;
		default:
			break;
		}

		return false;
	}
}