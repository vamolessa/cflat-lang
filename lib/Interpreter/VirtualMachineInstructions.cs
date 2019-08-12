using System.Text;

internal static class VirtualMachineInstructions
{
	private static void PushValue(VirtualMachine vm, ValueData value, ValueType type)
	{
		vm.typeStack.PushBack(type);
		vm.valueStack.PushBack(value);
	}

	private static ValueData PopValue(VirtualMachine vm)
	{
		vm.typeStack.count -= 1;
		return vm.valueStack.PopLast();
	}

	private static ref ValueData Peek(VirtualMachine vm)
	{
		return ref vm.valueStack.buffer[vm.valueStack.count - 1];
	}

	private static ref ValueData PeekBefore(VirtualMachine vm)
	{
		return ref vm.valueStack.buffer[vm.valueStack.count - 2];
	}

	private static ValueType PeekType(VirtualMachine vm)
	{
		return vm.typeStack.buffer[vm.typeStack.count - 1];
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
				var size = NextByte(vm, ref frame);
				var stackTop = vm.valueStack.count - size;
				var functionIndex = vm.valueStack.buffer[stackTop - 1].asInt;
				var function = vm.chunk.nativeFunctions.buffer[functionIndex];

				vm.callframeStack.PushBack(
					new VirtualMachine.CallFrame(
						functionIndex,
						-1,
						stackTop
					)
				);
				var returnSize = function.callback(vm);
				VirtualMachineHelper.Return(vm, returnSize);
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
				var size = vm.chunk.GetTypeSize(type);

				VirtualMachineHelper.ValueToString(
					vm,
					vm.valueStack.count - size,
					type,
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
				var size = NextByte(vm, ref frame);
				vm.valueStack.count -= size;
				vm.typeStack.count -= size;
			}
			break;
		case Instruction.Move:
			{
				var sizeUnderMove = NextByte(vm, ref frame);
				var sizeToMove = NextByte(vm, ref frame);

				var srcIdx = vm.valueStack.count - sizeToMove;
				var dstIdx = srcIdx - sizeUnderMove;

				while (srcIdx < vm.valueStack.count)
				{
					vm.valueStack.buffer[dstIdx] = vm.valueStack.buffer[srcIdx];
					vm.typeStack.buffer[dstIdx++] = vm.typeStack.buffer[srcIdx++];
				}

				vm.valueStack.count = dstIdx;
				vm.typeStack.count = dstIdx;
			}
			break;
		case Instruction.LoadUnit:
			PushValue(vm, new ValueData(), new ValueType(TypeKind.Unit));
			break;
		case Instruction.LoadFalse:
			PushValue(vm, new ValueData(false), new ValueType(TypeKind.Bool));
			break;
		case Instruction.LoadTrue:
			PushValue(vm, new ValueData(true), new ValueType(TypeKind.Bool));
			break;
		case Instruction.LoadLiteral:
			{
				var index = BytesHelper.BytesToShort(NextByte(vm, ref frame), NextByte(vm, ref frame));
				PushValue(
					vm,
					vm.chunk.literalData.buffer[index],
					new ValueType(vm.chunk.literalKinds.buffer[index])
				);
				break;
			}
		case Instruction.LoadFunction:
			{
				var index = BytesHelper.BytesToShort(NextByte(vm, ref frame), NextByte(vm, ref frame));
				var typeIndex = vm.chunk.functions.buffer[index].typeIndex;
				PushValue(
					vm,
					new ValueData(index),
					new ValueType(TypeKind.Function, typeIndex)
				);
				break;
			}
		case Instruction.LoadNativeFunction:
			{
				var index = BytesHelper.BytesToShort(NextByte(vm, ref frame), NextByte(vm, ref frame));
				var typeIndex = vm.chunk.nativeFunctions.buffer[index].typeIndex;
				PushValue(
					vm,
					new ValueData(index),
					new ValueType(TypeKind.NativeFunction, typeIndex)
				);
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
				PushValue(vm, vm.valueStack.buffer[index], vm.typeStack.buffer[index]);
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
				vm.typeStack.Grow(size);
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
			PushValue(vm, new ValueData((float)PopValue(vm).asInt), new ValueType(TypeKind.Float));
			break;
		case Instruction.FloatToInt:
			PushValue(vm, new ValueData((int)PopValue(vm).asFloat), new ValueType(TypeKind.Int));
			break;
		case Instruction.NegateInt:
			Peek(vm).asInt = -Peek(vm).asInt;
			break;
		case Instruction.NegateFloat:
			Peek(vm).asFloat = -Peek(vm).asFloat;
			break;
		case Instruction.AddInt:
			PeekBefore(vm).asInt += PopValue(vm).asInt;
			break;
		case Instruction.AddFloat:
			PeekBefore(vm).asFloat += PopValue(vm).asFloat;
			break;
		case Instruction.SubtractInt:
			PeekBefore(vm).asInt -= PopValue(vm).asInt;
			break;
		case Instruction.SubtractFloat:
			PeekBefore(vm).asFloat -= PopValue(vm).asFloat;
			break;
		case Instruction.MultiplyInt:
			PeekBefore(vm).asInt *= PopValue(vm).asInt;
			break;
		case Instruction.MultiplyFloat:
			PeekBefore(vm).asFloat *= PopValue(vm).asFloat;
			break;
		case Instruction.DivideInt:
			PeekBefore(vm).asInt /= PopValue(vm).asInt;
			break;
		case Instruction.DivideFloat:
			PeekBefore(vm).asFloat /= PopValue(vm).asFloat;
			break;
		case Instruction.Not:
			Peek(vm).asBool = !Peek(vm).asBool;
			break;
		case Instruction.EqualBool:
			PushValue(
				vm,
				new ValueData(PopValue(vm).asBool == PopValue(vm).asBool),
				new ValueType(TypeKind.Bool)
			);
			break;
		case Instruction.EqualInt:
			PushValue(
				vm,
				new ValueData(PopValue(vm).asInt == PopValue(vm).asInt),
				new ValueType(TypeKind.Bool)
			);
			break;
		case Instruction.EqualFloat:
			PushValue(
				vm,
				new ValueData(PopValue(vm).asFloat == PopValue(vm).asFloat),
				new ValueType(TypeKind.Bool)
			);
			break;
		case Instruction.EqualString:
			PushValue(
				vm,
				new ValueData(
					(vm.heap.buffer[PopValue(vm).asInt] as string).Equals(
					vm.heap.buffer[PopValue(vm).asInt] as string)
				),
				new ValueType(TypeKind.Bool)
			);
			break;
		case Instruction.GreaterInt:
			PushValue(
				vm,
				new ValueData(PopValue(vm).asInt < PopValue(vm).asInt),
				new ValueType(TypeKind.Bool)
			);
			break;
		case Instruction.GreaterFloat:
			PushValue(
				vm,
				new ValueData(PopValue(vm).asFloat < PopValue(vm).asFloat),
				new ValueType(TypeKind.Bool)
			);
			break;
		case Instruction.LessInt:
			PushValue(
				vm,
				new ValueData(PopValue(vm).asInt > PopValue(vm).asInt),
				new ValueType(TypeKind.Bool)
			);
			break;
		case Instruction.LessFloat:
			PushValue(
				vm,
				new ValueData(PopValue(vm).asFloat > PopValue(vm).asFloat),
				new ValueType(TypeKind.Bool)
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
				if (!PopValue(vm).asBool)
					frame.codeIndex += offset;
				break;
			}
		case Instruction.ForLoopCheck:
			{
				var index = frame.baseStackIndex + NextByte(vm, ref frame);
				var less = vm.valueStack.buffer[index].asInt < vm.valueStack.buffer[index + 1].asInt;
				PushValue(vm, new ValueData(less), new ValueType(TypeKind.Bool));
				break;
			}
		default:
			break;
		}

		return false;
	}
}