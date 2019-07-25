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
	public void GetLines(string text, int startLine, int endLine, string result)
	{
		var lines = CompilerHelper.GetLines(text, startLine, endLine);
		Assert.Equal(result, lines);
	}
}