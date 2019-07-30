public static class Optimizer
{
	public static void Optimize(ByteCodeChunk chunk)
	{
		var optimized = new Buffer<byte>(chunk.bytes.count);

		for (var i = 0; i < chunk.bytes.count; i++)
		{
			var b = chunk.bytes.buffer[i];
			var op = (Instruction)b;
			var op1 = (Instruction)chunk.bytes.buffer[i + 1];
			var op2 = (Instruction)chunk.bytes.buffer[i + 2];

			switch (op)
			{
			case Instruction.LoadNil:
			case Instruction.LoadTrue:
			case Instruction.LoadFalse:
				if (op1 == Instruction.Pop)
				{
					i += 1;
					continue;
				}
				break;
			case Instruction.LoadLiteral:
			case Instruction.LoadLocal:
				if (op2 == Instruction.Pop)
				{
					i += 2;
					continue;
				}
				break;
			default:
				break;
			}

			optimized.PushBack(b);
		}

		chunk.bytes = optimized;
	}
}