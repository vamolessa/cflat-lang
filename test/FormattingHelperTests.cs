using Xunit;

public sealed class FormattingHelperTests
{
	private const byte TabSize = 4;

	[Theory]
	[InlineData("a", 0, 0, "a")]
	[InlineData("a", 0, 1, "a")]
	[InlineData("a", 0, 2, "a")]
	[InlineData("a\nb", 0, 0, "a")]
	[InlineData("a", 1, 1, "")]
	[InlineData("a\nb", 0, 1, "a\nb")]
	public void GetLinesTests(string source, ushort startLineIndex, ushort endLineIndex, string result)
	{
		var slice = FormattingHelper.GetLinesSlice(source, startLineIndex, endLineIndex);
		Assert.Equal(result, source.Substring(slice.index, slice.length));
	}

	[Theory]
	[InlineData("0123456789", 0, 0, 0)]
	[InlineData("\n123456789", 1, 1, 0)]
	[InlineData("0123\n56789", 7, 1, 2)]
	[InlineData("0123\n\t\t789", 7, 1, 8)]
	public void GetLineAndColumnTests(string source, int index, int expectedLineIndex, int expectedColumnIndex)
	{
		var position = FormattingHelper.GetLineAndColumn(source, index, TabSize);
		Assert.Equal(expectedLineIndex, position.lineIndex);
		Assert.Equal(expectedColumnIndex, position.columnIndex);
	}

	[Theory]
	[InlineData("abc", 0, 3)]
	[InlineData(" abc", 1, 3)]
	[InlineData("abc ", 0, 3)]
	[InlineData(" abc ", 1, 3)]
	[InlineData(" \t\nabc", 3, 3)]
	public void TrimTests(string source, ushort expectedIndex, ushort expectedLength)
	{
		var slice = new Slice(0, source.Length);
		slice = FormattingHelper.Trim(source, slice);
		Assert.Equal(expectedIndex, slice.index);
		Assert.Equal(expectedLength, slice.length);
	}
}