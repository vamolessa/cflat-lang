using System.Collections.Generic;

public static class OldParser
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

	private static readonly EndParser endParser = new EndParser();

	public static DeferParser<T> Declare<T>()
	{
		return new DeferParser<T>();
	}

	public static EndParser End()
	{
		return endParser;
	}

	public static TokenParser<Token> Token(int tokenKind)
	{
		return new TokenParser<Token>(tokenKind, (s, t) => t);
	}

	public static TokenParser<T> Token<T>(int tokenKind, System.Func<string, Token, T> converter)
	{
		return new TokenParser<T>(tokenKind, converter);
	}

	public static AnyParser<T> Any<T>(params OldParser<T>[] parsers)
	{
		return new AnyParser<T>(parsers);
	}
}

public abstract class OldParser<T>
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

	public Result<T, OldParser.Error> Parse(string source, List<Token> tokens)
	{
		var result = PartialParse(source, tokens, 0);
		if (!result.isOk)
			return Result.Error(new OldParser.Error(result.error.tokenIndex, result.error.message));

		if (result.ok.matchCount != tokens.Count)
			return Result.Error(new OldParser.Error(result.error.tokenIndex, "Not a valid program"));

		return Result.Ok(result.ok.parsed);
	}

	public abstract Result<PartialOk, OldParser.Error> PartialParse(string source, List<Token> tokens, int index);
}
