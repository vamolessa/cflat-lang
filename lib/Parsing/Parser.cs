using System.Collections.Generic;

public readonly struct ParseError
{
	public readonly int sourceIndex;
	public readonly string message;

	public ParseError(int sourceIndex, string message)
	{
		this.sourceIndex = sourceIndex;
		this.message = message;
	}
}

public sealed class Parser
{
	public readonly List<ParseError> errors = new List<ParseError>();
	internal Token previousToken;
	internal Token currentToken;

	private ITokenizer tokenizer;
	private bool panicMode;

	public void AddError(int sourceIndex, string message)
	{
		if (panicMode)
			return;

		panicMode = true;
		errors.Add(new ParseError(sourceIndex, message));
	}

	public void Begin(ITokenizer tokenizer)
	{
		errors.Clear();
		this.tokenizer = tokenizer;
		previousToken = new Token(Token.EndKind, 0, 0);
		currentToken = new Token(Token.EndKind, 0, 0);
		panicMode = false;
	}

	public void Next()
	{
		previousToken = currentToken;

		while (true)
		{
			currentToken = tokenizer.Next();
			if (currentToken.kind != Token.ErrorKind)
				break;

			AddError(currentToken.index, "Invalid char");
		}
	}

	public void Consume(int tokenKind, string errorMessage)
	{
		if (currentToken.kind == tokenKind)
			Next();
		else
			AddError(currentToken.index, errorMessage);
	}

	public T Convert<T>(System.Func<string, Token, T> converter)
	{
		return tokenizer.Convert(previousToken, converter);
	}
}