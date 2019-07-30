public static class Optimizer
{
	private static Instruction PeekOp(ByteCodeChunk chunk, int index)
	{
		return (Instruction)chunk.bytes.buffer[index++];
	}

	private static Instruction NextOp(ByteCodeChunk chunk, ref int index)
	{
		return (Instruction)chunk.bytes.buffer[index++];
	}

	private static byte NextByte(ByteCodeChunk chunk, ref int index)
	{
		return chunk.bytes.buffer[index++];
	}

	public static void Optimize(ByteCodeChunk chunk)
	{
		var optimized = new Buffer<byte>(chunk.bytes.count);

		for (var i = 0; i < chunk.bytes.count;)
		{
			var op = NextOp(chunk, ref i);
			switch (op)
			{
			case Instruction.Halt:
			case Instruction.Return:
			case Instruction.Pop:
			case Instruction.IntToFloat:
			case Instruction.FloatToInt:
			case Instruction.NegateInt:
			case Instruction.NegateFloat:
			case Instruction.AddInt:
			case Instruction.AddFloat:
			case Instruction.SubtractInt:
			case Instruction.SubtractFloat:
			case Instruction.MultiplyInt:
			case Instruction.MultiplyFloat:
			case Instruction.DivideInt:
			case Instruction.DivideFloat:
			case Instruction.Not:
			case Instruction.EqualBool:
			case Instruction.EqualInt:
			case Instruction.EqualFloat:
			case Instruction.EqualString:
			case Instruction.GreaterInt:
			case Instruction.GreaterFloat:
			case Instruction.LessInt:
			case Instruction.LessFloat:
			case Instruction.LoadTrue:
			case Instruction.LoadFalse:
				optimized.PushBack((byte)op);
				break;
			case Instruction.Print:
			case Instruction.PopMultiple:
			case Instruction.CopyTo:
			case Instruction.AssignLocal:
			case Instruction.LoadLiteral:
			case Instruction.LoadLocal:
				optimized.PushBack((byte)op);
				optimized.PushBack(NextByte(chunk, ref i));
				break;
			case Instruction.LoadNil:
				if (PeekOp(chunk, i) == Instruction.Pop)
					i += 1;
				else
					optimized.PushBack((byte)op);
				break;
			default:
				break;
			}
		}

		chunk.bytes = optimized;
	}
}