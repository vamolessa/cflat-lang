using System.Collections.Generic;

public static class Parser
{
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

	public static TokenParser<Token> Token(int tokenKind)
	{
		return new TokenParser<Token>(tokenKind).As((s, t) => t);
	}

	public static TokenParser<T> Token<T>(int tokenKind, System.Func<string, Token, T> converter)
	{
		return new TokenParser<T>(tokenKind).As(converter);
	}
}

public abstract class Parser<T>
{
	public readonly struct PartialOk
	{
		public readonly int matchCount;
		public readonly T parsed;

		public PartialOk(int matchCount, T parsed)
		{
			this.matchCount = matchCount;
			this.parsed = parsed;
		}
	}

	public static DeferParser<T> Declare()
	{
		return new DeferParser<T>();
	}

	public static AnyParser<T> operator |(Parser<T> a, Parser<T> b)
	{
		return a.Or(b);
	}

	public Result<T, Parser.Error> Parse(string source, List<Token> tokens)
	{
		var result = PartialParse(source, tokens, 0);
		if (!result.isOk)
			return Result.Error(new Parser.Error(result.error.tokenIndex, result.error.message));

		if (result.ok.matchCount != tokens.Count)
			return Result.Error(new Parser.Error(result.error.tokenIndex, "Not a valid program"));

		return Result.Ok(result.ok.parsed);
	}

	public Parser<T> Debug(System.Action<DebugParser<T>.DebugInfo> checkpoint)
	{
		return new DebugParser<T>(this, checkpoint);
	}

	public virtual AnyParser<T> Or(Parser<T> other)
	{
		return new AnyParser<T>(this, other);
	}

	public RepeatParser<T> AtLeast(int minRepeatCount)
	{
		return new RepeatParser<T>(this, minRepeatCount);
	}

	public MaybeParser<T> Maybe()
	{
		return new MaybeParser<T>(this);
	}

	public Parser<R> Select<R>(System.Func<T, R> selector)
	{
		return new SelectParser<T, R>(this, selector);
	}

	public Parser<C> SelectMany<B, C>(System.Func<T, Parser<B>> parserSelector, System.Func<T, B, C> resultSelector)
	{
		return new SelectManyParser<T, B, C>(this, parserSelector, resultSelector);
	}

	public abstract Result<PartialOk, Parser.Error> PartialParse(string source, List<Token> tokens, int index);
}
