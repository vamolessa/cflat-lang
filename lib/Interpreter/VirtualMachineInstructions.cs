//#define DEBUG_TRACE
using System.Text;

internal static class VirtualMachineInstructions
{
	public static void Run(VirtualMachine vm)
	{
#if DEBUG_TRACE
		var debugSb = new StringBuilder();
#endif

		ref var stackBuffer = ref vm.valueStack;
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

			var nextInstruction = (Instruction)bytes[codeIndex++];
			switch (nextInstruction)
			{
			case Instruction.Halt:
				vm.callframeStack.count -= 1;
				return;
			case Instruction.Call:
				{
					var size = bytes[codeIndex++];
					var stackTop = stackSize - size;
					var functionIndex = stack[stackTop - 1].asInt;
					var function = vm.chunk.functions.buffer[functionIndex];

					vm.callframeStack.PushBackUnchecked(
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
					var stackTop = stackSize - bytes[codeIndex++];
					var functionIndex = stack[stackTop - 1].asInt;
					var function = vm.chunk.nativeFunctions.buffer[functionIndex];

					vm.callframeStack.PushBackUnchecked(
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
					var size = bytes[codeIndex++];
					VirtualMachineHelper.Return(vm, size);
					break;
				}
			case Instruction.Print:
				{
					var type = ValueType.Read(
						bytes[codeIndex++],
						bytes[codeIndex++],
						bytes[codeIndex++],
						bytes[codeIndex++]
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
					var size = bytes[codeIndex++];
					stackSize -= size;
				}
				break;
			case Instruction.Move:
				{
					var sizeUnderMove = bytes[codeIndex++];
					var sizeToMove = bytes[codeIndex++];

					var srcIdx = stackSize - sizeToMove;
					var dstIdx = srcIdx - sizeUnderMove;

					while (srcIdx < stackSize)
						stack[dstIdx++] = stack[srcIdx++];

					stackSize = dstIdx;
				}
				break;
			case Instruction.LoadUnit:
				stackBuffer.PushBackUnchecked(new ValueData());
				break;
			case Instruction.LoadFalse:
				stackBuffer.PushBackUnchecked(new ValueData(false));
				break;
			case Instruction.LoadTrue:
				stackBuffer.PushBackUnchecked(new ValueData(true));
				break;
			case Instruction.LoadLiteral:
				{
					var index = BytesHelper.BytesToShort(bytes[codeIndex++], bytes[codeIndex++]);
					stackBuffer.PushBackUnchecked(vm.chunk.literalData.buffer[index]);
					break;
				}
			case Instruction.LoadFunction:
				{
					var index = BytesHelper.BytesToShort(bytes[codeIndex++], bytes[codeIndex++]);
					stackBuffer.PushBackUnchecked(new ValueData(index));
					break;
				}
			case Instruction.LoadNativeFunction:
				{
					var index = BytesHelper.BytesToShort(bytes[codeIndex++], bytes[codeIndex++]);
					stackBuffer.PushBackUnchecked(new ValueData(index));
					break;
				}
			case Instruction.AssignLocal:
				{
					var index = frame.baseStackIndex + bytes[codeIndex++];
					stack[index] = stack[stackSize - 1];
					break;
				}
			case Instruction.LoadLocal:
				{
					var index = frame.baseStackIndex + bytes[codeIndex++];
					stackBuffer.PushBackUnchecked(stack[index]);
					break;
				}
			case Instruction.AssignLocalMultiple:
				{
					var dstIdx = frame.baseStackIndex + bytes[codeIndex++];
					var size = bytes[codeIndex++];
					var srcIdx = stackSize - size;

					while (srcIdx < stackSize)
						stack[dstIdx++] = stack[srcIdx++];
					break;
				}
			case Instruction.LoadLocalMultiple:
				{
					var srcIdx = frame.baseStackIndex + bytes[codeIndex++];
					var size = bytes[codeIndex++];
					var dstIdx = stackSize;

					stackBuffer.GrowUnchecked(size);
					while (dstIdx < stackSize)
						stack[dstIdx++] = stack[srcIdx++];
					break;
				}
			case Instruction.IncrementLocalInt:
				{
					var index = frame.baseStackIndex + bytes[codeIndex++];
					stack[index].asInt += 1;
					break;
				}
			case Instruction.IntToFloat:
				stack[stackSize - 1] = new ValueData((float)stack[stackSize - 1].asInt);
				break;
			case Instruction.FloatToInt:
				stack[stackSize - 1] = new ValueData((int)stack[stackSize - 1].asFloat);
				break;
			case Instruction.NegateInt:
				stack[stackSize - 1].asInt = -stack[stackSize - 1].asInt;
				break;
			case Instruction.NegateFloat:
				stack[stackSize - 1].asFloat = -stack[stackSize - 1].asFloat;
				break;
			case Instruction.AddInt:
				stack[stackSize - 2].asInt += stack[--stackSize].asInt;
				break;
			case Instruction.AddFloat:
				stack[stackSize - 2].asFloat += stack[--stackSize].asFloat;
				break;
			case Instruction.SubtractInt:
				stack[stackSize - 2].asInt -= stack[--stackSize].asInt;
				break;
			case Instruction.SubtractFloat:
				stack[stackSize - 2].asFloat -= stack[--stackSize].asFloat;
				break;
			case Instruction.MultiplyInt:
				stack[stackSize - 2].asInt *= stack[--stackSize].asInt;
				break;
			case Instruction.MultiplyFloat:
				stack[stackSize - 2].asFloat *= stack[--stackSize].asFloat;
				break;
			case Instruction.DivideInt:
				stack[stackSize - 2].asInt /= stack[--stackSize].asInt;
				break;
			case Instruction.DivideFloat:
				stack[stackSize - 2].asFloat /= stack[--stackSize].asFloat;
				break;
			case Instruction.Not:
				stack[stackSize - 1].asBool = !stack[stackSize - 1].asBool;
				break;
			case Instruction.EqualBool:
				stackBuffer.PushBackUnchecked(new ValueData(stack[--stackSize].asBool == stack[--stackSize].asBool));
				break;
			case Instruction.EqualInt:
				stackBuffer.PushBackUnchecked(new ValueData(stack[--stackSize].asInt == stack[--stackSize].asInt));
				break;
			case Instruction.EqualFloat:
				stackBuffer.PushBackUnchecked(new ValueData(stack[--stackSize].asFloat == stack[--stackSize].asFloat));
				break;
			case Instruction.EqualString:
				stackBuffer.PushBackUnchecked(new ValueData(
						(vm.heap.buffer[stack[--stackSize].asInt] as string).Equals(
						vm.heap.buffer[stack[--stackSize].asInt] as string)
					));
				break;
			case Instruction.GreaterInt:
				stackBuffer.PushBackUnchecked(new ValueData(stack[--stackSize].asInt < stack[--stackSize].asInt));
				break;
			case Instruction.GreaterFloat:
				stackBuffer.PushBackUnchecked(new ValueData(stack[--stackSize].asFloat < stack[--stackSize].asFloat));
				break;
			case Instruction.LessInt:
				stackBuffer.PushBackUnchecked(new ValueData(stack[--stackSize].asInt > stack[--stackSize].asInt));
				break;
			case Instruction.LessFloat:
				stackBuffer.PushBackUnchecked(new ValueData(stack[--stackSize].asFloat > stack[--stackSize].asFloat));
				break;
			case Instruction.JumpForward:
				{
					var offset = BytesHelper.BytesToShort(bytes[codeIndex++], bytes[codeIndex++]);
					codeIndex += offset;
					break;
				}
			case Instruction.JumpBackward:
				{
					var offset = BytesHelper.BytesToShort(bytes[codeIndex++], bytes[codeIndex++]);
					codeIndex -= offset;
					break;
				}
			case Instruction.JumpForwardIfFalse:
				{
					var offset = BytesHelper.BytesToShort(bytes[codeIndex++], bytes[codeIndex++]);
					if (!stack[stackSize - 1].asBool)
						codeIndex += offset;
					break;
				}
			case Instruction.JumpForwardIfTrue:
				{
					var offset = BytesHelper.BytesToShort(bytes[codeIndex++], bytes[codeIndex++]);
					if (stack[stackSize - 1].asBool)
						codeIndex += offset;
					break;
				}
			case Instruction.PopAndJumpForwardIfFalse:
				{
					var offset = BytesHelper.BytesToShort(bytes[codeIndex++], bytes[codeIndex++]);
					if (!stack[--stackSize].asBool)
						codeIndex += offset;
					break;
				}
			case Instruction.ForLoopCheck:
				{
					var index = frame.baseStackIndex + bytes[codeIndex++];
					var less = stack[index].asInt < stack[index + 1].asInt;
					stackBuffer.PushBackUnchecked(new ValueData(less));
					break;
				}
			default:
				break;
			}
		}
	}
}