using Xunit;

public sealed class CompilerHelperTests
{
	private const int TabSize = 4;

	[Theory]
	[InlineData("a", 0, 0, "a")]
	[InlineData("a", 0, 1, "a")]
	[InlineData("a", 0, 2, "a")]
	[InlineData("a\na", 0, 0, "a")]
	[InlineData("a", 1, 1, "")]
	[InlineData("a\na", 0, 1, "a\na")]
	public void GetLinesTest(string source, int startLine, int endLine, string result)
	{
		var lines = FormattingHelper.GetLines(source, startLine, endLine);
		Assert.Equal(result, lines);
	}

	[Theory]
	[InlineData("0123456789", 0, 1, 1)]
	[InlineData("\n123456789", 1, 2, 1)]
	[InlineData("0123\n56789", 7, 2, 3)]
	[InlineData("0123\n\t\t789", 7, 2, 9)]
	public void GetLineAndColumnTest(string source, int index, int expectedLine, int expectedColumn)
	{
		var position = FormattingHelper.GetLineAndColumn(source, index, TabSize);
		Assert.Equal(expectedLine, position.line);
		Assert.Equal(expectedColumn, position.column);
	}
}