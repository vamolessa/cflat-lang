using System.Collections.Generic;

public static class ExtraParsers
{
	public static OldParser<A> Debug<A>(this OldParser<A> self, System.Action<DebugParser<A>.DebugInfo> checkpoint)
	{
		return new DebugParser<A>(self, checkpoint);
	}

	public static RepeatWithSeparatorParser<A, B> RepeatWithSeparator<A, B>(this OldParser<A> self, OldParser<B> endParser, int separatorToken)
	{
		return new RepeatWithSeparatorParser<A, B>(self, endParser, separatorToken);
	}

	public static LeftAssociativeParser<T> LeftAssociative<T>(this OldParser<T> higherPrecedenceParser, params int[] operatorTokens)
	{
		return new LeftAssociativeParser<T>(higherPrecedenceParser, operatorTokens);
	}
}

public sealed class DebugParser<T> : OldParser<T>
{
	public readonly struct DebugInfo
	{
		public readonly OldParser<T> parser;
		public readonly Result<PartialOk, OldParser.Error> result;
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
			OldParser<T> parser,
			Result<PartialOk, OldParser.Error> result,
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

	private readonly OldParser<T> parser;
	private readonly System.Action<DebugInfo> checkpoint;

	public DebugParser(OldParser<T> parser, System.Action<DebugInfo> checkpoint)
	{
		this.parser = parser;
		this.checkpoint = checkpoint;
	}

	public override Result<PartialOk, OldParser.Error> PartialParse(string source, List<Token> tokens, int index)
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

public sealed class RepeatWithSeparatorParser<A, B> : OldParser<List<A>>
{
	private readonly OldParser<A> repeatParser;
	private readonly OldParser<B> endParser;
	private readonly int separatorToken;

	public RepeatWithSeparatorParser(OldParser<A> repeatParser, OldParser<B> endParser, int separatorToken)
	{
		this.repeatParser = repeatParser;
		this.endParser = endParser;
		this.separatorToken = separatorToken;
	}

	public override Result<PartialOk, OldParser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		var parsed = new List<A>();
		var initialIndex = index;

		do
		{
			var result = repeatParser.PartialParse(source, tokens, index);
			if (!result.isOk || result.ok.matchCount == 0)
				break;

			index += result.ok.matchCount;
			parsed.Add(result.ok.parsed);
		} while (MatchSeparator(tokens, ref index));

		{
			var result = endParser.PartialParse(source, tokens, index);
			if (!result.isOk)
				return Result.Error(result.error);
		}

		return Result.Ok(new PartialOk(index - initialIndex, parsed));
	}

	private bool MatchSeparator(List<Token> tokens, ref int index)
	{
		if (tokens[index].kind != separatorToken)
			return false;

		index += 1;
		return true;
	}
}

public sealed class LeftAssociativeParser<T> : OldParser<T>
{
	private readonly OldParser<T> higherPrecedenceParser;
	private readonly int[] operatorTokens;
	private System.Func<Token, T, T, T> aggregator;

	public LeftAssociativeParser(OldParser<T> higherPrecedenceParser, int[] operatorTokens)
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

	public override Result<PartialOk, OldParser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		var initialIndex = index;

		var result = higherPrecedenceParser.PartialParse(source, tokens, index);
		if (!result.isOk)
			return result;

		index += result.ok.matchCount;

		while (true)
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
