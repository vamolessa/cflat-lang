using Xunit;

public sealed class CompilerHelperTests
{
	[Theory]
	[InlineData("a", 0, 0, "a")]
	[InlineData("a", 0, 1, "a")]
	[InlineData("a", 0, 2, "a")]
	[InlineData("a\na", 0, 0, "a")]
	[InlineData("a", 1, 1, "")]
	[InlineData("a\na", 0, 1, "a\na")]
	public void GetLinesTest(string text, int startLine, int endLine, string result)
	{
		var lines = CompilerHelper.GetLines(text, startLine, endLine);
		Assert.Equal(result, lines);
	}

	[Theory]
	[InlineData("0123456789", 0, 0)]
	[InlineData("0123456789", 3, 3)]
	[InlineData("\t\t23456789", 3, 17)]
	[InlineData("\t\t2345\t789", 3, 17)]
	public void LengthToIndexTest(string source, int index, int expectedLength)
	{
		var length = CompilerHelper.LengthUntilIndex(source, index, 8);
		Assert.Equal(expectedLength, length);
	}
}