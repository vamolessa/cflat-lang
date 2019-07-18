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
	private string expectedErrorMessage = "Invalid token";

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

	public TokenParser<T> Expect(string expectedErrorMessage)
	{
		this.expectedErrorMessage = expectedErrorMessage;
		return this;
	}

	public override Result<PartialOk, Parser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		if (index >= tokens.Count)
			return Result.Error(new Parser.Error(index, expectedErrorMessage));

		var token = tokens[index];
		if (token.kind != tokenKind)
			return Result.Error(new Parser.Error(index, expectedErrorMessage));

		var parsed = converter(source, token);
		return Result.Ok(new PartialOk(1, parsed));
	}
}

public sealed class AnyParser<T> : Parser<T>
{
	private readonly List<Parser<T>> parsers;
	private string expectedErrorMessage = "Did not have any match";

	public AnyParser(Parser<T> a, Parser<T> b)
	{
		parsers = new List<Parser<T>>();
		parsers.Add(a);
		parsers.Add(b);
	}

	public override AnyParser<T> Or(Parser<T> other)
	{
		parsers.Add(other);
		return this;
	}

	public AnyParser<T> Expect(string expectedErrorMessage)
	{
		this.expectedErrorMessage = expectedErrorMessage;
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

		return Result.Error(new Parser.Error(index, expectedErrorMessage));
	}
}

public sealed class RepeatParser<T> : Parser<List<T>>
{
	private readonly Parser<T> parser;
	private readonly int minRepeatCount;
	private string expectedErrorMessage = "Did not repeat enough";

	public RepeatParser(Parser<T> parser, int minRepeatCount)
	{
		this.parser = parser;
		this.minRepeatCount = minRepeatCount;
	}

	public RepeatParser<T> Expect(string expectedErrorMessage)
	{
		this.expectedErrorMessage = expectedErrorMessage;
		return this;
	}

	public override Result<PartialOk, Parser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		var repeatCount = 0;
		var parsed = new List<T>();
		var initialIndex = index;

		while (index < tokens.Count)
		{
			var result = parser.PartialParse(source, tokens, index);
			if (!result.isOk)
				return Result.Error(result.error);

			if (result.ok.matchCount == 0)
				break;

			repeatCount += 1;
			index += result.ok.matchCount;
			parsed.Add(result.ok.parsed);
		}

		if (repeatCount < minRepeatCount)
			return Result.Error(new Parser.Error(index, expectedErrorMessage));

		return Result.Ok(new PartialOk(index - initialIndex, parsed));
	}
}

public sealed class MaybeParser<T> : Parser<T>
{
	private readonly Parser<T> parser;

	public MaybeParser(Parser<T> parser)
	{
		this.parser = parser;
	}

	public override Result<PartialOk, Parser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		var result = parser.PartialParse(source, tokens, index);
		if (!result.isOk)
			return Result.Ok(new PartialOk(0, default(T)));
		return result;
	}
}

public sealed class LeftAssociativeParser<T> : Parser<T>
{
	private readonly Parser<T> higherPrecedenceParser;
	private readonly int[] operatorTokens;
	private System.Func<Token, T, T, T> aggregator;

	public LeftAssociativeParser(Parser<T> higherPrecedenceParser, params int[] operatorTokens)
	{
		this.higherPrecedenceParser = higherPrecedenceParser;
		this.operatorTokens = operatorTokens;
	}

	public LeftAssociativeParser<T> Aggregate(System.Func<Token, T, T, T> aggregator)
	{
		if (aggregator != null)
			this.aggregator = aggregator;
		return this;
	}

	public override Result<PartialOk, Parser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		var initialIndex = index;

		var result = higherPrecedenceParser.PartialParse(source, tokens, index);
		if (!result.isOk)
			return result;

		index += result.ok.matchCount;

		while (index < tokens.Count)
		{
			var operatorToken = new Token(-1, 0, 0);
			foreach (var token in operatorTokens)
			{
				if (tokens[index].kind == token)
				{
					operatorToken = tokens[index];
					break;
				}
			}

			if (operatorToken.kind < 0)
				break;

			index += 1;
			var rightResult = higherPrecedenceParser.PartialParse(source, tokens, index);
			if (!rightResult.isOk)
				return rightResult;

			index += rightResult.ok.matchCount;

			var parsed = aggregator(
				operatorToken,
				result.ok.parsed,
				rightResult.ok.parsed
			);
			result = Result.Ok(new PartialOk(index - initialIndex, parsed));
		}

		return result;
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