using System.Text;

internal static class VirtualMachineInstructions
{
	public static byte NextByte(VirtualMachine vm, ref VirtualMachine.CallFrame frame)
	{
		return vm.chunk.bytes.buffer[frame.codeIndex++];
	}

	public static bool Tick(VirtualMachine vm)
	{
		ref var frame = ref vm.callframeStack.buffer[vm.callframeStack.count - 1];

		var nextInstruction = (Instruction)NextByte(vm, ref frame);
		switch (nextInstruction)
		{
		case Instruction.Halt:
			return true;
		case Instruction.Call:
			{
				var argCount = NextByte(vm, ref frame);
				var stackTop = vm.valueStack.count - argCount;
				var functionIndex = vm.valueStack.buffer[stackTop - 1].asInt;
				var function = vm.chunk.functions.buffer[functionIndex];

				vm.callframeStack.PushBack(
					new VirtualMachine.CallFrame(
						functionIndex,
						function.codeIndex,
						vm.valueStack.count - argCount
					)
				);
				break;
			}
		case Instruction.Return:
			{
				var returnType = vm.PeekType();
				var returnValue = vm.PopValue();

				vm.callframeStack.count -= 1;
				var stackTop = vm.callframeStack.buffer[vm.callframeStack.count].baseStackIndex - 1;

				vm.valueStack.count = stackTop;
				vm.typeStack.count = stackTop;

				vm.PushValue(returnValue, returnType);

				if (vm.callframeStack.count == 0)
					return true;
				break;
			}
		case Instruction.Print:
			{
				var sb = new StringBuilder();
				var size = vm.chunk.GetTypeSize(vm.PeekType());

				VirtualMachineHelper.ValueToString(
					vm,
					vm.valueStack.count - size,
					Option.None,
					sb
				);
				vm.typeStack.count -= size;
				vm.valueStack.count -= size;

				System.Console.WriteLine(sb);
				break;
			}
		case Instruction.Pop:
			vm.valueStack.count -= 1;
			vm.typeStack.count -= 1;
			break;
		case Instruction.PopMultiple:
			{
				var count = NextByte(vm, ref frame);
				vm.valueStack.count -= count;
				vm.typeStack.count -= count;
			}
			break;
		case Instruction.CopyTo:
			{
				var last = vm.valueStack.count - 1;
				var index = last - NextByte(vm, ref frame);
				vm.valueStack.buffer[index] = vm.valueStack.buffer[last];
				vm.typeStack.buffer[index] = vm.typeStack.buffer[last];
			}
			break;
		case Instruction.LoadUnit:
			vm.PushValue(new ValueData(), ValueType.Unit);
			break;
		case Instruction.LoadFalse:
			vm.PushValue(new ValueData(false), ValueType.Bool);
			break;
		case Instruction.LoadTrue:
			vm.PushValue(new ValueData(true), ValueType.Bool);
			break;
		case Instruction.LoadLiteral:
			{
				var index = NextByte(vm, ref frame);
				vm.PushValue(
					vm.chunk.literalData.buffer[index],
					vm.chunk.literalTypes.buffer[index]
				);
				break;
			}
		case Instruction.LoadFunction:
			{
				var index = BytesHelper.BytesToShort(NextByte(vm, ref frame), NextByte(vm, ref frame));
				var typeIndex = vm.chunk.functions.buffer[index].typeIndex;
				vm.PushValue(
					new ValueData(index),
					ValueTypeHelper.SetIndex(ValueType.Function, typeIndex)
				);
				break;
			}
		case Instruction.ConvertToStruct:
			{
				var index = BytesHelper.BytesToShort(NextByte(vm, ref frame), NextByte(vm, ref frame));
				var structType = vm.chunk.structTypes.buffer[index];

				var type = ValueTypeHelper.SetIndex(ValueType.Struct, index);
				var idx = vm.typeStack.count - structType.size;

				while (idx < vm.typeStack.count)
					vm.typeStack.buffer[idx++] = type;
				break;
			}
		case Instruction.AssignLocal:
			{
				var index = frame.baseStackIndex + NextByte(vm, ref frame);
				vm.valueStack.buffer[index] = vm.Peek();
				break;
			}
		case Instruction.LoadLocal:
			{
				var index = frame.baseStackIndex + NextByte(vm, ref frame);
				vm.PushValue(
					vm.valueStack.buffer[index],
					vm.typeStack.buffer[index]
				);
				break;
			}
		case Instruction.LoadLocalMultiple:
			{
				var srcIdx = frame.baseStackIndex + NextByte(vm, ref frame);
				var size = NextByte(vm, ref frame);
				var dstIdx = vm.valueStack.count;

				vm.valueStack.Grow(size);
				while (dstIdx < vm.valueStack.count)
				{
					vm.valueStack.buffer[dstIdx] = vm.valueStack.buffer[srcIdx];
					vm.typeStack.buffer[dstIdx++] = vm.typeStack.buffer[srcIdx++];
				}
				break;
			}
		case Instruction.IncrementLocalInt:
			{
				var index = frame.baseStackIndex + NextByte(vm, ref frame);
				vm.valueStack.buffer[index].asInt += 1;
				break;
			}
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
		case Instruction.JumpForward:
			{
				var offset = BytesHelper.BytesToShort(NextByte(vm, ref frame), NextByte(vm, ref frame));
				frame.codeIndex += offset;
				break;
			}
		case Instruction.JumpBackward:
			{
				var offset = BytesHelper.BytesToShort(NextByte(vm, ref frame), NextByte(vm, ref frame));
				frame.codeIndex -= offset;
				break;
			}
		case Instruction.JumpForwardIfFalse:
			{
				var offset = BytesHelper.BytesToShort(NextByte(vm, ref frame), NextByte(vm, ref frame));
				if (!vm.valueStack.buffer[vm.valueStack.count - 1].asBool)
					frame.codeIndex += offset;
				break;
			}
		case Instruction.JumpForwardIfTrue:
			{
				var offset = BytesHelper.BytesToShort(NextByte(vm, ref frame), NextByte(vm, ref frame));
				if (vm.valueStack.buffer[vm.valueStack.count - 1].asBool)
					frame.codeIndex += offset;
				break;
			}
		case Instruction.PopAndJumpForwardIfFalse:
			{
				var offset = BytesHelper.BytesToShort(NextByte(vm, ref frame), NextByte(vm, ref frame));
				if (!vm.PopValue().asBool)
					frame.codeIndex += offset;
				break;
			}
		case Instruction.ForLoopCheck:
			{
				var index = frame.baseStackIndex + NextByte(vm, ref frame);
				var less = vm.valueStack.buffer[index].asInt < vm.valueStack.buffer[index + 1].asInt;
				vm.PushValue(
					new ValueData(less),
					ValueType.Bool
				);
				break;
			}
		default:
			break;
		}

		return false;
	}
}