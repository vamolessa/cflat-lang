public delegate void ParseFunction(Precedence currentPrecedence);

public readonly struct ParseRule
{
	public readonly ParseFunction prefixRule;
	public readonly ParseFunction infixRule;
	public readonly Precedence precedence;

	public ParseRule(ParseFunction prefixRule, ParseFunction infixRule, Precedence precedence)
	{
		this.prefixRule = prefixRule;
		this.infixRule = infixRule;
		this.precedence = precedence;
	}
}

public sealed class Parser
{
	public readonly Tokenizer tokenizer;
	private readonly System.Action<Slice, string> onError;

	public Token previousToken;
	public Token currentToken;

	public Parser(Tokenizer tokenizer, System.Action<Slice, string> onError)
	{
		this.tokenizer = tokenizer;
		this.onError = onError;

		Reset();
	}

	public void Reset()
	{
		this.previousToken = new Token(TokenKind.End, new Slice());
		this.currentToken = new Token(TokenKind.End, new Slice());
	}

	public void Next()
	{
		previousToken = currentToken;

		while (true)
		{
			currentToken = tokenizer.Next();
			if (currentToken.kind != TokenKind.Error)
				break;

			onError(currentToken.slice, "Invalid char");
		}
	}

	public bool Check(TokenKind tokenKind)
	{
		return currentToken.kind == tokenKind;
	}

	public bool Match(TokenKind tokenKind)
	{
		if (currentToken.kind != tokenKind)
			return false;

		Next();
		return true;
	}

	public void Consume(TokenKind tokenKind, string errorMessage)
	{
		if (currentToken.kind == tokenKind)
			Next();
		else
			onError(currentToken.slice, errorMessage);
	}
}