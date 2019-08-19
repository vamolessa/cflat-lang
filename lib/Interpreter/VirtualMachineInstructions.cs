using System.Text;

internal static class VirtualMachineInstructions
{
	private static ref ValueData Peek(VirtualMachine vm)
	{
		return ref vm.valueStack.buffer[vm.valueStack.count - 1];
	}

	private static ref ValueData PeekBefore(VirtualMachine vm)
	{
		return ref vm.valueStack.buffer[vm.valueStack.count - 2];
	}

	private static byte NextByte(VirtualMachine vm, ref VirtualMachine.CallFrame frame)
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
				var size = NextByte(vm, ref frame);
				var stackTop = vm.valueStack.count - size;
				var functionIndex = vm.valueStack.buffer[stackTop - 1].asInt;
				var function = vm.chunk.functions.buffer[functionIndex];

				vm.callframeStack.PushBack(
					new VirtualMachine.CallFrame(
						functionIndex,
						function.codeIndex,
						stackTop
					)
				);
				break;
			}
		case Instruction.CallNative:
			{
				var stackTop = vm.valueStack.count - NextByte(vm, ref frame);
				var functionIndex = vm.valueStack.buffer[stackTop - 1].asInt;
				var function = vm.chunk.nativeFunctions.buffer[functionIndex];

				vm.callframeStack.PushBack(
					new VirtualMachine.CallFrame(
						functionIndex,
						-1,
						stackTop
					)
				);

				function.callback(new RuntimeContext(
					vm,
					vm.callframeStack.buffer[vm.callframeStack.count - 1].baseStackIndex
				));
				VirtualMachineHelper.Return(vm, function.returnSize);
				break;
			}
		case Instruction.Return:
			{
				var size = NextByte(vm, ref frame);
				VirtualMachineHelper.Return(vm, size);
				if (vm.callframeStack.count == 0)
					return true;
				break;
			}
		case Instruction.Print:
			{
				var type = ValueType.Read(
					NextByte(vm, ref frame),
					NextByte(vm, ref frame),
					NextByte(vm, ref frame),
					NextByte(vm, ref frame)
				);

				var sb = new StringBuilder();
				var size = type.GetSize(vm.chunk);

				VirtualMachineHelper.ValueToString(
					vm,
					vm.valueStack.count - size,
					type,
					sb
				);
				vm.valueStack.count -= size;

				System.Console.WriteLine(sb);
				break;
			}
		case Instruction.Pop:
			vm.valueStack.count -= 1;
			break;
		case Instruction.PopMultiple:
			{
				var size = NextByte(vm, ref frame);
				vm.valueStack.count -= size;
			}
			break;
		case Instruction.Move:
			{
				var sizeUnderMove = NextByte(vm, ref frame);
				var sizeToMove = NextByte(vm, ref frame);

				var srcIdx = vm.valueStack.count - sizeToMove;
				var dstIdx = srcIdx - sizeUnderMove;

				while (srcIdx < vm.valueStack.count)
					vm.valueStack.buffer[dstIdx++] = vm.valueStack.buffer[srcIdx++];

				vm.valueStack.count = dstIdx;
			}
			break;
		case Instruction.LoadUnit:
			vm.valueStack.PushBack(new ValueData());
			break;
		case Instruction.LoadFalse:
			vm.valueStack.PushBack(new ValueData(false));
			break;
		case Instruction.LoadTrue:
			vm.valueStack.PushBack(new ValueData(true));
			break;
		case Instruction.LoadLiteral:
			{
				var index = BytesHelper.BytesToShort(NextByte(vm, ref frame), NextByte(vm, ref frame));
				vm.valueStack.PushBack(vm.chunk.literalData.buffer[index]);
				break;
			}
		case Instruction.LoadFunction:
			{
				var index = BytesHelper.BytesToShort(NextByte(vm, ref frame), NextByte(vm, ref frame));
				vm.valueStack.PushBack(new ValueData(index));
				break;
			}
		case Instruction.LoadNativeFunction:
			{
				var index = BytesHelper.BytesToShort(NextByte(vm, ref frame), NextByte(vm, ref frame));
				vm.valueStack.PushBack(new ValueData(index));
				break;
			}
		case Instruction.AssignLocal:
			{
				var index = frame.baseStackIndex + NextByte(vm, ref frame);
				vm.valueStack.buffer[index] = Peek(vm);
				break;
			}
		case Instruction.LoadLocal:
			{
				var index = frame.baseStackIndex + NextByte(vm, ref frame);
				vm.valueStack.PushBack(vm.valueStack.buffer[index]);
				break;
			}
		case Instruction.AssignLocalMultiple:
			{
				var dstIdx = frame.baseStackIndex + NextByte(vm, ref frame);
				var size = NextByte(vm, ref frame);
				var srcIdx = vm.valueStack.count - size;

				while (srcIdx < vm.valueStack.count)
					vm.valueStack.buffer[dstIdx++] = vm.valueStack.buffer[srcIdx++];
				break;
			}
		case Instruction.LoadLocalMultiple:
			{
				var srcIdx = frame.baseStackIndex + NextByte(vm, ref frame);
				var size = NextByte(vm, ref frame);
				var dstIdx = vm.valueStack.count;

				vm.valueStack.Grow(size);
				while (dstIdx < vm.valueStack.count)
					vm.valueStack.buffer[dstIdx++] = vm.valueStack.buffer[srcIdx++];
				break;
			}
		case Instruction.IncrementLocalInt:
			{
				var index = frame.baseStackIndex + NextByte(vm, ref frame);
				vm.valueStack.buffer[index].asInt += 1;
				break;
			}
		case Instruction.IntToFloat:
			vm.valueStack.PushBack(new ValueData((float)vm.valueStack.PopLast().asInt));
			break;
		case Instruction.FloatToInt:
			vm.valueStack.PushBack(new ValueData((int)vm.valueStack.PopLast().asFloat));
			break;
		case Instruction.NegateInt:
			Peek(vm).asInt = -Peek(vm).asInt;
			break;
		case Instruction.NegateFloat:
			Peek(vm).asFloat = -Peek(vm).asFloat;
			break;
		case Instruction.AddInt:
			PeekBefore(vm).asInt += vm.valueStack.PopLast().asInt;
			break;
		case Instruction.AddFloat:
			PeekBefore(vm).asFloat += vm.valueStack.PopLast().asFloat;
			break;
		case Instruction.SubtractInt:
			PeekBefore(vm).asInt -= vm.valueStack.PopLast().asInt;
			break;
		case Instruction.SubtractFloat:
			PeekBefore(vm).asFloat -= vm.valueStack.PopLast().asFloat;
			break;
		case Instruction.MultiplyInt:
			PeekBefore(vm).asInt *= vm.valueStack.PopLast().asInt;
			break;
		case Instruction.MultiplyFloat:
			PeekBefore(vm).asFloat *= vm.valueStack.PopLast().asFloat;
			break;
		case Instruction.DivideInt:
			PeekBefore(vm).asInt /= vm.valueStack.PopLast().asInt;
			break;
		case Instruction.DivideFloat:
			PeekBefore(vm).asFloat /= vm.valueStack.PopLast().asFloat;
			break;
		case Instruction.Not:
			Peek(vm).asBool = !Peek(vm).asBool;
			break;
		case Instruction.EqualBool:
			vm.valueStack.PushBack(new ValueData(vm.valueStack.PopLast().asBool == vm.valueStack.PopLast().asBool));
			break;
		case Instruction.EqualInt:
			vm.valueStack.PushBack(new ValueData(vm.valueStack.PopLast().asInt == vm.valueStack.PopLast().asInt));
			break;
		case Instruction.EqualFloat:
			vm.valueStack.PushBack(new ValueData(vm.valueStack.PopLast().asFloat == vm.valueStack.PopLast().asFloat));
			break;
		case Instruction.EqualString:
			vm.valueStack.PushBack(new ValueData(
					(vm.heap.buffer[vm.valueStack.PopLast().asInt] as string).Equals(
					vm.heap.buffer[vm.valueStack.PopLast().asInt] as string)
				));
			break;
		case Instruction.GreaterInt:
			vm.valueStack.PushBack(new ValueData(vm.valueStack.PopLast().asInt < vm.valueStack.PopLast().asInt));
			break;
		case Instruction.GreaterFloat:
			vm.valueStack.PushBack(new ValueData(vm.valueStack.PopLast().asFloat < vm.valueStack.PopLast().asFloat));
			break;
		case Instruction.LessInt:
			vm.valueStack.PushBack(new ValueData(vm.valueStack.PopLast().asInt > vm.valueStack.PopLast().asInt));
			break;
		case Instruction.LessFloat:
			vm.valueStack.PushBack(new ValueData(vm.valueStack.PopLast().asFloat > vm.valueStack.PopLast().asFloat));
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
				if (!vm.valueStack.PopLast().asBool)
					frame.codeIndex += offset;
				break;
			}
		case Instruction.ForLoopCheck:
			{
				var index = frame.baseStackIndex + NextByte(vm, ref frame);
				var less = vm.valueStack.buffer[index].asInt < vm.valueStack.buffer[index + 1].asInt;
				vm.valueStack.PushBack(new ValueData(less));
				break;
			}
		default:
			break;
		}

		return false;
	}
}