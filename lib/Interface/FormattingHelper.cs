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
			switch (source[i])
			{
			case '\t': column += tabSize; break;
			case '\r': break;
			default: column += 1; break;
			}

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

	public static string FormatCompileError(string source, Buffer<CompileError> errors, byte tabSize)
	{
		var sb = new StringBuilder();

		for (var i = 0; i < errors.count; i++)
		{
			var e = errors.buffer[i];
			sb.Append(e.message);

			if (e.slice.index > 0 || e.slice.length > 0)
				HighlightSlice(source, e.slice, tabSize, sb);
		}

		return sb.ToString();
	}

	public static string FormatRuntimeError(string source, RuntimeError error, byte tabSize)
	{
		var sb = new StringBuilder();
		sb.Append((string)error.message);
		if (error.instructionIndex < 0)
			return sb.ToString();

		HighlightSlice(source, error.slice, tabSize, sb);
		return sb.ToString();
	}

	private static void HighlightSlice(string source, Slice slice, byte tabSize, StringBuilder sb)
	{
		var startPosition = GetLineAndColumn(source, slice.index, tabSize);
		var endPosition = GetLineAndColumn(source, slice.index + slice.length, tabSize);

		if (slice.length == 0)
			slice = new Slice(slice.index, (ushort)1);

		sb.Append(" (line: ");
		sb.Append(startPosition.lineIndex + 1);
		sb.Append(", column: ");
		sb.Append(startPosition.columnIndex + 1);
		sb.AppendLine(")");

		var lineNumberColumnWidth = GetDigitCount(endPosition.lineIndex + 1) + 2;
		var lineNumberTabStopOffset = (tabSize - lineNumberColumnWidth % tabSize) % tabSize;

		sb.Append(' ', lineNumberTabStopOffset + lineNumberColumnWidth - 2);
		sb.AppendLine("|");
		for (var i = startPosition.lineIndex; i <= endPosition.lineIndex; i++)
		{
			var lineSlice = GetLineSlice(source, i);

			sb.Append(' ', lineNumberTabStopOffset);
			sb.Append(i + 1);
			sb.Append("| ");
			sb.Append(source, lineSlice.index, lineSlice.length);
			sb.AppendLine();

			sb.Append(' ', lineNumberTabStopOffset + lineNumberColumnWidth - 2);
			sb.Append("| ");

			if (i == startPosition.lineIndex)
			{
				sb.Append(' ', startPosition.columnIndex);
				var endColumnIndex = startPosition.lineIndex == endPosition.lineIndex ?
					endPosition.columnIndex :
					GetLineAndColumn(source, lineSlice.index + lineSlice.length, tabSize).columnIndex;

				sb.Append('^', endColumnIndex - startPosition.columnIndex);
			}
			else if (i == endPosition.lineIndex)
			{
				var endColumnIndex = GetLineAndColumn(source, slice.index + slice.length, tabSize).columnIndex;
				sb.Append('^', endColumnIndex);
			}
			else
			{
				var endColumnIndex = GetLineAndColumn(source, lineSlice.index + lineSlice.length, tabSize).columnIndex;
				sb.Append('^', endColumnIndex);
			}
			sb.AppendLine();
		}
	}

	private static int GetDigitCount(int number)
	{
		var count = 1;
		while (number > 10)
		{
			number /= 10;
			count += 1;
		}

		return count;
	}
}