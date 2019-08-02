using System.Text;

public static class VirtualMachineHelper
{
	public static string ValueToString(ByteCodeChunk chunk, object[] objs, ValueData data, ValueType type)
	{
		switch (ValueTypeHelper.GetKind(type))
		{
		case ValueType.Unit:
			return "{}";
		case ValueType.Bool:
			return data.asBool ? "true" : "false";
		case ValueType.Int:
			return data.asInt.ToString();
		case ValueType.Float:
			return data.asFloat.ToString();
		case ValueType.String:
			return string.Concat("\"", objs[data.asInt].ToString(), "\"");
		case ValueType.Function:
			return chunk.FormatFunction(data.asInt);
		case ValueType.Custom:
			return string.Format("CustomType [{0}] {1}", objs[data.asInt].GetType().Name, objs[data.asInt].ToString());
		default:
			return string.Format("<invalid type {0}>", type);
		}
	}

	public static string PopToString(VirtualMachine vm)
	{
		var data = vm.valueStack.buffer[vm.valueStack.count - 1];
		var type = vm.typeStack.buffer[vm.typeStack.count - 1];
		var str = ValueToString(vm.chunk, vm.heap.buffer, data, type);
		vm.PopValue();

		return str;
	}

	public static void TraceStack(VirtualMachine vm, StringBuilder sb)
	{
		sb.Append("          ");
		for (var i = 0; i < vm.valueStack.count; i++)
		{
			sb.Append("[");
			sb.Append(ValueToString(
				vm.chunk,
				vm.heap.buffer,
				vm.valueStack.buffer[i],
				vm.typeStack.buffer[i]
			));
			sb.Append("]");
		}
		sb.AppendLine();
	}

	public static string TraceCallStack(VirtualMachine vm, string source)
	{
		var sb = new StringBuilder();

		sb.AppendLine("callstack:");
		for (var i = vm.callframeStack.count - 1; i >= 0; i--)
		{
			var callframe = vm.callframeStack.buffer[i];
			var sourceIndex = vm.chunk.slices.buffer[callframe.codeIndex - 1].index;

			var pos = CompilerHelper.GetLineAndColumn(source, sourceIndex, 1);
			sb.Append("[line ");
			sb.Append(pos.line);
			sb.Append("] ");

			sb.Append(vm.chunk.FormatFunction(callframe.functionIndex));

			sb.Append(" => ");
			var line = CompilerHelper.GetLines(source, pos.line - 1, pos.line - 1);
			sb.AppendLine(line.TrimStart());
		}

		return sb.ToString();
	}

	public static string FormatError(string source, RuntimeError error, int contextSize, int tabSize)
	{
		var sb = new StringBuilder();

		var position = CompilerHelper.GetLineAndColumn(source, error.slice.index, tabSize);

		sb.Append(error.message);
		sb.Append(" (line: ");
		sb.Append(position.line);
		sb.Append(", column: ");
		sb.Append(position.column);
		sb.AppendLine(")");

		sb.Append(CompilerHelper.GetLines(
			source,
			System.Math.Max(position.line - contextSize, 0),
			System.Math.Max(position.line - 1, 0)
		));
		sb.AppendLine();
		sb.Append(' ', position.column - 1);
		sb.Append('^', error.slice.length > 0 ? error.slice.length : 1);
		sb.Append(" here\n");

		return sb.ToString();
	}
}