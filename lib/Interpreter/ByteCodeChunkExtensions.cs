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
			sb.Append("   | ");
		}
		else
		{
			var sourceIndex = self.sourceIndexes.buffer[index];
			var position = CompilerHelper.GetLineAndColumn(source, sourceIndex);
			sb.AppendFormat("{0,4} ", position.line);
		}

		var instructionCode = self.bytes.buffer[index];
		var instruction = (Instruction)instructionCode;

		switch (instruction)
		{
		case Instruction.LoadNil:
		case Instruction.LoadTrue:
		case Instruction.LoadFalse:
		case Instruction.Return:
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
		case Instruction.Equal:
		case Instruction.GreaterInt:
		case Instruction.GreaterFloat:
		case Instruction.LessInt:
		case Instruction.LessFloat:
			return SimpleInstruction(instruction, index, sb);
		case Instruction.LoadLiteral:
			return LoadLiteralInstruction(self, instruction, index, sb);
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

	private static int LoadLiteralInstruction(ByteCodeChunk chunk, Instruction instruction, int index, StringBuilder sb)
	{
		var literalIndex = chunk.bytes.buffer[index + 1];
		var value = chunk.literalData.buffer[literalIndex];
		var type = chunk.literalTypes.buffer[literalIndex];

		sb.Append(instruction.ToString());
		switch (type)
		{
		case ValueType.Int:
			sb.AppendFormat(" {0}", value.asInt);
			break;
		case ValueType.Float:
			sb.AppendFormat(" {0}", value.asFloat);
			break;
		case ValueType.String:
			sb.AppendFormat(" {0}", chunk.stringLiterals.buffer[(uint)value.asInt]);
			break;
		}

		return index + 2;
	}
}
