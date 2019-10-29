using Xunit;

public sealed class CompilerHelperTests
{
	private const byte TabSize = 4;

	[Theory]
	[InlineData("a", 0, 0, "a")]
	[InlineData("a", 0, 1, "a")]
	[InlineData("a", 0, 2, "a")]
	[InlineData("a\nb", 0, 0, "a")]
	[InlineData("a", 1, 1, "")]
	[InlineData("a\nb", 0, 1, "a\nb")]
	public void GetLinesTest(string source, ushort startLineIndex, ushort endLineIndex, string result)
	{
		var slice = FormattingHelper.GetLinesSlice(source, startLineIndex, endLineIndex);
		Assert.Equal(result, source.Substring(slice.index, slice.length));
	}

	[Theory]
	[InlineData("0123456789", 0, 0, 0)]
	[InlineData("\n123456789", 1, 1, 0)]
	[InlineData("0123\n56789", 7, 1, 2)]
	[InlineData("0123\n\t\t789", 7, 1, 8)]
	public void GetLineAndColumnTest(string source, int index, int expectedLineIndex, int expectedColumnIndex)
	{
		var position = FormattingHelper.GetLineAndColumn(source, index, TabSize);
		Assert.Equal(expectedLineIndex, position.lineIndex);
		Assert.Equal(expectedColumnIndex, position.columnIndex);
	}
}