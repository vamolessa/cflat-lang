using System.Text;

public readonly struct LineAndColumn
{
	public readonly int line;
	public readonly int column;

	public LineAndColumn(int line, int column)
	{
		this.line = line;
		this.column = column;
	}

	public override string ToString()
	{
		return string.Format("line {0} column: {1}", line, column);
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
	public static LineAndColumn GetLineAndColumn(string source, int index, int tabSize)
	{
		var line = 1;
		var column = 1;

		for (var i = 0; i < index; i++)
		{
			column += source[i] == '\t' ? tabSize : 1;

			if (source[i] == '\n')
			{
				line += 1;
				column = 1;
			}
		}

		return new LineAndColumn(line, column);
	}

	public static string GetLines(string source, int startLine, int endLine)
	{
		var startLineIndex = 0;
		var lineCount = 0;

		for (var i = 0; i < source.Length; i++)
		{
			if (source[i] != '\n')
				continue;

			if (lineCount == endLine)
				return source.Substring(startLineIndex, i - startLineIndex);

			lineCount += 1;

			if (lineCount == startLine)
				startLineIndex = i + 1;
		}

		if (lineCount >= startLine && lineCount <= endLine)
			return source.Substring(startLineIndex);

		return "";
	}

	public static string FormatCompileError(string source, Buffer<CompileError> errors, int contextSize, int tabSize)
	{
		var sb = new StringBuilder();

		for (var i = 0; i < errors.count; i++)
		{
			var e = errors.buffer[i];
			sb.Append(e.message);

			if (e.slice.index == 0 && e.slice.length == 0)
				continue;

			var position = GetLineAndColumn(source, e.slice.index, tabSize);
			var lines = GetLines(
				source,
				System.Math.Max(position.line - contextSize, 0),
				System.Math.Max(position.line - 1, 0)
			);

			sb.Append(" (line: ");
			sb.Append(position.line);
			sb.Append(", column: ");
			sb.Append(position.column);
			sb.AppendLine(")");

			sb.AppendLine(lines);
			sb.Append(' ', position.column - 1);
			sb.Append('^', e.slice.length > 0 ? e.slice.length : 1);
			sb.Append(" here\n\n");
		}

		return sb.ToString();
	}

	public static string FormatRuntimeError(string source, RuntimeError error, int contextSize, int tabSize)
	{
		var sb = new StringBuilder();
		sb.Append((string)error.message);
		if (error.instructionIndex < 0)
			return sb.ToString();

		var position = FormattingHelper.GetLineAndColumn(source, (int)error.slice.index, tabSize);

		sb.Append(" (line: ");
		sb.Append(position.line);
		sb.Append(", column: ");
		sb.Append(position.column);
		sb.AppendLine(")");

		sb.Append(FormattingHelper.GetLines(
			source,
			System.Math.Max(position.line - contextSize, 0),
			System.Math.Max(position.line - 1, 0)
		));
		sb.AppendLine();
		sb.Append(' ', position.column - 1);
		sb.Append('^', (int)(error.slice.length > 0 ? error.slice.length : 1));
		sb.Append(" here\n\n");

		return sb.ToString();
	}
}