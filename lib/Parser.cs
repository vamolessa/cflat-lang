using System.Collections.Generic;

public struct ParseError
{
	public readonly LineAndColumn position;
	public readonly string message;

	public ParseError(LineAndColumn position, string message)
	{
		this.position = position;
		this.message = message;
	}
}

public sealed class ParseException : System.Exception
{
	public ParseException(string message) : base(message, null)
	{
	}
}

public abstract class Parser<T>
{
	protected string source;
	private readonly List<Token> tokens = new List<Token>();
	private readonly List<ParseError> errors = new List<ParseError>();
	private int nextIndex;

	public Result<T, List<ParseError>> Parse(string source, Scanner[] scanners)
	{
		tokens.Clear();
		errors.Clear();
		this.source = source;
		nextIndex = 0;

		var errorIndexes = new List<int>();
		Tokenizer.Tokenize(source, scanners, tokens, errorIndexes);
		if (errorIndexes.Count > 0)
		{
			foreach (var errorIndex in errorIndexes)
			{
				errors.Add(new ParseError(
					ParserHelper.GetLineAndColumn(source, errorIndex),
					string.Format("Invalid char '{0}'", source[errorIndex])
				));
			}

			return Result.Error(errors);
		}

		try
		{
			return Result.Ok(TryParse());
		}
		catch (ParseException e)
		{
			var errorIndex = 0;
			if (nextIndex < tokens.Count - 1)
			{
				errorIndex = tokens[nextIndex].index;
			}
			else if (tokens.Count > 1)
			{
				var lastToken = tokens[tokens.Count - 2];
				errorIndex = lastToken.index + lastToken.length;
			}

			errors.Add(new ParseError(
				ParserHelper.GetLineAndColumn(source, errorIndex),
				e.Message
			));
			return Result.Error(errors);
		}
	}

	protected abstract T TryParse();

	protected Token Peek()
	{
		return tokens[nextIndex];
	}

	protected bool Match(int tokenKind)
	{
		if (!Check(tokenKind))
			return false;

		nextIndex += 1;
		return true;
	}

	protected bool Check(int tokenKind)
	{
		return tokens[nextIndex].kind == tokenKind;
	}

	protected bool CheckAny(int kindA, int kindB)
	{
		var tokenKind = Peek().kind;
		return
			tokenKind == kindA ||
			tokenKind == kindB;
	}

	protected bool CheckAny(int kindA, int kindB, int kindC)
	{
		var tokenKind = Peek().kind;
		return
			tokenKind == kindA ||
			tokenKind == kindB ||
			tokenKind == kindC;
	}

	protected bool CheckAny(int kindA, int kindB, int kindC, int kindD)
	{
		var tokenKind = Peek().kind;
		return
			tokenKind == kindA ||
			tokenKind == kindB ||
			tokenKind == kindC ||
			tokenKind == kindD;
	}

	protected Token Next()
	{
		return tokens[nextIndex++];
	}

	protected Token Consume(int tokenKind, string expectMessage)
	{
		if (Check(tokenKind))
			return Next();

		throw new ParseException(expectMessage);
	}
}