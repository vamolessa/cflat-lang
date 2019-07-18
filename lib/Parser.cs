using System.Collections.Generic;

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
	}

	public readonly struct PartialOk
	{
		public readonly int matchCount;
		public readonly Option<T> maybeParsed;

		public PartialOk(int matchCount, Option<T> maybeParsed)
		{
			this.matchCount = matchCount;
			this.maybeParsed = maybeParsed;
		}
	}

	public readonly struct Error
	{
		public readonly int tokenIndex;
		public readonly string message;

		public Error(int tokenIndex, string message)
		{
			this.tokenIndex = tokenIndex;
			this.message = message;
		}
	}

	public static DeferParser<T> Declare()
	{
		return new DeferParser<T>();
	}

	public string expectedErrorMessage = "Invalid input";

	public Result<T, Error> Parse(string source, List<Token> tokens)
	{
		var result = PartialParse(source, tokens, 0);
		if (!result.IsOk)
			return Result.Error(new Error(result.error.tokenIndex, result.error.message));

		if (result.ok.matchCount != tokens.Count || !result.ok.maybeParsed.isSome)
			return Result.Error(new Error(result.error.tokenIndex, "Not a valid program"));

		return Result.Ok(result.ok.maybeParsed.value);
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

	public RepeatParser<T> AtLeast(int minRepeatCount)
	{
		return new RepeatParser<T>(this, minRepeatCount);
	}

	public MaybeParser<T> Maybe()
	{
		return new MaybeParser<T>(this);
	}

	public abstract Result<PartialOk, Error> PartialParse(string source, List<Token> tokens, int index);
}
