using System.Collections.Generic;

public struct MaybeParsed
{
	public static MaybeParsed NotParsed = new MaybeParsed();

	public static MaybeParsed<T> Some<T>(T parsed)
	{
		return new MaybeParsed<T>(parsed);
	}
}

public readonly struct MaybeParsed<T>
{
	public static MaybeParsed<T> NotParsed = new MaybeParsed<T>();

	public readonly T parsed;
	public readonly bool wasParsed;

	public MaybeParsed(T parsed)
	{
		this.parsed = parsed;
		this.wasParsed = true;
	}

	public static implicit operator MaybeParsed<T>(T parsed)
	{
		return new MaybeParsed<T>(parsed);
	}

	public static implicit operator MaybeParsed<T>(MaybeParsed notParsed)
	{
		return new MaybeParsed<T>();
	}
}

public abstract class Parser<T>
{
	public struct Builder
	{
		public TokenParser<T> Token(int tokenKind)
		{
			return new TokenParser<T>(tokenKind);
		}

		public AnyParser<T> Any(params Parser<T>[] parsers)
		{
			return new AnyParser<T>(parsers);
		}

		public AllParser<T> All(params Parser<T>[] parsers)
		{
			return new AllParser<T>(parsers);
		}

		public RepeatParser<T> Repeat(Parser<T> parser, int minMatchCount)
		{
			return new RepeatParser<T>(parser, minMatchCount);
		}
	}

	public readonly struct Result
	{
		public readonly T parsed;
		public readonly int errorIndex;
		public readonly string errorMessage;

		public bool IsSuccess
		{
			get { return string.IsNullOrEmpty(errorMessage) && errorIndex < 0; }
		}

		public Result(T parsed)
		{
			this.parsed = parsed;
			this.errorIndex = -1;
			this.errorMessage = null;
		}

		public Result(int errorIndex, string errorMessage)
		{
			this.parsed = default(T);
			this.errorIndex = errorIndex;
			this.errorMessage = errorMessage;
		}
	}

	public readonly struct PartialResult
	{
		public readonly int matchCount;
		public readonly MaybeParsed<T> maybeParsed;
		public readonly string errorMessage;

		public bool IsSuccess
		{
			get { return string.IsNullOrEmpty(errorMessage); }
		}

		public PartialResult(int matchCount, MaybeParsed<T> maybeParsed)
		{
			this.matchCount = matchCount;
			this.maybeParsed = maybeParsed;
			this.errorMessage = null;
		}

		public PartialResult(int matchCount, string errorMessage)
		{
			this.matchCount = matchCount;
			this.maybeParsed = MaybeParsed.NotParsed;
			this.errorMessage = errorMessage;
		}
	}

	public static Parser<T> Build(System.Func<Builder, Parser<T>> body)
	{
		return new LazyParser<T>(body);
	}

	public string expectedErrorMessage = "Invalid input";

	public Result Parse(string source, List<Token> tokens)
	{
		var result = PartialParse(source, tokens, 0);
		if (!result.IsSuccess)
			return new Result(result.matchCount, result.errorMessage);

		if (result.matchCount != tokens.Count || !result.maybeParsed.wasParsed)
			return new Result(result.matchCount, "Not a valid program");

		return new Result(result.maybeParsed.parsed);
	}

	public Parser<T> Expect(string expectedErrorMessage)
	{
		this.expectedErrorMessage = expectedErrorMessage;
		return this;
	}

	public Parser<T> Debug(System.Action<DebugParser<T>.DebugInfo> checkpoint)
	{
		return new DebugParser<T>(this, checkpoint);
	}

	public abstract PartialResult PartialParse(string source, List<Token> tokens, int index);
}
