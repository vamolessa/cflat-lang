using System.Text;

public static class ByteCodeChunkExtensions
{
	public static void Disassemble(this ByteCodeChunk self, string chunkName, StringBuilder sb)
	{
		sb.Append("== ");
		sb.Append(chunkName);
		sb.Append(" [");
		sb.Append(self.bytes.count);
		sb.AppendLine(" bytes] ==");

		for (var index = 0; index < self.bytes.count;)
			index = DisassembleInstruction(self, index, sb);
	}

	public static int DisassembleInstruction(this ByteCodeChunk self, int index, StringBuilder sb)
	{
		sb.AppendFormat("{0:0000} ", index);
		if (index > 0 && self.positions.buffer[index].line == self.positions.buffer[index - 1].line)
			sb.Append("   | ");
		else
			sb.AppendFormat("{0,4} ", self.positions.buffer[index].line);

		var instructionCode = self.bytes.buffer[index];
		var instruction = (Instruction)instructionCode;

		switch (instruction)
		{
		case Instruction.Return:
		case Instruction.Negate:
			return SimpleInstruction(instruction, index, sb);
		case Instruction.LoadConstant:
			return LoadConstantInstruction(self, instruction, index, sb);
		default:
			sb.AppendFormat("Unknown instruction '{0}'\n", instructionCode);
			return index + 1;
		}
	}

	private static int SimpleInstruction(Instruction instruction, int index, StringBuilder sb)
	{
		sb.AppendLine(instruction.ToString());
		return index + 1;
	}

	private static int LoadConstantInstruction(ByteCodeChunk chunk, Instruction instruction, int index, StringBuilder sb)
	{
		var constantIndex = chunk.bytes.buffer[index + 1];
		var constant = chunk.constants.buffer[constantIndex];
		sb.AppendFormat("{0} '{1}'\n", instruction, constant);
		return index + 2;
	}
}
