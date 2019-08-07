public static class CompilerParsingExtensions
{
	public static void Next(this Compiler compiler)
	{
		compiler.previousToken = compiler.currentToken;

		while (true)
		{
			compiler.currentToken = compiler.tokenizer.Next();
			if (compiler.currentToken.kind != TokenKind.Error)
				break;

			compiler.AddHardError(compiler.currentToken.slice, "Invalid char");
		}
	}

	public static bool Check(this Compiler compiler, TokenKind tokenKind)
	{
		return compiler.currentToken.kind == tokenKind;
	}

	public static bool Match(this Compiler compiler, TokenKind tokenKind)
	{
		if (compiler.currentToken.kind != tokenKind)
			return false;

		compiler.Next();
		return true;
	}

	public static void Consume(this Compiler compiler, TokenKind tokenKind, string errorMessage)
	{
		if (compiler.currentToken.kind == tokenKind)
			compiler.Next();
		else
			compiler.AddHardError(compiler.currentToken.slice, errorMessage);
	}

	public static void ParseWithPrecedence(this Compiler compiler, ParseRule[] parseRules, Precedence precedence)
	{
		compiler.Next();
		if (compiler.previousToken.kind == TokenKind.End)
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