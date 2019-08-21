using System.Text;

public static class PepperInspectionExtensions
{
	public static string TraceCallStack(this Pepper self)
	{
		var vm = self.virtualMachine;
		var sb = new StringBuilder();

		sb.AppendLine("callstack:");
		for (var i = vm.callframeStack.count - 1; i >= 0; i--)
		{
			var callframe = vm.callframeStack.buffer[i];
			var sourceIndex = vm.chunk.slices.buffer[callframe.codeIndex - 1].index;

			if (callframe.functionIndex < 0)
				continue;

			if (sourceIndex >= 0)
			{
				var pos = CompilerHelper.GetLineAndColumn(
					self.source,
					sourceIndex,
					1
				);
				sb.Append("[line ");
				sb.Append(pos.line);
				sb.Append("] ");

				vm.chunk.FormatFunction(callframe.functionIndex, sb);

				sb.Append(" => ");
				var line = CompilerHelper.GetLines(
					self.source,
					pos.line - 1,
					pos.line - 1
				);
				sb.AppendLine(line.TrimStart());
			}
			else
			{
				sb.Append("[native function] ");
				vm.chunk.FormatNativeFunction(callframe.functionIndex, sb);
				sb.AppendLine();
			}
		}

		return sb.ToString();
	}

	public static string Disassemble(this Pepper self)
	{
		var sb = new StringBuilder();
		self.byteCode.Disassemble(self.source, "script", sb);
		return sb.ToString();
	}
}