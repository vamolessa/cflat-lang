//#define DEBUG_TRACE
using System.Text;

internal static class VirtualMachineInstructions
{
	public static void RunTopFunction(VirtualMachine vm)
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
				--vm.callframeStack.count;
				return;
			case Instruction.Call:
				{
					var size = bytes[codeIndex++];
					var stackTop = stackSize - size;
					var functionIndex = stack[stackTop - 1].asInt;
					var function = vm.chunk.functions.buffer[functionIndex];

					vm.callframeStack.PushBackUnchecked(
						new CallFrame(
							function.codeIndex,
							stackTop,
							(ushort)functionIndex,
							CallFrame.Type.Function
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
							0,
							stackTop,
							(ushort)functionIndex,
							CallFrame.Type.NativeFunction
						)
					);

					var context = new RuntimeContext(vm, stackTop);
					function.callback(ref context);
					VirtualMachineHelper.Return(vm, function.returnSize);
					break;
				}
			case Instruction.CallNativeAuto:
				{
					var callIndex = BytesHelper.BytesToUShort(bytes[codeIndex++], bytes[codeIndex++]);
					var call = vm.chunk.nativeCalls.buffer[callIndex];
					var stackTop = stackSize - call.argumentsSize;

					vm.callframeStack.PushBackUnchecked(
						new CallFrame(
							0,
							stackTop + 1,
							0,
							CallFrame.Type.AutoNativeFunction
						)
					);

					var reader = new ReadMarshaler(vm, stackTop);
					var arguments = new object[call.argumentTypes.Length];
					for (var i = 0; i < arguments.Length; i++)
						arguments[i] = Marshal.GetObject(ref reader, call.argumentTypes[i]);

					var result = call.methodInfo.Invoke(null, arguments);
					if (call.returnType.IsKind(TypeKind.Unit))
						stackBuffer.PushBackUnchecked(new ValueData());
					else if (call.returnType.IsKind(TypeKind.Bool) && result is bool b)
						stackBuffer.PushBackUnchecked(new ValueData(b));
					else if (call.returnType.IsKind(TypeKind.Int) && result is int i)
						stackBuffer.PushBackUnchecked(new ValueData(i));
					else if (call.returnType.IsKind(TypeKind.Float) && result is float f)
						stackBuffer.PushBackUnchecked(new ValueData(f));
					else
					{
						stackBuffer.PushBackUnchecked(new ValueData(vm.nativeObjects.count));
						vm.nativeObjects.PushBackUnchecked(result);
					}

					VirtualMachineHelper.Return(vm, call.returnType.GetSize(vm.chunk));
					break;
				}
			case Instruction.Return:
				VirtualMachineHelper.Return(vm, bytes[codeIndex++]);
				break;
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
				--stackSize;
				break;
			case Instruction.PopMultiple:
				stackSize -= bytes[codeIndex++];
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
					var index = BytesHelper.BytesToUShort(bytes[codeIndex++], bytes[codeIndex++]);
					stackBuffer.PushBackUnchecked(vm.chunk.literalData.buffer[index]);
					break;
				}
			case Instruction.LoadFunction:
			case Instruction.LoadNativeFunction:
				{
					var index = BytesHelper.BytesToUShort(bytes[codeIndex++], bytes[codeIndex++]);
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
					++stack[index].asInt;
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
						(vm.nativeObjects.buffer[stack[--stackSize].asInt] as string).Equals(
						vm.nativeObjects.buffer[stack[--stackSize].asInt] as string)
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
					var offset = BytesHelper.BytesToUShort(bytes[codeIndex++], bytes[codeIndex++]);
					codeIndex += offset;
					break;
				}
			case Instruction.JumpBackward:
				{
					var offset = BytesHelper.BytesToUShort(bytes[codeIndex++], bytes[codeIndex++]);
					codeIndex -= offset;
					break;
				}
			case Instruction.JumpForwardIfFalse:
				{
					var offset = BytesHelper.BytesToUShort(bytes[codeIndex++], bytes[codeIndex++]);
					if (!stack[stackSize - 1].asBool)
						codeIndex += offset;
					break;
				}
			case Instruction.JumpForwardIfTrue:
				{
					var offset = BytesHelper.BytesToUShort(bytes[codeIndex++], bytes[codeIndex++]);
					if (stack[stackSize - 1].asBool)
						codeIndex += offset;
					break;
				}
			case Instruction.PopAndJumpForwardIfFalse:
				{
					var offset = BytesHelper.BytesToUShort(bytes[codeIndex++], bytes[codeIndex++]);
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
			case Instruction.DebugPushFrame:
				vm.debugData.frameStack.PushBack(vm.debugData.typeStack.count);
				break;
			case Instruction.DebugPopFrame:
				vm.debugData.typeStack.count = vm.debugData.frameStack.PopLast();
				break;
			case Instruction.DebugPushType:
				{
					var type = ValueType.Read(
						bytes[codeIndex++],
						bytes[codeIndex++],
						bytes[codeIndex++],
						bytes[codeIndex++]
					);

					var totalTypeSize = type.GetSize(vm.chunk);
					for (var i = 0; i < vm.debugData.typeStack.count; i++)
						totalTypeSize += vm.debugData.typeStack.buffer[i].GetSize(vm.chunk);

					for (var i = stackSize - totalTypeSize; i > 0; i--)
						vm.debugData.typeStack.PushBack(new ValueType(TypeKind.Unit));

					vm.debugData.typeStack.PushBack(type);
					break;
				}
			case Instruction.DebugPopType:
				vm.debugData.typeStack.count -= 1;
				break;
			default:
				break;
			}
		}
	}
}