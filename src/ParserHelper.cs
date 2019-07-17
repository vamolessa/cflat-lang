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
}
