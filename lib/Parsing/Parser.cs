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
	private readonly List<ParseError> errors = new List<ParseError>();
	private ITokenizer tokenizer;
	private Token previousToken;
	private Token currentToken;

	public void AddError(int sourceIndex, string message)
	{
		errors.Add(new ParseError(sourceIndex, message));
	}

	public void Begin(ITokenizer tokenizer)
	{
		errors.Clear();
		this.tokenizer = tokenizer;
		previousToken = new Token(Token.EndKind, 0, 0);
		currentToken = new Token(Token.EndKind, 0, 0);
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
}