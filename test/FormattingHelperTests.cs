using Xunit;

public sealed class FormattingHelperTests
{
	private const byte TabSize = 4;

	[Theory]
	[InlineData("abc", 0, 0, 3)]
	[InlineData("abc\n", 0, 0, 3)]
	[InlineData("abc", 1, 0, 0)]
	[InlineData("abc", 2, 0, 0)]
	[InlineData("abc\ndef", 0, 0, 3)]
	[InlineData("abc\ndef", 1, 4, 3)]
	[InlineData("\n", 0, 0, 0)]
	public void GetLineTests(string source, ushort lineIndex, ushort expectedIndex, ushort expectedLength)
	{
		var slice = FormattingHelper.GetLineSlice(source, lineIndex);
		Assert.Equal(expectedIndex, slice.index);
		Assert.Equal(expectedLength, slice.length);
	}

	[Theory]
	[InlineData("0123456789", 0, 0, 0)]
	[InlineData("0123456789", 4, 0, 4)]
	[InlineData("\n123456789", 1, 1, 0)]
	[InlineData("0123\n56789", 7, 1, 2)]
	[InlineData("0123\n\t\t789", 7, 1, 8)]
	[InlineData("01\r23456789", 6, 0, 5)]
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