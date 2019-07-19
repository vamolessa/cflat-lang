using System.Collections.Generic;

public static class ExtraParsers
{
	public static Parser<A> Debug<A>(this Parser<A> self, System.Action<DebugParser<A>.DebugInfo> checkpoint)
	{
		return new DebugParser<A>(self, checkpoint);
	}

	public static RepeatWithSeparatorParser<A, B> RepeatWithSeparator<A, B>(this Parser<A> self, Parser<B> endParser, int separatorToken)
	{
		return new RepeatWithSeparatorParser<A, B>(self, endParser, separatorToken);
	}

	public static LeftAssociativeParser<T> LeftAssociative<T>(this Parser<T> higherPrecedenceParser, params int[] operatorTokens)
	{
		return new LeftAssociativeParser<T>(higherPrecedenceParser, operatorTokens);
	}
}

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

public sealed class RepeatWithSeparatorParser<A, B> : Parser<List<A>>
{
	private readonly Parser<A> repeatParser;
	private readonly Parser<B> endParser;
	private readonly int separatorToken;

	public RepeatWithSeparatorParser(Parser<A> repeatParser, Parser<B> endParser, int separatorToken)
	{
		this.repeatParser = repeatParser;
		this.endParser = endParser;
		this.separatorToken = separatorToken;
	}

	public override Result<PartialOk, Parser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		var parsed = new List<A>();
		var initialIndex = index;

		do
		{
			var repeatResult = repeatParser.PartialParse(source, tokens, index);
			if (!repeatResult.isOk)
			{
				var endResult = endParser.PartialParse(source, tokens, index);
				if (endResult.isOk)
					break;
				else
					return Result.Error(repeatResult.error);
			}

			if (repeatResult.ok.matchCount == 0)
				break;

			index += repeatResult.ok.matchCount;
			parsed.Add(repeatResult.ok.parsed);
		} while (MatchSeparator(tokens, ref index));

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

public sealed class LeftAssociativeParser<T> : Parser<T>
{
	private readonly Parser<T> higherPrecedenceParser;
	private readonly int[] operatorTokens;
	private System.Func<Token, T, T, T> aggregator;

	public LeftAssociativeParser(Parser<T> higherPrecedenceParser, int[] operatorTokens)
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
