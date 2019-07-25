using System.Text;

public static class ByteCodeChunkExtensions
{
	public static void Disassemble(this ByteCodeChunk self, string source, string chunkName, StringBuilder sb)
	{
		sb.Append("== ");
		sb.Append(chunkName);
		sb.Append(" [");
		sb.Append(self.bytes.count);
		sb.AppendLine(" bytes] ==");

		for (var index = 0; index < self.bytes.count;)
			index = DisassembleInstruction(self, source, index, sb);

		sb.Append("== ");
		sb.Append(chunkName);
		sb.AppendLine(" end ==");
	}

	public static int DisassembleInstruction(this ByteCodeChunk self, string source, int index, StringBuilder sb)
	{
		sb.AppendFormat("{0:0000} ", index);
		if (index > 0 && self.sourceIndexes.buffer[index] == self.sourceIndexes.buffer[index - 1])
		{
			sb.Append("          | ");
		}
		else
		{
			var sourceIndex = self.sourceIndexes.buffer[index];
			var position = CompilerHelper.GetLineAndColumn(source, sourceIndex);
			sb.AppendFormat("({0,4},{1,4}) ", position.line, position.column);
		}

		var instructionCode = self.bytes.buffer[index];
		var instruction = (Instruction)instructionCode;

		switch (instruction)
		{
		case Instruction.LoadNil:
		case Instruction.LoadTrue:
		case Instruction.LoadFalse:
		case Instruction.Return:
		case Instruction.Negate:
		case Instruction.Add:
		case Instruction.Subtract:
		case Instruction.Multiply:
		case Instruction.Divide:
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
