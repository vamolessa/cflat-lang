using System.Text;

public static class VirtualMachineHelper
{
	public static void TraceStack(VirtualMachine vm, StringBuilder sb)
	{
		sb.Append("                 ");
		for (var i = 0; i < vm.stack.count; i++)
		{
			sb.Append("[");
			sb.Append(vm.stack.buffer[i].AsString(vm.heap.buffer));
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