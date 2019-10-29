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
	public static Slice Trim(string source, Slice slice)
	{
		var startIndex = (int)slice.index;
		var endIndex = slice.index + slice.length - 1;

		while (startIndex < endIndex && char.IsWhiteSpace(source, startIndex))
			startIndex += 1;

		while (endIndex > startIndex && char.IsWhiteSpace(source, endIndex))
			endIndex -= 1;

		return new Slice(startIndex, endIndex - startIndex + 1);
	}

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

	public static Slice GetLineSlice(string source, ushort lineIndex)
	{
		var lineStartIndex = 0;

		while (lineIndex > 0)
		{
			while (
				lineStartIndex < source.Length &&
				source[lineStartIndex++] != '\n'
			)
			{ }

			lineIndex -= 1;
		}

		if (lineStartIndex >= source.Length)
			return new Slice();

		var lineEndIndex = lineStartIndex;
		while (
			lineEndIndex < source.Length &&
			source[lineEndIndex] != '\n'
		)
			lineEndIndex += 1;

		return new Slice(lineStartIndex, lineEndIndex - lineStartIndex);
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
		const int LineNumberColumnWidth = 6;
		var position = GetLineAndColumn(source, slice.index, tabSize);

		sb.Append(" (line: ");
		sb.Append(position.lineIndex + 1);
		sb.Append(", column: ");
		sb.Append(position.columnIndex + 1);
		sb.Append(")");

		var lineNumberTabStopOffset = (tabSize - LineNumberColumnWidth % tabSize) % tabSize;

		for (var i = 0; i < contextSize; i++)
		{
			var lineIndex = position.lineIndex - contextSize + 1 + i;
			var lineSlice = GetLineSlice(source, (ushort)lineIndex);

			sb.AppendLine();
			sb.Append(' ', lineNumberTabStopOffset);
			sb.AppendFormat("{0,4}| ", lineIndex + 1);
			sb.Append(source, lineSlice.index, lineSlice.length);
		}

		sb.AppendLine();
		sb.Append(' ', lineNumberTabStopOffset + LineNumberColumnWidth + position.columnIndex);
		sb.Append('^', slice.length > 0 ? slice.length : (ushort)1);
		sb.Append(" here\n\n");
	}
}