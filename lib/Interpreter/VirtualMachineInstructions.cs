//#define DEBUG_TRACE

using System.Text;

internal static class VirtualMachineInstructions
{
	private static ref ValueData Peek2(ValueData[] stack, int count)
	{
		return ref stack[count - 1];
	}

	private static ref ValueData PeekBefore2(ValueData[] stack, int count)
	{
		return ref stack[count - 2];
	}


	private static byte NextByte(byte[] bytes, ref int codeIndex)
	{
		return bytes[codeIndex++];
	}

	public static void Run(VirtualMachine vm)
	{
#if DEBUG_TRACE
		var debugSb = new StringBuilder();
#endif

		var bytes = vm.chunk.bytes.buffer;
		var stack = vm.valueStack.buffer;
		ref var stackSize = ref vm.valueStack.count;

		while (true)
		{
#if DEBUG_TRACE
			var ip = vm.callframeStack.buffer[vm.callframeStack.count - 1].codeIndex;
			debugSb.Clear();
			VirtualMachineHelper.TraceStack(vm, debugSb);
			vm.chunk.DisassembleInstruction(ip, debugSb);
			System.Console.WriteLine(debugSb);
#endif

			ref var frame = ref vm.callframeStack.buffer[vm.callframeStack.count - 1];
			ref var codeIndex = ref frame.codeIndex;

			var nextInstruction = (Instruction)NextByte(bytes, ref codeIndex);
			switch (nextInstruction)
			{
			case Instruction.Halt:
				vm.callframeStack.count -= 1;
				return;
			case Instruction.Call:
				{
					var size = NextByte(bytes, ref codeIndex);
					var stackTop = stackSize - size;
					var functionIndex = stack[stackTop - 1].asInt;
					var function = vm.chunk.functions.buffer[functionIndex];

					vm.callframeStack.PushBack(
						new CallFrame(
							functionIndex,
							function.codeIndex,
							stackTop
						)
					);
					break;
				}
			case Instruction.CallNative:
				{
					var stackTop = stackSize - NextByte(bytes, ref codeIndex);
					var functionIndex = stack[stackTop - 1].asInt;
					var function = vm.chunk.nativeFunctions.buffer[functionIndex];

					vm.callframeStack.PushBack(
						new CallFrame(
							functionIndex,
							-1,
							stackTop
						)
					);

					var context = new RuntimeContext(vm, stackTop);
					function.callback(ref context);
					VirtualMachineHelper.Return(vm, function.returnSize);
					break;
				}
			case Instruction.Return:
				{
					var size = NextByte(bytes, ref codeIndex);
					VirtualMachineHelper.Return(vm, size);
					break;
				}
			case Instruction.Print:
				{
					var type = ValueType.Read(
						NextByte(bytes, ref codeIndex),
						NextByte(bytes, ref codeIndex),
						NextByte(bytes, ref codeIndex),
						NextByte(bytes, ref codeIndex)
					);

					var sb = new StringBuilder();
					var size = type.GetSize(vm.chunk);

					VirtualMachineHelper.ValueToString(
						vm,
						stackSize - size,
						type,
						sb
					);
					stackSize -= size;

					System.Console.WriteLine(sb);
					break;
				}
			case Instruction.Pop:
				stackSize -= 1;
				break;
			case Instruction.PopMultiple:
				{
					var size = NextByte(bytes, ref codeIndex);
					stackSize -= size;
				}
				break;
			case Instruction.Move:
				{
					var sizeUnderMove = NextByte(bytes, ref codeIndex);
					var sizeToMove = NextByte(bytes, ref codeIndex);

					var srcIdx = stackSize - sizeToMove;
					var dstIdx = srcIdx - sizeUnderMove;

					while (srcIdx < stackSize)
						stack[dstIdx++] = stack[srcIdx++];

					stackSize = dstIdx;
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
					var index = BytesHelper.BytesToShort(NextByte(bytes, ref codeIndex), NextByte(bytes, ref codeIndex));
					vm.valueStack.PushBack(vm.chunk.literalData.buffer[index]);
					break;
				}
			case Instruction.LoadFunction:
				{
					var index = BytesHelper.BytesToShort(NextByte(bytes, ref codeIndex), NextByte(bytes, ref codeIndex));
					vm.valueStack.PushBack(new ValueData(index));
					break;
				}
			case Instruction.LoadNativeFunction:
				{
					var index = BytesHelper.BytesToShort(NextByte(bytes, ref codeIndex), NextByte(bytes, ref codeIndex));
					vm.valueStack.PushBack(new ValueData(index));
					break;
				}
			case Instruction.AssignLocal:
				{
					var index = frame.baseStackIndex + NextByte(bytes, ref codeIndex);
					stack[index] = stack[stackSize - 1];
					break;
				}
			case Instruction.LoadLocal:
				{
					var index = frame.baseStackIndex + NextByte(bytes, ref codeIndex);
					vm.valueStack.PushBack(stack[index]);
					break;
				}
			case Instruction.AssignLocalMultiple:
				{
					var dstIdx = frame.baseStackIndex + NextByte(bytes, ref codeIndex);
					var size = NextByte(bytes, ref codeIndex);
					var srcIdx = stackSize - size;

					while (srcIdx < stackSize)
						stack[dstIdx++] = stack[srcIdx++];
					break;
				}
			case Instruction.LoadLocalMultiple:
				{
					var srcIdx = frame.baseStackIndex + NextByte(bytes, ref codeIndex);
					var size = NextByte(bytes, ref codeIndex);
					var dstIdx = stackSize;

					vm.valueStack.Grow(size);
					while (dstIdx < stackSize)
						stack[dstIdx++] = stack[srcIdx++];
					break;
				}
			case Instruction.IncrementLocalInt:
				{
					var index = frame.baseStackIndex + NextByte(bytes, ref codeIndex);
					stack[index].asInt += 1;
					break;
				}
			case Instruction.IntToFloat:
				vm.valueStack.PushBack(new ValueData((float)vm.valueStack.PopLast().asInt));
				break;
			case Instruction.FloatToInt:
				vm.valueStack.PushBack(new ValueData((int)vm.valueStack.PopLast().asFloat));
				break;
			case Instruction.NegateInt:
				stack[stackSize - 1].asInt = -stack[stackSize - 1].asInt;
				break;
			case Instruction.NegateFloat:
				stack[stackSize - 1].asFloat = -stack[stackSize - 1].asFloat;
				break;
			case Instruction.AddInt:
				stack[stackSize - 2].asInt += vm.valueStack.PopLast().asInt;
				break;
			case Instruction.AddFloat:
				stack[stackSize - 2].asFloat += vm.valueStack.PopLast().asFloat;
				break;
			case Instruction.SubtractInt:
				stack[stackSize - 2].asInt -= vm.valueStack.PopLast().asInt;
				break;
			case Instruction.SubtractFloat:
				stack[stackSize - 2].asFloat -= vm.valueStack.PopLast().asFloat;
				break;
			case Instruction.MultiplyInt:
				stack[stackSize - 2].asInt *= vm.valueStack.PopLast().asInt;
				break;
			case Instruction.MultiplyFloat:
				stack[stackSize - 2].asFloat *= vm.valueStack.PopLast().asFloat;
				break;
			case Instruction.DivideInt:
				stack[stackSize - 2].asInt /= vm.valueStack.PopLast().asInt;
				break;
			case Instruction.DivideFloat:
				stack[stackSize - 2].asFloat /= vm.valueStack.PopLast().asFloat;
				break;
			case Instruction.Not:
				stack[stackSize - 1].asBool = !stack[stackSize - 1].asBool;
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
					var offset = BytesHelper.BytesToShort(NextByte(bytes, ref codeIndex), NextByte(bytes, ref codeIndex));
					codeIndex += offset;
					break;
				}
			case Instruction.JumpBackward:
				{
					var offset = BytesHelper.BytesToShort(NextByte(bytes, ref codeIndex), NextByte(bytes, ref codeIndex));
					codeIndex -= offset;
					break;
				}
			case Instruction.JumpForwardIfFalse:
				{
					var offset = BytesHelper.BytesToShort(NextByte(bytes, ref codeIndex), NextByte(bytes, ref codeIndex));
					if (!stack[stackSize - 1].asBool)
						codeIndex += offset;
					break;
				}
			case Instruction.JumpForwardIfTrue:
				{
					var offset = BytesHelper.BytesToShort(NextByte(bytes, ref codeIndex), NextByte(bytes, ref codeIndex));
					if (stack[stackSize - 1].asBool)
						codeIndex += offset;
					break;
				}
			case Instruction.PopAndJumpForwardIfFalse:
				{
					var offset = BytesHelper.BytesToShort(NextByte(bytes, ref codeIndex), NextByte(bytes, ref codeIndex));
					if (!vm.valueStack.PopLast().asBool)
						codeIndex += offset;
					break;
				}
			case Instruction.ForLoopCheck:
				{
					var index = frame.baseStackIndex + NextByte(bytes, ref codeIndex);
					var less = stack[index].asInt < stack[index + 1].asInt;
					vm.valueStack.PushBack(new ValueData(less));
					break;
				}
			default:
				break;
			}
		}
	}
}