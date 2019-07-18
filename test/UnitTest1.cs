using Xunit;

public sealed class ScannerTests
{
	[Theory]
	[InlineData("foo", 0, "foo")]
	[InlineData("xxfoo", 2, "foo")]
	[InlineData("fooxx", 0, "foo")]
	[InlineData("xxfooxx", 2, "foo")]
	public void StartsWithTest(string str, int index, string match)
	{
		var result = ScannerHelper.StartsWith(str, index, match);
		Assert.True(result);
	}

	[Theory]
	[InlineData("foo", 0, "foo")]
	[InlineData("xxfoo", 2, "foo")]
	[InlineData("foobar", 0, "foo")]
	public void ExactScanTest(string input, int index, string match)
	{
		var scanner = new ExactScanner(match);
		var result = scanner.Scan(input, index);
		Assert.Equal(match.Length, result);
	}

	[Theory]
	[InlineData("(foo)", 0, "(", ")", 5)]
	[InlineData("xx(foo)", 2, "(", ")", 5)]
	[InlineData("(foo\\)bar)", 0, "(", ")", 10)]
	[InlineData("((foo)bar))", 0, "((", "))", 11)]
	public void EnclosedScanTest(string input, int index, string beginMatch, string endMatch, int expected)
	{
		var scanner = new EnclosedScanner(beginMatch, endMatch);
		var result = scanner.Scan(input, index);
		Assert.Equal(expected, result);
	}
}
