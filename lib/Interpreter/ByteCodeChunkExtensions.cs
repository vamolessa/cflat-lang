using System.Text;

public sealed class ByteCodeChunkDebugView
{
	public readonly string[] lines;

	public ByteCodeChunkDebugView(ByteCodeChunk chunk)
	{
		var sb = new StringBuilder();
		chunk.Disassemble(sb);
		lines = sb.ToString().Split(new string[] { "\n", "\r\n" }, System.StringSplitOptions.RemoveEmptyEntries);
	}
}

public static class ByteCodeChunkExtensions
{
	public static void Disassemble(this ByteCodeChunk self, StringBuilder sb)
	{
		sb.Append("== ");
		sb.Append(self.bytes.count);
		sb.AppendLine(" bytes ==");
		sb.AppendLine("byte instruction");

		for (var index = 0; index < self.bytes.count;)
			index = DisassembleInstruction(self, index, sb);
		sb.AppendLine("== end ==");
	}

	public static void Disassemble(this ByteCodeChunk self, string source, string chunkName, StringBuilder sb)
	{
		sb.Append("== ");
		sb.Append(chunkName);
		sb.Append(" [");
		sb.Append(self.bytes.count);
		sb.AppendLine(" bytes] ==");
		sb.AppendLine("line byte instruction");

		for (var index = 0; index < self.bytes.count;)
		{
			PrintLineNumber(self, source, index, sb);
			index = DisassembleInstruction(self, index, sb);
		}

		sb.Append("== ");
		sb.Append(chunkName);
		sb.AppendLine(" end ==");
	}

	public static void PrintLineNumber(this ByteCodeChunk self, string source, int index, StringBuilder sb)
	{
		var currentSourceIndex = self.slices.buffer[index].index;
		var currentPosition = CompilerHelper.GetLineAndColumn(source, currentSourceIndex, 1);
		var lastLine = -1;
		if (index > 0)
		{
			var lastSourceIndex = self.slices.buffer[index - 1].index;
			lastLine = CompilerHelper.GetLineAndColumn(source, lastSourceIndex, 1).line;
		}

		if (currentPosition.line == lastLine)
			sb.Append("   | ");
		else
			sb.AppendFormat("{0,4} ", currentPosition.line);
	}

	public static int DisassembleInstruction(this ByteCodeChunk self, int index, StringBuilder sb)
	{
		sb.AppendFormat("{0:0000} ", index);

		var instructionCode = self.bytes.buffer[index];
		var instruction = (Instruction)instructionCode;

		switch (instruction)
		{
		case Instruction.Halt:
		case Instruction.Return:
		case Instruction.Print:
		case Instruction.Pop:
		case Instruction.LoadUnit:
		case Instruction.LoadFalse:
		case Instruction.LoadTrue:
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
			return SimpleInstruction(instruction, index, sb);
		case Instruction.PopMultiple:
		case Instruction.CopyTo:
		case Instruction.AssignLocal:
		case Instruction.LoadLocal:
		case Instruction.IncrementLocal:
		case Instruction.ForLoopCheck:
			return ArgInstruction(self, instruction, index, sb);
		case Instruction.LoadLiteral:
			return LoadLiteralInstruction(self, instruction, index, sb);
		case Instruction.JumpForward:
		case Instruction.JumpForwardIfFalse:
		case Instruction.JumpForwardIfTrue:
		case Instruction.PopAndJumpForwardIfFalse:
			return JumpInstruction(self, instruction, 1, index, sb);
		case Instruction.JumpBackward:
			return JumpInstruction(self, instruction, -1, index, sb);
		default:
			sb.AppendFormat("Unknown instruction '{0}'\n", instruction.ToString());
			return index + 1;
		}
	}

	private static int SimpleInstruction(Instruction instruction, int index, StringBuilder sb)
	{
		sb.AppendLine(instruction.ToString());
		return index + 1;
	}

	private static int ArgInstruction(ByteCodeChunk chunk, Instruction instruction, int index, StringBuilder sb)
	{
		sb.Append(instruction.ToString());
		sb.Append(' ');
		sb.Append(chunk.bytes.buffer[index + 1]);
		sb.AppendLine();
		return index + 2;
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
			sb.AppendFormat(" {0}\n", value.asInt);
			break;
		case ValueType.Float:
			sb.AppendFormat(" {0}\n", value.asFloat);
			break;
		case ValueType.String:
			sb.AppendFormat(" \"{0}\"\n", chunk.stringLiterals.buffer[value.asInt]);
			break;
		}

		return index + 2;
	}

	private static int JumpInstruction(ByteCodeChunk chunk, Instruction instruction, int sign, int index, StringBuilder sb)
	{
		sb.Append(instruction.ToString());
		sb.Append(' ');
		var offset = BytesHelper.BytesToShort(
			chunk.bytes.buffer[index + 1],
			chunk.bytes.buffer[index + 2]
		);
		sb.Append(offset);
		sb.Append(" (goto ");
		sb.Append(index + 3 + offset * sign);
		sb.AppendLine(")");
		return index + 3;
	}
}
