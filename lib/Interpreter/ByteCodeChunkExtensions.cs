using System.Text;

public static class ByteCodeChunkExtensions
{
	public static void Disassemble(this ByteCodeChunk self, string chunkName, StringBuilder sb)
	{
		sb.Append("== ");
		sb.Append(chunkName);
		sb.Append(" ==");

		for (var index = 0; index < self.bytes.count;)
			index = DisassembleInstruction(self, index, sb);
	}

	public static int DisassembleInstruction(this ByteCodeChunk self, int index, StringBuilder sb)
	{
		sb.AppendFormat("{0000:0}", index);
		if (index > 0 && self.positions.buffer[index].line == self.positions.buffer[index - 1].line)
			sb.Append("   | ");
		else
			sb.AppendFormat("{####:0} ", self.positions.buffer[index].line);

		var instructionCode = self.bytes.buffer[index];
		var instruction = (Instruction)instructionCode;

		switch (instruction)
		{
		case Instruction.Return:
			return SimpleInstruction(instruction, index, sb);
		case Instruction.LoadConstant:
			return LoadConstantInstruction(self, instruction, index, sb);
		default:
			sb.AppendFormat("Unknown opcode {0}\n", instructionCode);
			return index + 1;
		}
	}

	private static int SimpleInstruction(Instruction instruction, int offset, StringBuilder sb)
	{
		sb.AppendLine(instruction.ToString());
		return offset + 1;
	}

	private static int LoadConstantInstruction(ByteCodeChunk chunk, Instruction instruction, int offset, StringBuilder sb)
	{
		var constant = chunk.constants.buffer[offset + 1];
		sb.AppendFormat("{0} '{1}'\n", instruction, constant);
		return offset + 2;
	}
}
