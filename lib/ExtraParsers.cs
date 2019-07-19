using System.Collections.Generic;

public static class ExtraParsers
{
	public static RepeatWithSeparatorParser<T> RepeatWithSeparator<T>(this Parser<T> parser, int separatorToken)
	{
		return new RepeatWithSeparatorParser<T>(parser, separatorToken);
	}

	public static LeftAssociativeParser<T> LeftAssociative<T>(this Parser<T> higherPrecedenceParser, params int[] operatorTokens)
	{
		return new LeftAssociativeParser<T>(higherPrecedenceParser, operatorTokens);
	}
}

public sealed class RepeatWithSeparatorParser<T> : Parser<List<T>>
{
	private readonly Parser<T> parser;
	private readonly int separatorToken;

	public RepeatWithSeparatorParser(Parser<T> parser, int separatorToken)
	{
		this.parser = parser;
		this.separatorToken = separatorToken;
	}

	public override Result<PartialOk, Parser.Error> PartialParse(string source, List<Token> tokens, int index)
	{
		var parsed = new List<T>();
		var initialIndex = index;

		if (index >= tokens.Count)
			return Result.Ok(new PartialOk(0, parsed));

		do
		{
			var result = parser.PartialParse(source, tokens, index);
			if (!result.isOk)
				return Result.Error(result.error);

			if (result.ok.matchCount == 0)
				break;

			index += result.ok.matchCount;
			parsed.Add(result.ok.parsed);
		} while (MatchSeparator(tokens, ref index));

		return Result.Ok(new PartialOk(index - initialIndex, parsed));
	}

	private bool MatchSeparator(List<Token> tokens, ref int index)
	{
		if (index >= tokens.Count || tokens[index].kind != separatorToken)
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
