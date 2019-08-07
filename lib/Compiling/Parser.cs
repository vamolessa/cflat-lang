public delegate void ParseFunction(Compiler compiler, Precedence currentPrecedence);

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
	public Token previousToken;
	public Token currentToken;
	public Tokenizer tokenizer;

	public void Reset(Tokenizer tokenizer)
	{
		this.tokenizer = tokenizer;
		this.previousToken = new Token(TokenKind.End, new Slice());
		this.currentToken = new Token(TokenKind.End, new Slice());
	}

	public void Next(Compiler compiler)
	{
		previousToken = currentToken;

		while (true)
		{
			currentToken = tokenizer.Next();
			if (currentToken.kind != TokenKind.Error)
				break;

			compiler.AddHardError(currentToken.slice, "Invalid char");
		}
	}

	public bool Check(TokenKind tokenKind)
	{
		return currentToken.kind == tokenKind;
	}

	public bool Match(Compiler compiler, TokenKind tokenKind)
	{
		if (currentToken.kind != tokenKind)
			return false;

		Next(compiler);
		return true;
	}

	public void Consume(Compiler compiler, TokenKind tokenKind, string errorMessage)
	{
		if (currentToken.kind == tokenKind)
			Next(compiler);
		else
			compiler.AddHardError(compiler.currentToken.slice, errorMessage);
	}

	public void ParseWithPrecedence(Compiler compiler, ParseRule[] parseRules, Precedence precedence)
	{
		Next(compiler);
		if (previousToken.kind == TokenKind.End)
			return;

		var prefixRule = parseRules[(int)compiler.previousToken.kind].prefixRule;
		if (prefixRule == null)
		{
			compiler.AddHardError(compiler.previousToken.slice, "Expected expression");
			return;
		}
		prefixRule(compiler, precedence);

		while (
			compiler.currentToken.kind != TokenKind.End &&
			precedence <= parseRules[(int)compiler.currentToken.kind].precedence
		)
		{
			compiler.Next();
			var infixRule = parseRules[(int)compiler.previousToken.kind].infixRule;
			infixRule(compiler, precedence);
		}

		compiler.onParseWithPrecedence(compiler, precedence);
	}
}