using System.Collections.Generic;
using Xunit;

public sealed class ParserTests
{
	public enum TokenKind
	{
		Number,
		Sum,
		Minus,
		Comma
	}

	private readonly string source = "01234";
	private readonly List<Token> tokens = new List<Token>()
	{
		new Token(0, 0, 1),
		new Token(1, 1, 1),
		new Token(2, 2, 1),
		new Token(3, 3, 1),
		new Token(4, 4, 1),
		Token.EndToken
	};
	private readonly Scanner[] scanners = {
		new IntegerNumberScanner().ForToken((int)TokenKind.Number),
		new CharScanner('+').ForToken((int)TokenKind.Sum),
		new CharScanner('-').ForToken((int)TokenKind.Minus),
		new CharScanner(',').ForToken((int)TokenKind.Comma),
	};

	[Fact]
	public void SelectParser1()
	{
		var parser =
			from p0 in OldParser.Token(0)
			select new None();

		var result = parser.PartialParse(source, tokens, 0);
		Assert.True(result.isOk);
		Assert.Equal(1, result.ok.matchCount);
	}

	[Fact]
	public void SelectParser2()
	{
		var parser =
			from p0 in OldParser.Token(0)
			from p1 in OldParser.Token(1)
			select new None();

		var result = parser.PartialParse(source, tokens, 0);
		Assert.True(result.isOk);
		Assert.Equal(2, result.ok.matchCount);
	}

	[Fact]
	public void SelectParser3()
	{
		var parser =
			from p0 in OldParser.Token(0)
			from p1 in OldParser.Token(1)
			from p2 in OldParser.Token(2)
			select new None();

		var result = parser.PartialParse(source, tokens, 0);
		Assert.True(result.isOk);
		Assert.Equal(3, result.ok.matchCount);
	}

	[Theory]
	[InlineData("", 0)]
	[InlineData("+", 1)]
	[InlineData("++", 2)]
	[InlineData("+++++", 5)]
	public void RepeatParser(string source, int tokenCount)
	{
		var tokens = new List<Token>();
		var errors = new List<int>();
		Tokenizer.Tokenize(source, scanners, tokens, errors);
		Assert.True(errors.Count == 0);
		Assert.Equal(tokenCount + 1, tokens.Count);

		var parser = OldParser.Token((int)TokenKind.Sum).RepeatUntil(OldParser.End());

		var result = parser.PartialParse(source, tokens, 0);
		Assert.True(result.isOk);
		Assert.Equal(tokenCount, result.ok.matchCount);
	}

	[Theory]
	[InlineData("1", 1)]
	[InlineData("1+2", 3)]
	[InlineData("1+2+3", 5)]
	[InlineData("1-2+3", 5)]
	public void ExpressionParser(string source, int tokenCount)
	{
		var tokens = new List<Token>();
		var errors = new List<int>();
		Tokenizer.Tokenize(source, scanners, tokens, errors);
		Assert.True(errors.Count == 0);
		Assert.Equal(tokenCount + 1, tokens.Count);

		var parser =
			from p0 in OldParser.Token((int)TokenKind.Number)
			from p1 in (
				from p2 in OldParser.Any(
					OldParser.Token((int)TokenKind.Sum),
					OldParser.Token((int)TokenKind.Minus)
				)
				from p3 in OldParser.Token((int)TokenKind.Number)
				select (p2, p3)
			).RepeatUntil(OldParser.End())
			select new None();

		var result = parser.PartialParse(source, tokens, 0);
		Assert.True(result.isOk, $"{result.error.message} @{result.error.tokenIndex}");
		Assert.Equal(tokenCount, result.ok.matchCount);
	}

	[Theory]
	[InlineData("1", 1)]
	[InlineData("1+2", 3)]
	[InlineData("1+2+3", 5)]
	[InlineData("1-2+3", 5)]
	public void AssociativeExpressionParser(string source, int tokenCount)
	{
		var tokens = new List<Token>();
		var errors = new List<int>();
		Tokenizer.Tokenize(source, scanners, tokens, errors);
		Assert.True(errors.Count == 0);
		Assert.Equal(tokenCount + 1, tokens.Count);

		var parser = ExtraParsers.LeftAssociative(
			OldParser.Token((int)TokenKind.Number, (s, t) => new None()),
			(int)TokenKind.Sum,
			(int)TokenKind.Minus
		).Aggregate((t, l, r) => new None());

		var result = parser.PartialParse(source, tokens, 0);
		Assert.True(result.isOk, $"{result.error.message} @{result.error.tokenIndex}");
		Assert.Equal(tokenCount, result.ok.matchCount);
	}

	[Theory]
	[InlineData("", 0, 0)]
	[InlineData("1", 1, 1)]
	[InlineData("1,2", 3, 2)]
	[InlineData("1,2,3", 5, 3)]
	public void ListWithSeparatorParser(string source, int tokenCount, int parseCount)
	{
		var tokens = new List<Token>();
		var errors = new List<int>();
		Tokenizer.Tokenize(source, scanners, tokens, errors);
		Assert.True(errors.Count == 0);
		Assert.Equal(tokenCount + 1, tokens.Count);

		var parser = ExtraParsers.RepeatWithSeparator(
			OldParser.Token((int)TokenKind.Number, (s, t) => new None()),
			OldParser.End(),
			(int)TokenKind.Comma
		);

		var result = parser.PartialParse(source, tokens, 0);
		Assert.True(result.isOk, $"{result.error.message} @{result.error.tokenIndex}");
		Assert.Equal(tokenCount, result.ok.matchCount);
		Assert.Equal(parseCount, result.ok.parsed.Count);
	}

	[Theory]
	[InlineData("+", 0)]
	[InlineData("11", 1)]
	public void AnyParserFail(string source, int errorIndex)
	{
		var tokens = new List<Token>();
		var errors = new List<int>();
		Tokenizer.Tokenize(source, scanners, tokens, errors);
		Assert.True(errors.Count == 0);

		var parser =
			from n in OldParser.Token((int)TokenKind.Number)
			from o in OldParser.Any(
				OldParser.Token((int)TokenKind.Sum),
				OldParser.Token((int)TokenKind.Minus)
			)
			select new None();

		var result = parser.PartialParse(source, tokens, 0);
		Assert.False(result.isOk);
		Assert.Equal(errorIndex, result.error.tokenIndex);
	}
}