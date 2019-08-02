using System.Collections.Generic;
using Xunit;

public sealed class ScannerTests
{
	private readonly Scanner[] scanners = new Scanner[] {
		new WhiteSpaceScanner().Ignore(),
		new IntegerNumberScanner().ForToken(0),
		new CharScanner('+').ForToken(1),
		new CharScanner('-').ForToken(2)
	};

	[Theory]
	[InlineData("foo", 0, "foo")]
	[InlineData("xxfoo", 2, "foo")]
	[InlineData("fooxx", 0, "foo")]
	[InlineData("xxfooxx", 2, "foo")]
	public void StartsWithTest(string str, int index, string match)
	{
		var result = Scanner.StartsWith(str, index, match);
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
	[InlineData("(foobar", 0, "(", ")", 0)]
	public void EnclosedScanTest(string input, int index, string beginMatch, string endMatch, int expected)
	{
		var scanner = new EnclosedScanner(beginMatch, endMatch);
		var result = scanner.Scan(input, index);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("1234", 0, 4)]
	[InlineData("xx1234", 2, 4)]
	public void IntegerNumberScanTest(string input, int index, int expected)
	{
		var scanner = new IntegerNumberScanner();
		var result = scanner.Scan(input, index);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("1234", 0, 4)]
	[InlineData("xx1234", 2, 4)]
	public void RealNumberScanTest(string input, int index, int expected)
	{
		var scanner = new RealNumberScanner();
		var result = scanner.Scan(input, index);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("foo", 0, 3)]
	[InlineData("xxfoo", 2, 3)]
	[InlineData("xx_", 2, 1)]
	[InlineData("xx_foo_", 2, 5)]
	public void IdentifierScanTest(string input, int index, int expected)
	{
		var scanner = new IdentifierScanner("_");
		var result = scanner.Scan(input, index);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("1", new int[] { 0 })]
	[InlineData("123", new int[] { 0 })]
	[InlineData("  123", new int[] { 0 })]
	[InlineData("123  ", new int[] { 0 })]
	[InlineData("  123  ", new int[] { 0 })]
	[InlineData("-12", new int[] { 2, 0 })]
	[InlineData("12+34", new int[] { 0, 1, 0 })]
	[InlineData("12+-34", new int[] { 0, 1, 2, 0 })]
	[InlineData(" 12 + - 34 ", new int[] { 0, 1, 2, 0 })]
	public void TokenizerTest(string input, int[] expectedTokenKinds)
	{
		var tokenizer = new Tokenizer();
		tokenizer.Reset(scanners, input);
		var tokens = new List<Token>();
		for (var t = tokenizer.Next(); t.IsValid(); t = tokenizer.Next())
			tokens.Add(t);

		Assert.Equal(expectedTokenKinds.Length, tokens.Count);
		for (var i = 0; i < expectedTokenKinds.Length; i++)
			Assert.Equal(expectedTokenKinds[i], tokens[i].kind);
	}
}
