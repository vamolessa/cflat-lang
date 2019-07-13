using System.Collections.Generic;

public sealed class DebugParser<T> : Parser<T>
{
	public readonly struct DebugInfo
	{
		public readonly Parser<T> parser;
		public readonly PartialResult result;
		public readonly string source;
		public readonly List<Token> tokens;
		public readonly int index;

		public DebugInfo(
			Parser<T> parser,
			PartialResult result,
			string source,
			List<Token> tokens,
			int index)
		{
			this.parser = parser;
			this.result = result;
			this.source = source;
			this.tokens = tokens;
			this.index = index;
		}

		public override string ToString()
		{
			return result.IsSuccess ?
				string.Format(
					"DebugInfo parser: {0} read: {1} input left: {2}",
					parser.GetType().Name,
					result.matchCount,
					source.Substring(tokens[index].index)
				) :
				string.Format(
					"DebugInfo parser: {0} error: {1} input left: {2}",
					parser.GetType().Name,
					result.errorMessage,
					source.Substring(tokens[index].index)
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

	public override PartialResult PartialParse(string source, List<Token> tokens, int index)
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
	private System.Func<string, Token, MaybeParsed<T>> converter;

	public TokenParser()
	{
		this.tokenKind = -1;
	}

	public TokenParser(int tokenKind)
	{
		this.tokenKind = tokenKind;
	}

	public TokenParser<T> As(System.Func<string, Token, MaybeParsed<T>> converter)
	{
		this.converter = converter;
		return this;
	}

	public override PartialResult PartialParse(string source, List<Token> tokens, int index)
	{
		if (index >= tokens.Count)
			return new PartialResult(index, expectedErrorMessage);

		var token = tokens[index];
		if (token.kind != tokenKind)
			return new PartialResult(index, expectedErrorMessage);

		if (converter == null)
			return new PartialResult(1, MaybeParsed.NotParsed);

		var maybeParsed = converter(source, token);
		return maybeParsed.wasParsed ?
			new PartialResult(1, maybeParsed) :
			new PartialResult(index, expectedErrorMessage);
	}
}

public sealed class AnyParser<T> : Parser<T>
{
	private readonly Parser<T>[] parsers;

	public AnyParser(Parser<T>[] parsers)
	{
		this.parsers = parsers;
	}

	public override PartialResult PartialParse(string source, List<Token> tokens, int index)
	{
		foreach (var parser in parsers)
		{
			var result = parser.PartialParse(source, tokens, index);
			if (result.IsSuccess)
				return result;
		}

		return new PartialResult(index, expectedErrorMessage);
	}
}

public sealed class AllParser<T> : Parser<T>
{
	private readonly Parser<T>[] parsers;
	private System.Func<List<T>, MaybeParsed<T>> converter;

	public AllParser(Parser<T>[] parsers)
	{
		this.parsers = parsers;
		this.converter = parsed => parsed.Count > 0 ? MaybeParsed.Some(parsed[0]) : MaybeParsed.NotParsed;
	}

	public AllParser<T> As(System.Func<List<T>, MaybeParsed<T>> converter)
	{
		if (converter != null)
			this.converter = converter;
		return this;
	}

	public override PartialResult PartialParse(string source, List<Token> tokens, int index)
	{
		var allConverted = new List<T>();

		var initialIndex = index;
		foreach (var parser in parsers)
		{
			var result = parser.PartialParse(source, tokens, index);
			if (!result.IsSuccess)
				return result;

			index += result.matchCount;
			if (result.maybeParsed.wasParsed)
				allConverted.Add(result.maybeParsed.parsed);
		}

		var maybeParsed = converter(allConverted);
		return maybeParsed.wasParsed ?
			new PartialResult(index - initialIndex, maybeParsed) :
			new PartialResult(index, expectedErrorMessage);
	}
}

public sealed class RepeatParser<T> : Parser<T>
{
	private readonly Parser<T> parser;
	private readonly int minMatchCount;
	private System.Func<List<T>, MaybeParsed<T>> converter;

	public RepeatParser(Parser<T> parser, int minMatchCount)
	{
		this.parser = parser;
		this.minMatchCount = minMatchCount;
		this.converter = parsed => parsed.Count > 0 ? MaybeParsed.Some(parsed[0]) : MaybeParsed.NotParsed;
	}

	public RepeatParser<T> As(System.Func<List<T>, MaybeParsed<T>> converter)
	{
		if (converter != null)
			this.converter = converter;
		return this;
	}

	public override PartialResult PartialParse(string source, List<Token> tokens, int index)
	{
		var matchCount = 0;
		var allConverted = new List<T>();
		var initialIndex = index;

		while (true)
		{
			var result = parser.PartialParse(source, tokens, index);
			if (!result.IsSuccess)
			{
				if (matchCount < minMatchCount)
					return result;
				break;
			}

			if (result.matchCount == 0)
				break;

			matchCount += 1;
			index += result.matchCount;
			if (result.maybeParsed.wasParsed)
				allConverted.Add(result.maybeParsed.parsed);
		}

		var maybeParsed = converter(allConverted);
		return maybeParsed.wasParsed ?
			new PartialResult(index - initialIndex, maybeParsed) :
			new PartialResult(index, expectedErrorMessage);
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

	public override PartialResult PartialParse(string source, List<Token> tokens, int index)
	{
		if (parser == null)
			parser = initialization(new Builder());

		return parser.PartialParse(source, tokens, index);
	}
}