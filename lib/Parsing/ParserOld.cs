using System.Collections.Generic;

public sealed class ParseException : System.Exception
{
	public ParseException(string message) : base(message, null)
	{
	}
}

public sealed class ParserOld
{
	public string source;

	private readonly List<Token> tokens = new List<Token>();
	private readonly List<ParseError> errors = new List<ParseError>();
	private int nextIndex;

	public Result<T, List<ParseError>> Parse<T>(string source, Scanner[] scanners, System.Func<T> tryParse, System.Action recover = null)
	{
		this.source = source;
		tokens.Clear();
		errors.Clear();
		nextIndex = 0;

		var errorIndexes = new List<int>();
		Tokenizer.Tokenize(source, scanners, tokens, errorIndexes);
		if (errorIndexes.Count > 0)
		{
			foreach (var errorIndex in errorIndexes)
			{
				errors.Add(new ParseError(
					0,
					string.Format("Invalid char '{0}'", source[errorIndex])
				));
			}

			return Result.Error(errors);
		}

		while (!Check(Token.EndKind))
		{
			try
			{
				return Result.Ok(tryParse());
			}
			catch (ParseException e)
			{
				AddError(e.Message);
				if (recover != null)
					recover();
				else
					break;
			}
		}

		return Result.Error(errors);
	}

	public void AddError(string errorMessage)
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
			0,
			errorMessage
		));
	}

	public Token Peek()
	{
		return tokens[nextIndex];
	}

	public bool Match(int tokenKind)
	{
		if (!Check(tokenKind))
			return false;

		nextIndex += 1;
		return true;
	}

	public bool Check(int tokenKind)
	{
		return tokens[nextIndex].kind == tokenKind;
	}

	public bool CheckAny(int kindA, int kindB)
	{
		var tokenKind = Peek().kind;
		return
			tokenKind == kindA ||
			tokenKind == kindB;
	}

	public bool CheckAny(int kindA, int kindB, int kindC)
	{
		var tokenKind = Peek().kind;
		return
			tokenKind == kindA ||
			tokenKind == kindB ||
			tokenKind == kindC;
	}

	public bool CheckAny(int kindA, int kindB, int kindC, int kindD)
	{
		var tokenKind = Peek().kind;
		return
			tokenKind == kindA ||
			tokenKind == kindB ||
			tokenKind == kindC ||
			tokenKind == kindD;
	}

	public Token Next()
	{
		return tokens[nextIndex++];
	}

	public Token Consume(int tokenKind, string expectMessage)
	{
		if (Check(tokenKind))
			return Next();

		throw new ParseException(expectMessage);
	}
}