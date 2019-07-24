using System.Text;

public static class ByteCodeChunkExtensions
{
	public static void Disassemble(this ByteCodeChunk self, string chunkName, StringBuilder sb)
	{
		sb.Append("== ");
		sb.Append(chunkName);
		sb.Append(" ==");

		for (var offset = 0; offset < self.instructions.count;)
			offset = DisassembleInstruction(self, offset, sb);
	}

	private static int DisassembleInstruction(this ByteCodeChunk self, int offset, StringBuilder sb)
	{
		sb.AppendFormat("{0000:0}", offset);

		var instructionCode = self.instructions.buffer[offset];
		var instruction = (Instruction)instructionCode;

		switch (instruction)
		{
		case Instruction.Return:
		case Instruction.LoadConstant:
			return SimpleInstruction(instruction, offset, sb);
		default:
			sb.AppendFormat("Unknown opcode {0}\n", instructionCode);
			return offset + 1;
		}
	}

	private static int SimpleInstruction(Instruction instruction, int offset, StringBuilder sb)
	{
		sb.AppendLine(instruction.ToString());
		return offset + 1;
	}
}
