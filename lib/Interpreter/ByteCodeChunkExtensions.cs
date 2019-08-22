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
	public static void FormatFunction(this ByteCodeChunk self, int functionIndex, StringBuilder sb)
	{
		var function = self.functions.buffer[functionIndex];
		if (string.IsNullOrEmpty(function.name))
			sb.Append("<anonymous>");
		else
			sb.Append(function.name);
		sb.Append(' ');
		FormatFunctionType(self, function.typeIndex, sb);
	}

	public static void FormatNativeFunction(this ByteCodeChunk self, int functionIndex, StringBuilder sb)
	{
		var function = self.nativeFunctions.buffer[functionIndex];
		sb.Append(function.name);
		sb.Append(' ');
		FormatFunctionType(self, function.typeIndex, sb);
	}

	public static void FormatFunctionType(this ByteCodeChunk self, ushort functionTypeIndex, StringBuilder sb)
	{
		var type = self.functionTypes.buffer[functionTypeIndex];
		sb.Append("fn(");
		for (var i = 0; i < type.parameters.length; i++)
		{
			var paramIndex = type.parameters.index + i;
			var paramType = self.functionParamTypes.buffer[paramIndex];
			paramType.Format(self, sb);
			if (i < type.parameters.length - 1)
				sb.Append(',');
		}
		sb.Append(')');
		if (!type.returnType.IsKind(TypeKind.Unit))
		{
			sb.Append(':');
			type.returnType.Format(self, sb);
		}
	}

	public static void FormatTupleType(this ByteCodeChunk self, ushort tupleTypeIndex, StringBuilder sb)
	{
		var type = self.tupleTypes.buffer[tupleTypeIndex];
		sb.Append('(');
		for (var i = 0; i < type.elements.length; i++)
		{
			var elementIndex = type.elements.index + i;
			var elementType = self.tupleElementTypes.buffer[elementIndex];
			elementType.Format(self, sb);
			if (i < type.elements.length - 1)
				sb.Append(' ');
		}
		sb.Append(')');
	}

	public static void FormatStructType(this ByteCodeChunk self, ushort structTypeIndex, StringBuilder sb)
	{
		var type = self.structTypes.buffer[structTypeIndex];
		if (string.IsNullOrEmpty(type.name))
			sb.Append("struct");
		else
			sb.Append(type.name);
		sb.Append('{');
		for (var i = 0; i < type.fields.length; i++)
		{
			var fieldIndex = type.fields.index + i;
			var field = self.structTypeFields.buffer[fieldIndex];
			sb.Append(field.name);
			sb.Append(':');
			field.type.Format(self, sb);
			if (i < type.fields.length - 1)
				sb.Append(' ');
		}
		sb.Append('}');
	}

	public static void Disassemble(this ByteCodeChunk self, StringBuilder sb)
	{
		sb.Append("== ");
		sb.Append(self.bytes.count);
		sb.AppendLine(" bytes ==");
		sb.AppendLine("byte instruction");

		for (var index = 0; index < self.bytes.count;)
		{
			PrintFunctionName(self, index, sb);
			index = DisassembleInstruction(self, index, sb);
			sb.AppendLine();
		}
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
			PrintFunctionName(self, index, sb);
			PrintLineNumber(self, source, index, sb);
			index = DisassembleInstruction(self, index, sb);
			sb.AppendLine();
		}

		sb.Append("== ");
		sb.Append(chunkName);
		sb.AppendLine(" end ==");
	}

	private static void PrintLineNumber(ByteCodeChunk self, string source, int index, StringBuilder sb)
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

	private static void PrintFunctionName(ByteCodeChunk self, int codeIndex, StringBuilder sb)
	{
		for (var i = 0; i < self.functions.count; i++)
		{
			var function = self.functions.buffer[i];
			if (function.codeIndex == codeIndex)
			{
				sb.Append("  // ");
				self.FormatFunction(i, sb);
				sb.AppendLine();
				break;
			}
		}
	}

	public static int DisassembleInstruction(this ByteCodeChunk self, int index, StringBuilder sb)
	{
		sb.AppendFormat("{0:0000} ", index);

		var instructionCode = self.bytes.buffer[index];
		var instruction = (Instruction)instructionCode;

		switch (instruction)
		{
		case Instruction.Halt:
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
		case Instruction.Call:
		case Instruction.CallNative:
		case Instruction.Return:
		case Instruction.PopMultiple:
		case Instruction.LoadLocal:
		case Instruction.AssignLocal:
		case Instruction.IncrementLocalInt:
		case Instruction.ForLoopCheck:
			return OneArgInstruction(self, instruction, index, sb);
		case Instruction.Move:
		case Instruction.AssignLocalMultiple:
		case Instruction.LoadLocalMultiple:
			return TwoArgInstruction(self, instruction, index, sb);
		case Instruction.LoadLiteral:
			return LoadLiteralInstruction(self, instruction, index, sb);
		case Instruction.LoadFunction:
			return LoadFunctionInstruction(self, instruction, index, sb);
		case Instruction.LoadNativeFunction:
			return LoadNativeFunctionInstruction(self, instruction, index, sb);
		case Instruction.JumpForward:
		case Instruction.JumpForwardIfFalse:
		case Instruction.JumpForwardIfTrue:
		case Instruction.PopAndJumpForwardIfFalse:
			return JumpInstruction(self, instruction, 1, index, sb);
		case Instruction.JumpBackward:
			return JumpInstruction(self, instruction, -1, index, sb);
		case Instruction.Print:
			return PrintInstruction(self, instruction, index, sb);
		default:
			sb.Append("Unknown instruction ");
			sb.Append(instruction.ToString());
			return index + 1;
		}
	}

	private static int SimpleInstruction(Instruction instruction, int index, StringBuilder sb)
	{
		sb.Append(instruction.ToString());
		return index + 1;
	}

	private static int OneArgInstruction(ByteCodeChunk chunk, Instruction instruction, int index, StringBuilder sb)
	{
		sb.Append(instruction.ToString());
		sb.Append(' ');
		sb.Append(chunk.bytes.buffer[index + 1]);
		return index + 2;
	}

	private static int TwoArgInstruction(ByteCodeChunk chunk, Instruction instruction, int index, StringBuilder sb)
	{
		sb.Append(instruction.ToString());
		sb.Append(' ');
		sb.Append(chunk.bytes.buffer[index + 1]);
		sb.Append(", ");
		sb.Append(chunk.bytes.buffer[index + 2]);
		return index + 3;
	}

	private static int LoadLiteralInstruction(ByteCodeChunk chunk, Instruction instruction, int index, StringBuilder sb)
	{
		var literalIndex = BytesHelper.BytesToShort(
			chunk.bytes.buffer[index + 1],
			chunk.bytes.buffer[index + 2]
		);
		var value = chunk.literalData.buffer[literalIndex];
		var type = chunk.literalKinds.buffer[literalIndex];

		sb.Append(instruction.ToString());
		sb.Append(' ');
		switch (type)
		{
		case TypeKind.Int:
			sb.Append(value.asInt);
			break;
		case TypeKind.Float:
			sb.Append(value.asFloat);
			break;
		case TypeKind.String:
			sb.Append('"');
			sb.Append(chunk.stringLiterals.buffer[value.asInt]);
			sb.Append('"');
			break;
		}

		return index + 3;
	}

	private static int LoadFunctionInstruction(ByteCodeChunk chunk, Instruction instruction, int index, StringBuilder sb)
	{
		sb.Append(instruction.ToString());
		sb.Append(' ');
		var functionIndex = BytesHelper.BytesToShort(
			chunk.bytes.buffer[index + 1],
			chunk.bytes.buffer[index + 2]
		);
		FormatFunction(chunk, functionIndex, sb);
		return index + 3;
	}

	private static int LoadNativeFunctionInstruction(ByteCodeChunk chunk, Instruction instruction, int index, StringBuilder sb)
	{
		sb.Append(instruction.ToString());
		sb.Append(' ');
		var functionIndex = BytesHelper.BytesToShort(
			chunk.bytes.buffer[index + 1],
			chunk.bytes.buffer[index + 2]
		);
		FormatNativeFunction(chunk, functionIndex, sb);
		return index + 3;
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
		sb.Append(")");
		return index + 3;
	}

	private static int PrintInstruction(ByteCodeChunk chunk, Instruction instruction, int index, StringBuilder sb)
	{
		sb.Append(instruction.ToString());
		sb.Append(' ');

		var type = ValueType.Read(
			chunk.bytes.buffer[index + 1],
			chunk.bytes.buffer[index + 2],
			chunk.bytes.buffer[index + 3],
			chunk.bytes.buffer[index + 4]
		);

		type.Format(chunk, sb);

		return index + 5;
	}
}
