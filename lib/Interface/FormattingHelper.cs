using System.Text;

public readonly struct LineAndColumn
{
	public readonly ushort lineIndex;
	public readonly ushort columnIndex;

	public LineAndColumn(ushort lineIndex, ushort columnIndex)
	{
		this.lineIndex = lineIndex;
		this.columnIndex = columnIndex;
	}
}

public readonly struct CompileError
{
	public readonly Slice slice;
	public readonly string message;

	public CompileError(Slice slice, string message)
	{
		this.slice = slice;
		this.message = message;
	}
}

public readonly struct RuntimeError
{
	public readonly int instructionIndex;
	public readonly Slice slice;
	public readonly string message;

	public RuntimeError(int instructionIndex, Slice slice, string message)
	{
		this.instructionIndex = instructionIndex;
		this.slice = slice;
		this.message = message;
	}
}

public static class FormattingHelper
{
	public static LineAndColumn GetLineAndColumn(string source, int index, byte tabSize)
	{
		ushort line = 0;
		ushort column = 0;

		for (var i = 0; i < index; i++)
		{
			column += source[i] == '\t' ? tabSize : (byte)1;

			if (source[i] == '\n')
			{
				line += 1;
				column = 0;
			}
		}

		return new LineAndColumn(line, column);
	}

	public static Slice GetLinesSlice(string source, ushort startLineIndex, ushort endLineIndex)
	{
		var startLinePos = 0;
		var lineCount = 1;

		for (var i = 0; i < source.Length; i++)
		{
			if (source[i] != '\n')
				continue;

			if (lineCount == endLineIndex + 1)
				return new Slice(startLinePos, i - startLinePos);

			lineCount += 1;

			if (lineCount == startLineIndex + 1)
				startLinePos = i + 1;
		}

		if (lineCount < endLineIndex + 1)
			endLineIndex = (ushort)(lineCount - 1);

		if (lineCount > startLineIndex)
			return new Slice(startLinePos, source.Length - startLinePos);

		return new Slice();
	}

	public static string FormatCompileError(string source, Buffer<CompileError> errors, int contextSize, byte tabSize)
	{
		var sb = new StringBuilder();

		for (var i = 0; i < errors.count; i++)
		{
			var e = errors.buffer[i];
			sb.Append(e.message);

			if (e.slice.index > 0 || e.slice.length > 0)
				AddContext(source, e.slice, contextSize, tabSize, sb);
		}

		return sb.ToString();
	}

	public static string FormatRuntimeError(string source, RuntimeError error, int contextSize, byte tabSize)
	{
		var sb = new StringBuilder();
		sb.Append((string)error.message);
		if (error.instructionIndex < 0)
			return sb.ToString();

		AddContext(source, error.slice, contextSize, tabSize, sb);
		return sb.ToString();
	}

	private static void AddContext(string source, Slice slice, int contextSize, byte tabSize, StringBuilder sb)
	{
		var position = GetLineAndColumn(source, slice.index, tabSize);
		var contextSlice = GetLinesSlice(
			source,
			(ushort)(position.lineIndex - contextSize + 1),
			position.lineIndex
		);

		sb.Append(" (line: ");
		sb.Append(position.lineIndex + 1);
		sb.Append(", column: ");
		sb.Append(position.columnIndex + 1);
		sb.AppendLine(")");

		sb.Append(source, contextSlice.index, contextSlice.length);
		sb.AppendLine();
		sb.Append(' ', position.columnIndex);
		sb.Append('^', (int)(slice.length > 0 ? slice.length : 1));
		sb.Append(" here\n\n");
	}
}