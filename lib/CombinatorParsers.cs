using System.Collections.Generic;

public sealed class DebugParser<T> : Parser<T>
{
	public readonly struct DebugInfo
	{
		public readonly Parser<T> parser;
		public readonly Result<PartialOk, Parser.Error> result;
		public readonly string source;
		public readonly List<Token> tokens;
		public readonly int tokenIndex;

		public string InputLeft
		{
			get
			{
				var index = tokenIndex;
				if (result.isOk)
					index += result.ok.matchCount;
				return index < tokens.Count ?
					string.Format(
						"input left: {0}",
						source.Substring(tokens[index].index)
					) :
					"no input left";
			}
		}

		public string MatchCountOrError
		{
			get
			{
				return result.isOk ?
					string.Format("match count: {0}", result.ok.matchCount) :
					string.Format("error: {0}", result.error.message);
			}
		}

		public DebugInfo(
			Parser<T> parser,
			Result<PartialOk, Parser.Error> result,
			string source,
			List<Token> tokens,
			int tokenIndex)
		{
			this.parser = parser;
			this.result = result;
			this.source = source;
			this.tokens = tokens;
			this.tokenIndex = tokenIndex;
		}

		public override string ToString()
		{
			return string.Format(
				"DebugInfo parser: {0} {1} {2}",
				parser.GetType().Name,
				MatchCountOrError,
				InputLeft
			);
		}
	}

	private readonly Parser<T> parser;
	private readonly System.Action<DebugInfo> checkpoint;

	public DebugParser(Parser<T> parser, System.Action<DebugInfo> checkpoint)
	{
		this.parser = parser;
		this.checkpoint = checkpoint;
	}

	public override Result<PartialOk, Parser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		var result = parser.PartialParse(source, tokens, index);
		if (checkpoint != null)
		{
			checkpoint(new DebugInfo(
				parser,
				result,
				source,
				tokens,
				index
			));
		}

		return result;
	}
}

public sealed class TokenParser<T> : Parser<T>
{
	private readonly int tokenKind;
	private System.Func<string, Token, T> converter;
	private string expectErrorMessage = "Invalid token";

	public TokenParser()
	{
		this.tokenKind = -1;
	}

	public TokenParser(int tokenKind)
	{
		this.tokenKind = tokenKind;
	}

	public TokenParser<T> As(System.Func<string, Token, T> converter)
	{
		this.converter = converter;
		return this;
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

public sealed class RepeatParser<T> : Parser<List<T>>
{
	private readonly Parser<T> parser;

	public RepeatParser(Parser<T> parser)
	{
		this.parser = parser;
	}

	public override Result<PartialOk, Parser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		var parsed = new List<T>();
		var initialIndex = index;

		while (index < tokens.Count)
		{
			var result = parser.PartialParse(source, tokens, index);
			if (!result.isOk)
				return Result.Error(result.error);

			if (result.ok.matchCount == 0)
				break;

			index += result.ok.matchCount;
			parsed.Add(result.ok.parsed);
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