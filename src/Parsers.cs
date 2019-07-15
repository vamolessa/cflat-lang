using System.Collections.Generic;

public sealed class DebugParser<T> : Parser<T>
{
	public readonly struct DebugInfo
	{
		public readonly Parser<T> parser;
		public readonly Result<PartialOk> result;
		public readonly string source;
		public readonly List<Token> tokens;
		public readonly int index;

		public DebugInfo(
			Parser<T> parser,
			Result<PartialOk> result,
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
			return result.IsOk ?
				string.Format(
					"DebugInfo parser: {0} read: {1} input left: {2}",
					parser.GetType().Name,
					result.ok.matchCount,
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

		return new Result<PartialOk>(index, expectedErrorMessage);
	}
}

public sealed class AllParser<T> : Parser<T>
{
	private readonly Parser<T>[] parsers;
	private System.Func<List<T>, Option<T>> converter;

	public AllParser(Parser<T>[] parsers)
	{
		this.parsers = parsers;
		this.converter = parsed => parsed.Count > 0 ? Option.Some(parsed[0]) : Option.None;
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
	private readonly int minMatchCount;
	private System.Func<List<T>, Option<T>> converter;

	public RepeatParser(Parser<T> parser, int minMatchCount)
	{
		this.parser = parser;
		this.minMatchCount = minMatchCount;
		this.converter = parsed => parsed.Count > 0 ? Option.Some(parsed[0]) : Option.None;
	}

	public RepeatParser<T> As(System.Func<List<T>, Option<T>> converter)
	{
		if (converter != null)
			this.converter = converter;
		return this;
	}

	public override Result<PartialOk> PartialParse(string source, List<Token> tokens, int index)
	{
		var matchCount = 0;
		var allConverted = new List<T>();
		var initialIndex = index;

		while (true)
		{
			var result = parser.PartialParse(source, tokens, index);
			if (!result.IsOk)
			{
				if (matchCount < minMatchCount)
					return result;
				break;
			}

			if (result.ok.matchCount == 0)
				break;

			matchCount += 1;
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