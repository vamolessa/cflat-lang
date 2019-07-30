public static class Optimizer
{
	public static void Optimize(ByteCodeChunk chunk)
	{
		var optimized = new Buffer<byte>(chunk.bytes.count);

		var opPop = (int)Instruction.Pop;

		for (var i = 0; i < chunk.bytes.count; i++)
		{
			var b = chunk.bytes.buffer[i];
			var op = (Instruction)b;
			switch (op)
			{
			case Instruction.LoadNil:
			case Instruction.LoadTrue:
			case Instruction.LoadFalse:
			case Instruction.LoadLiteral:
			case Instruction.LoadLocal:
				if (chunk.bytes.buffer[i + 1] == opPop)
				{
					i += 1;
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