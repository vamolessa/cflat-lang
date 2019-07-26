using System.Text;

public static class VirtualMachineHelper
{
	public static string PopToString(VirtualMachine vm)
	{
		var data = vm.valueStack.buffer[vm.valueStack.count - 1];
		var type = vm.typeStack.buffer[vm.typeStack.count - 1];
		var str = Value.AsString(vm.heap.buffer, data, type);
		vm.PopValue();

		return str;
	}

	public static void TraceStack(VirtualMachine vm, StringBuilder sb)
	{
		sb.Append("          ");
		for (var i = 0; i < vm.valueStack.count; i++)
		{
			sb.Append("[");
			sb.Append(Value.AsString(
				vm.heap.buffer,
				vm.valueStack.buffer[i],
				vm.typeStack.buffer[i]
			));
			sb.Append("]");
		}
		sb.AppendLine();
	}

	public static string FormatError(string source, RuntimeError error, int contextSize)
	{
		var sb = new StringBuilder();

		var position = CompilerHelper.GetLineAndColumn(source, error.sourceIndex);

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
			System.Math.Max(position.line - 1 - contextSize, 0),
			System.Math.Max(position.line - 1, 0)
		));
		sb.AppendLine();
		sb.Append(' ', position.column - 1);
		sb.Append("^ here\n");

		return sb.ToString();
	}
}