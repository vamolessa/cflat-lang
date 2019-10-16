#define DEBUG_TRACE
using System.Text;

internal static class VirtualMachineInstructions
{
	public static void RunTopFunction(VirtualMachine vm)
	{
#if DEBUG_TRACE
		var debugSb = new StringBuilder();
#endif

		ref var stackBuffer = ref vm.valueStack;
		var stack = vm.valueStack.buffer;
		ref var stackSize = ref vm.valueStack.count;

		while (true)
		{
			ref var frame = ref vm.callframeStack.buffer[vm.callframeStack.count - 1];
			ref var codeIndex = ref frame.codeIndex;
			var bytes = vm.linking.chunks.buffer[frame.chunkIndex].bytes.buffer;

#if DEBUG_TRACE
			{
				var ip = vm.callframeStack.buffer[vm.callframeStack.count - 1].codeIndex;
				debugSb.Clear();
				VirtualMachineHelper.TraceStack(vm, debugSb);
				vm.linking.chunks.buffer[frame.chunkIndex].DisassembleInstruction(ip, debugSb);
				System.Console.WriteLine(debugSb);
			}
#endif

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
					var function = vm.linking.chunks.buffer[frame.chunkIndex].functions.buffer[functionIndex];

					vm.callframeStack.PushBackUnchecked(
						new CallFrame(
							frame.chunkIndex,
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
					var function = vm.linking.chunks.buffer[frame.chunkIndex].nativeFunctions.buffer[functionIndex];

					vm.callframeStack.PushBackUnchecked(
						new CallFrame(
							0,
							0,
							stackTop,
							(ushort)functionIndex,
							CallFrame.Type.NativeFunction
						)
					);

					try
					{
						var context = new RuntimeContext(vm, stackTop);
						function.callback(ref context);
						VirtualMachineHelper.Return(vm, function.returnSize);
					}
					catch (System.Exception e)
					{
						vm.Error(e.ToString());
						return;
					}
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
					var size = type.GetSize(vm.linking.chunks.buffer[type.chunkIndex]);

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
					stackBuffer.PushBackUnchecked(vm.linking.chunks.buffer[frame.chunkIndex].literalData.buffer[index]);
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
					stack[index] = stack[--stackSize];
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

					var endIndex = stackSize;
					stackSize -= bytes[codeIndex++];
					var srcIdx = stackSize;

					while (srcIdx < endIndex)
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
			case Instruction.CreateArray:
				{
					var elementSize = bytes[codeIndex++];
					var arrayLength = stack[--stackSize].asInt;
					if (arrayLength < 0)
					{
						vm.Error("Negative array length");
						return;
					}

					stackSize -= elementSize;

					var heapStartIndex = vm.valueHeap.count;
					var arraySize = elementSize * arrayLength;
					vm.valueHeap.GrowUnchecked(arraySize + 1);
					vm.valueHeap.buffer[heapStartIndex++] = new ValueData(arrayLength);

					for (var i = 0; i < arraySize; i += elementSize)
					{
						for (var j = 0; j < elementSize; j++)
							vm.valueHeap.buffer[heapStartIndex + i + j] = stack[stackSize + j];
					}

					stack[stackSize++] = new ValueData(heapStartIndex);
					break;
				}
			case Instruction.LoadArrayLength:
				stack[stackSize - 1] = vm.valueHeap.buffer[stack[stackSize - 1].asInt - 1];
				break;
			case Instruction.AssignArrayElement:
				{
					var size = bytes[codeIndex++];

					stackSize -= size;
					var stackStartIndex = stackSize;

					var index = stack[--stackSize].asInt;
					var heapStartIndex = stack[--stackSize].asInt;

					var length = vm.valueHeap.buffer[heapStartIndex - 1].asInt;
					if (index < 0 || index >= length)
					{
						vm.Error(string.Format("Index out of bounds. Index: {0}. Array length: {1}", index, length));
						return;
					}

					heapStartIndex += index * size;

					for (var i = 0; i < size; i++)
						vm.valueHeap.buffer[heapStartIndex + i] = stack[stackStartIndex + i];
					break;
				}
			case Instruction.LoadArrayElement:
				{
					var size = bytes[codeIndex++];

					var index = stack[--stackSize].asInt;
					var heapStartIndex = stack[--stackSize].asInt;

					var length = vm.valueHeap.buffer[heapStartIndex - 1].asInt;
					if (index < 0 || index >= length)
					{
						vm.Error(string.Format("Index out of bounds. Index: {0}. Array length: {1}", index, length));
						return;
					}

					heapStartIndex += index * size;
					var stackStartIndex = stackSize;
					stackBuffer.GrowUnchecked(size);

					for (var i = 0; i < size; i++)
						stack[stackStartIndex + i] = vm.valueHeap.buffer[heapStartIndex + i];
					break;
				}
			case Instruction.AssignArrayElementField:
				{
					var elementSize = bytes[codeIndex++];
					var fieldSize = bytes[codeIndex++];
					var offset = bytes[codeIndex++];

					stackSize -= fieldSize;
					var stackStartIndex = stackSize;

					var index = stack[--stackSize].asInt;
					var heapStartIndex = stack[--stackSize].asInt;

					var length = vm.valueHeap.buffer[heapStartIndex - 1].asInt;
					if (index < 0 || index >= length)
					{
						vm.Error(string.Format("Index out of bounds. Index: {0}. Array length: {1}", index, length));
						return;
					}

					heapStartIndex += index * elementSize + offset;

					for (var i = 0; i < fieldSize; i++)
						vm.valueHeap.buffer[heapStartIndex + i] = stack[stackStartIndex + i];
					break;
				}
			case Instruction.LoadArrayElementField:
				{
					var elementSize = bytes[codeIndex++];
					var fieldSize = bytes[codeIndex++];
					var offset = bytes[codeIndex++];

					var index = stack[--stackSize].asInt;
					var heapStartIndex = stack[--stackSize].asInt;

					var length = vm.valueHeap.buffer[heapStartIndex - 1].asInt;
					if (index < 0 || index >= length)
					{
						vm.Error(string.Format("Index out of bounds. Index: {0}. Array length: {1}", index, length));
						return;
					}

					heapStartIndex += index * elementSize + offset;
					var stackStartIndex = stackSize;
					stackBuffer.GrowUnchecked(fieldSize);

					for (var i = 0; i < fieldSize; i++)
						stack[stackStartIndex + i] = vm.valueHeap.buffer[heapStartIndex + i];
					break;
				}
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
			case Instruction.RepeatLoopCheck:
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

					vm.debugData.typeStack.PushBack(type);
					break;
				}
			case Instruction.DebugPopType:
				vm.debugData.typeStack.count -= 1;
				break;
			case Instruction.DebugPopTypeMultiple:
				vm.debugData.typeStack.count -= bytes[codeIndex++];
				break;
			default:
				break;
			}
		}
	}
}