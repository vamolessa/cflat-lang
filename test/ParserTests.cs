using System.Collections.Generic;
using Xunit;

public sealed class ParserTests
{
	private readonly string source = "01234";
	private readonly List<Token> tokens = new List<Token>()
	{
		new Token(0, 0, 1),
		new Token(1, 1, 1),
		new Token(2, 2, 1),
		new Token(3, 3, 1),
		new Token(4, 4, 1)
	};

	[Fact]
	public void SelectParser1()
	{
		var parser =
			from p0 in Parser.Token(0)
			select Option.None;
		var result = parser.PartialParse(source, tokens, 0);

		Assert.True(result.isOk);
		Assert.Equal(1, result.ok.matchCount);
	}

	[Fact]
	public void SelectParser2()
	{
		var parser =
			from p0 in Parser.Token(0)
			from p1 in Parser.Token(1)
			select Option.None;
		var result = parser.PartialParse(source, tokens, 0);

		Assert.True(result.isOk);
		Assert.Equal(2, result.ok.matchCount);
	}

	[Fact]
	public void SelectParser3()
	{
		var parser =
			from p0 in Parser.Token(0)
			from p1 in Parser.Token(1)
			from p2 in Parser.Token(2)
			select Option.None;
		var result = parser.PartialParse(source, tokens, 0);

		Assert.True(result.isOk);
		Assert.Equal(3, result.ok.matchCount);
	}
}