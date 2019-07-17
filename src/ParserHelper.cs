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

public static class ParserHelper
{
	public static LineAndColumn GetLineAndColumn(string source, int index)
	{
		var line = 1;
		var lastNewLineIndex = 0;

		for (var i = 0; i < index; i++)
		{
			if (source[i] == '\n')
			{
				lastNewLineIndex = i;
				line += 1;
			}
		}

		return new LineAndColumn(line, index - lastNewLineIndex);
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
			if (lineCount == startLine)
				startLineIndex = i + 1;

			lineCount += 1;
		}

		if (lineCount == endLine)
			return source.Substring(startLineIndex);

		return "";
	}

	public static string GetContext(string source, LineAndColumn position, int lineCount)
	{
		return string.Format(
			"{0}\n{1}^ here\n",
			ParserHelper.GetLines(
				source,
				position.line - 1 - lineCount,
				position.line - 1
			),
			new string(' ', position.column - 1)
		);
	}
}
