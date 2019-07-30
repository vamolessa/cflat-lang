using System.Text;

public static class VirtualMachineHelper
{
	public static string ValueToString(object[] objs, ValueData data, ValueType type)
	{
		switch (type)
		{
		case ValueType.Nil:
			return "nil";
		case ValueType.Bool:
			return data.asBool ? "true" : "false";
		case ValueType.Int:
			return data.asInt.ToString();
		case ValueType.Float:
			return data.asFloat.ToString();
		case ValueType.String:
			return string.Concat("\"", objs[data.asInt].ToString(), "\"");
		default:
			return "<invalid value>";
		}
	}

	public static string PopToString(VirtualMachine vm)
	{
		var data = vm.valueStack.buffer[vm.valueStack.count - 1];
		var type = vm.typeStack.buffer[vm.typeStack.count - 1];
		var str = ValueToString(vm.heap.buffer, data, type);
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
				vm.heap.buffer,
				vm.valueStack.buffer[i],
				vm.typeStack.buffer[i]
			));
			sb.Append("]");
		}
		sb.AppendLine();
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
		sb.Append(", pc: ");
		sb.Append(error.instructionIndex);
		sb.AppendLine(")");

		sb.Append(CompilerHelper.GetLines(
			source,
			System.Math.Max(position.line - contextSize, 0),
			System.Math.Max(position.line - 1, 0)
		));
		sb.AppendLine();
		sb.Append(' ', position.column - 1);
		sb.Append('^', error.slice.length > 0 ? error.slice.length : 1);
		sb.Append(" here\n\n");

		return sb.ToString();
	}
}