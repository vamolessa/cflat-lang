using System.Collections.Generic;

public sealed class EndParser : OldParser<None>
{
	public override Result<PartialOk, OldParser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		var token = tokens[index];
		if (token.kind != Token.EndToken.kind)
			return Result.Error(new OldParser.Error(index, "Not end of program"));

		return Result.Ok(new PartialOk(1, Option.None));
	}
}

public sealed class TokenParser<T> : OldParser<T>
{
	private readonly int tokenKind;
	private readonly System.Func<string, Token, T> converter;
	private string expectErrorMessage = "Invalid token";

	public TokenParser(int tokenKind, System.Func<string, Token, T> converter)
	{
		this.tokenKind = tokenKind;
		this.converter = converter;
	}

	public TokenParser<T> Expect(string expectErrorMessage)
	{
		this.expectErrorMessage = expectErrorMessage;
		return this;
	}

	public override Result<PartialOk, OldParser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		var token = tokens[index];
		if (token.kind != tokenKind)
			return Result.Error(new OldParser.Error(index, expectErrorMessage));

		var parsed = converter(source, token);
		return Result.Ok(new PartialOk(1, parsed));
	}
}

public sealed class AnyParser<T> : OldParser<T>
{
	private readonly OldParser<T>[] parsers;

	public AnyParser(OldParser<T>[] parsers)
	{
		this.parsers = parsers;
	}

	public override Result<PartialOk, OldParser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		var furtherTokenIndex = int.MinValue;
		var furtherMessage = "";

		foreach (var parser in parsers)
		{
			var result = parser.PartialParse(source, tokens, index);
			if (result.isOk)
				return result;

			if (result.error.tokenIndex > furtherTokenIndex)
			{
				furtherTokenIndex = result.error.tokenIndex;
				furtherMessage = result.error.message;
			}
		}

		return Result.Error(new OldParser.Error(furtherTokenIndex, furtherMessage));
	}
}

public sealed class RepeatUntilParser<A, B> : OldParser<List<A>>
{
	private readonly OldParser<A> repeatParser;
	private readonly OldParser<B> endParser;

	public RepeatUntilParser(OldParser<A> repeatParser, OldParser<B> endParser)
	{
		this.repeatParser = repeatParser;
		this.endParser = endParser;
	}

	public override Result<PartialOk, OldParser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		var parsed = new List<A>();
		var initialIndex = index;

		while (true)
		{
			var result = repeatParser.PartialParse(source, tokens, index);
			if (!result.isOk || result.ok.matchCount == 0)
				break;

			index += result.ok.matchCount;
			parsed.Add(result.ok.parsed);
		}

		{
			var result = endParser.PartialParse(source, tokens, index);
			if (!result.isOk)
				return Result.Error(result.error);
		}

		return Result.Ok(new PartialOk(index - initialIndex, parsed));
	}
}

public sealed class OptionalParser<T> : OldParser<Option<T>>
{
	private readonly OldParser<T> parser;

	public OptionalParser(OldParser<T> parser)
	{
		this.parser = parser;
	}

	public override Result<PartialOk, OldParser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		var result = parser.PartialParse(source, tokens, index);
		if (!result.isOk)
			return Result.Ok(new PartialOk(0, Option.None));

		return Result.Ok(new PartialOk(
			result.ok.matchCount,
			Option.Some(result.ok.parsed)
		));
	}
}

public sealed class DeferParser<T> : OldParser<T>
{
	public OldParser<T> parser;

	public override Result<PartialOk, OldParser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		return parser.PartialParse(source, tokens, index);
	}
}

public sealed class SelectParser<A, B> : OldParser<B>
{
	private readonly OldParser<A> parser;
	private readonly System.Func<A, B> selector;

	public SelectParser(OldParser<A> parser, System.Func<A, B> selector)
	{
		this.parser = parser;
		this.selector = selector;
	}

	public override Result<PartialOk, OldParser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		var result = parser.PartialParse(source, tokens, index);
		if (!result.isOk)
			return Result.Error(result.error);

		return Result.Ok(new PartialOk(
			result.ok.matchCount,
			selector(result.ok.parsed)
		));
	}
}

public sealed class SelectManyParser<A, B, C> : OldParser<C>
{
	private readonly OldParser<A> parser;
	private readonly System.Func<A, OldParser<B>> parserSelector;
	private readonly System.Func<A, B, C> resultSelector;

	public SelectManyParser(OldParser<A> parser, System.Func<A, OldParser<B>> parserSelector, System.Func<A, B, C> resultSelector)
	{
		this.parser = parser;
		this.parserSelector = parserSelector;
		this.resultSelector = resultSelector;
	}

	public override Result<PartialOk, OldParser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		var result = parser.PartialParse(source, tokens, index);
		if (!result.isOk)
			return Result.Error(result.error);

		var otherParser = parserSelector(result.ok.parsed);
		var otherResult = otherParser.PartialParse(
			source,
			tokens,
			index + result.ok.matchCount
		);

		if (!otherResult.isOk)
			return Result.Error(otherResult.error);

		return Result.Ok(new PartialOk(
			result.ok.matchCount + otherResult.ok.matchCount,
			resultSelector(result.ok.parsed, otherResult.ok.parsed)
		));
	}
}