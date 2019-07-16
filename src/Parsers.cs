using System.Collections.Generic;

public sealed class DebugParser<T> : Parser<T>
{
	public readonly struct DebugInfo
	{
		public readonly Parser<T> parser;
		public readonly Result<PartialOk> result;
		public readonly string source;
		public readonly List<Token> tokens;
		public readonly int tokenIndex;

		public string InputLeft
		{
			get
			{
				var index = tokenIndex;
				if (result.IsOk)
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
				return result.IsOk ?
					string.Format("match count: {0}", result.ok.matchCount) :
					string.Format("error: {0}", result.errorMessage);
			}
		}

		public DebugInfo(
			Parser<T> parser,
			Result<PartialOk> result,
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

	public override Result<PartialOk> PartialParse(string source, List<Token> tokens, int index)
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
	private System.Func<string, Token, Option<T>> converter;

	public TokenParser()
	{
		this.tokenKind = -1;
	}

	public TokenParser(int tokenKind)
	{
		this.tokenKind = tokenKind;
	}

	public TokenParser<T> As(System.Func<string, Token, Option<T>> converter)
	{
		this.converter = converter;
		return this;
	}

	public override Result<PartialOk> PartialParse(string source, List<Token> tokens, int index)
	{
		if (index >= tokens.Count)
			return Result.Error(index, expectedErrorMessage);

		var token = tokens[index];
		if (token.kind != tokenKind)
			return Result.Error(index, expectedErrorMessage);

		if (converter == null)
			return Result.Ok(new PartialOk(1, Option.None));

		var maybeParsed = converter(source, token);
		return maybeParsed.isSome ?
			Result.Ok(new PartialOk(1, maybeParsed)) :
			Result.Error(index, expectedErrorMessage);
	}
}

public sealed class AnyParser<T> : Parser<T>
{
	private readonly Parser<T>[] parsers;

	public AnyParser(Parser<T>[] parsers)
	{
		this.parsers = parsers;
	}

	public override Result<PartialOk> PartialParse(string source, List<Token> tokens, int index)
	{
		foreach (var parser in parsers)
		{
			var result = parser.PartialParse(source, tokens, index);
			if (result.IsOk)
				return result;
		}

		return Result.Error(index, expectedErrorMessage);
	}
}

public sealed class AllParser<T> : Parser<T>
{
	private readonly Parser<T>[] parsers;
	private System.Func<List<T>, Option<T>> converter;

	public AllParser(Parser<T>[] parsers)
	{
		this.parsers = parsers;
		this.converter = parsed => parsed.Count > 0 ?
			Option.Some(parsed[0]) :
			Option.None;
	}

	public AllParser<T> As(System.Func<List<T>, Option<T>> converter)
	{
		if (converter != null)
			this.converter = converter;
		return this;
	}

	public override Result<PartialOk> PartialParse(string source, List<Token> tokens, int index)
	{
		var allConverted = new List<T>();

		var initialIndex = index;
		foreach (var parser in parsers)
		{
			var result = parser.PartialParse(source, tokens, index);
			if (!result.IsOk)
				return result;

			index += result.ok.matchCount;
			if (result.ok.maybeParsed.isSome)
				allConverted.Add(result.ok.maybeParsed.value);
		}

		var maybeParsed = converter(allConverted);
		return maybeParsed.isSome ?
			Result.Ok(new PartialOk(index - initialIndex, maybeParsed)) :
			Result.Error(index, expectedErrorMessage);
	}
}

public sealed class RepeatParser<T> : Parser<T>
{
	private readonly Parser<T> parser;
	private readonly int minRepeatCount;
	private System.Func<List<T>, Option<T>> converter;

	public RepeatParser(Parser<T> parser, int minRepeatCount)
	{
		this.parser = parser;
		this.minRepeatCount = minRepeatCount;
		this.converter = parsed => parsed.Count > 0 ?
			Option.Some(parsed[0]) :
			Option.None;
	}

	public RepeatParser<T> As(System.Func<List<T>, Option<T>> converter)
	{
		if (converter != null)
			this.converter = converter;
		return this;
	}

	public override Result<PartialOk> PartialParse(string source, List<Token> tokens, int index)
	{
		var repeatCount = 0;
		var allConverted = new List<T>();
		var initialIndex = index;

		while (index < tokens.Count)
		{
			var result = parser.PartialParse(source, tokens, index);
			if (!result.IsOk)
				return result;

			if (result.ok.matchCount == 0)
				break;

			repeatCount += 1;
			index += result.ok.matchCount;
			if (result.ok.maybeParsed.isSome)
				allConverted.Add(result.ok.maybeParsed.value);
		}

		if (repeatCount < minRepeatCount)
			return Result.Error(index, expectedErrorMessage);

		var maybeParsed = converter(allConverted);
		return maybeParsed.isSome ?
			Result.Ok(new PartialOk(index - initialIndex, maybeParsed)) :
			Result.Error(index, expectedErrorMessage);
	}
}

public sealed class MaybeParser<T> : Parser<T>
{
	private readonly Parser<T> parser;

	public MaybeParser(Parser<T> parser)
	{
		this.parser = parser;
	}

	public override Result<PartialOk> PartialParse(string source, List<Token> tokens, int index)
	{
		var result = parser.PartialParse(source, tokens, index);
		if (!result.IsOk)
			return Result.Ok(new PartialOk(0, Option.None));
		return result;
	}
}

public sealed class LazyParser<T> : Parser<T>
{
	private Parser<T> parser;
	private readonly System.Func<Builder, Parser<T>> initialization;

	public LazyParser(System.Func<Builder, Parser<T>> initialization)
	{
		this.initialization = initialization;
	}

	public override Result<PartialOk> PartialParse(string source, List<Token> tokens, int index)
	{
		if (parser == null)
			parser = initialization(new Builder());

		return parser.PartialParse(source, tokens, index);
	}
}