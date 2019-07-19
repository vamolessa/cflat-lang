using System.Collections.Generic;

public sealed class EndParser : Parser<None>
{
	public override Result<PartialOk, Parser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		var token = tokens[index];
		if (token.kind != Token.EndToken.kind)
			return Result.Error(new Parser.Error(index, "Not end of program"));

		return Result.Ok(new PartialOk(1, Option.None));
	}
}

public sealed class TokenParser<T> : Parser<T>
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

	public override Result<PartialOk, Parser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		if (index >= tokens.Count)
			return Result.Error(new Parser.Error(index, expectErrorMessage));

		var token = tokens[index];
		if (token.kind != tokenKind)
			return Result.Error(new Parser.Error(index, expectErrorMessage));

		var parsed = converter(source, token);
		return Result.Ok(new PartialOk(1, parsed));
	}
}

public sealed class AnyParser<T> : Parser<T>
{
	private readonly Parser<T>[] parsers;
	private string expectErrorMessage = "Did not have any match";

	public AnyParser(Parser<T>[] parsers)
	{
		this.parsers = parsers;
	}

	public AnyParser<T> Expect(string expectErrorMessage)
	{
		this.expectErrorMessage = expectErrorMessage;
		return this;
	}

	public override Result<PartialOk, Parser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		foreach (var parser in parsers)
		{
			var result = parser.PartialParse(source, tokens, index);
			if (result.isOk)
				return result;
		}

		return Result.Error(new Parser.Error(index, expectErrorMessage));
	}
}

public sealed class RepeatUntilParser<A, B> : Parser<List<A>>
{
	private readonly Parser<A> repeatParser;
	private readonly Parser<B> endParser;

	public RepeatUntilParser(Parser<A> repeatParser, Parser<B> endParser)
	{
		this.repeatParser = repeatParser;
		this.endParser = endParser;
	}

	public override Result<PartialOk, Parser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		var parsed = new List<A>();
		var initialIndex = index;

		while (index < tokens.Count)
		{
			var repeatResult = repeatParser.PartialParse(source, tokens, index);
			if (!repeatResult.isOk)
			{
				var endResult = endParser.PartialParse(source, tokens, index);
				if (endResult.isOk)
					break;
				else
					return Result.Error(repeatResult.error);
			}

			if (repeatResult.ok.matchCount == 0)
				break;

			index += repeatResult.ok.matchCount;
			parsed.Add(repeatResult.ok.parsed);
		}

		return Result.Ok(new PartialOk(index - initialIndex, parsed));
	}
}

public sealed class OptionalParser<T> : Parser<Option<T>>
{
	private readonly Parser<T> parser;

	public OptionalParser(Parser<T> parser)
	{
		this.parser = parser;
	}

	public override Result<PartialOk, Parser.Error> PartialParse(string source, List<Token> tokens, int index)
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

public sealed class DeferParser<T> : Parser<T>
{
	public Parser<T> parser;

	public override Result<PartialOk, Parser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		return parser.PartialParse(source, tokens, index);
	}
}

public sealed class SelectParser<A, B> : Parser<B>
{
	private readonly Parser<A> parser;
	private readonly System.Func<A, B> selector;

	public SelectParser(Parser<A> parser, System.Func<A, B> selector)
	{
		this.parser = parser;
		this.selector = selector;
	}

	public override Result<PartialOk, Parser.Error> PartialParse(string source, List<Token> tokens, int index)
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

public sealed class SelectManyParser<A, B, C> : Parser<C>
{
	private readonly Parser<A> parser;
	private readonly System.Func<A, Parser<B>> parserSelector;
	private readonly System.Func<A, B, C> resultSelector;

	public SelectManyParser(Parser<A> parser, System.Func<A, Parser<B>> parserSelector, System.Func<A, B, C> resultSelector)
	{
		this.parser = parser;
		this.parserSelector = parserSelector;
		this.resultSelector = resultSelector;
	}

	public override Result<PartialOk, Parser.Error> PartialParse(string source, List<Token> tokens, int index)
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
			return Result.Error(result.error);

		return Result.Ok(new PartialOk(
			result.ok.matchCount + otherResult.ok.matchCount,
			resultSelector(result.ok.parsed, otherResult.ok.parsed)
		));
	}
}