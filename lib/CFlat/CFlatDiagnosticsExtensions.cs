using System.Text;

public static class CFlatDiagnosticsExtensions
{
	public static string GetFormattedCompileErrors(this CFlat self)
	{
		var sb = new StringBuilder();

		for (var i = 0; i < self.compileErrors.count; i++)
		{
			var e = self.compileErrors.buffer[i];
			sb.Append(e.message);

			if (e.slice.index > 0 || e.slice.length > 0)
			{
				var source = self.sources.buffer[e.sourceIndex];
				FormattingHelper.AddHighlightSlice(source.name, source.content, e.slice, sb);
			}
		}

		return sb.ToString();
	}

	public static string GetFormattedRuntimeError(this CFlat self)
	{
		if (!self.vm.error.isSome)
			return "";

		var error = self.vm.error.value;

		var sb = new StringBuilder();
		sb.Append(error.message);
		if (error.instructionIndex < 0)
			return sb.ToString();

		var source = self.sources.buffer[self.vm.chunk.FindSourceIndex(error.instructionIndex)];

		FormattingHelper.AddHighlightSlice(source.name, source.content, error.slice, sb);
		return sb.ToString();
	}

	public static string TraceCallStack(this CFlat self)
	{
		var vm = self.vm;
		var sb = new StringBuilder();

		sb.AppendLine("callstack:");
		for (var i = vm.callframeStack.count - 1; i >= 0; i--)
		{
			var callframe = vm.callframeStack.buffer[i];

			switch (callframe.type)
			{
			case CallFrame.Type.EntryPoint:
				break;
			case CallFrame.Type.Function:
				{
					var codeIndex = System.Math.Max(callframe.codeIndex - 1, 0);
					var sourceIndex = vm.chunk.sourceSlices.buffer[codeIndex].index;
					var source = self.sources.buffer[vm.chunk.FindSourceIndex(codeIndex)];

					var pos = FormattingHelper.GetLineAndColumn(
						source.content,
						sourceIndex
					);
					sb.Append("[line ");
					sb.Append(pos.lineIndex + 1);
					sb.Append("] ");

					vm.chunk.FormatFunction(callframe.functionIndex, sb);

					sb.Append(" => ");
					var slice = FormattingHelper.GetLineSlice(source.content, pos.lineIndex);
					slice = FormattingHelper.Trim(source.content, slice);
					sb.Append(source.content, slice.index, slice.length);
					sb.AppendLine();
					break;
				}
			case CallFrame.Type.NativeFunction:
				sb.Append("[native] ");
				vm.chunk.FormatNativeFunction(callframe.functionIndex, sb);
				sb.AppendLine();
				break;
			}
		}

		return sb.ToString();
	}

	public static string Disassemble(this CFlat self)
	{
		var sb = new StringBuilder();
		self.chunk.Disassemble(self.sources.buffer, sb);
		return sb.ToString();
	}
}