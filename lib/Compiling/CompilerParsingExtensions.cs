public static class CompilerParsingExtensions
{
	public static void Next(this Compiler compiler)
	{
		compiler.previousToken = compiler.currentToken;

		while (true)
		{
			compiler.currentToken = compiler.tokenizer.Next();
			if (compiler.currentToken.kind != Token.ErrorKind)
				break;

			compiler.AddHardError(compiler.currentToken.slice, "Invalid char");
		}
	}

	public static bool Check(this Compiler compiler, int tokenKind)
	{
		return compiler.currentToken.kind == tokenKind;
	}

	public static bool Match(this Compiler compiler, int tokenKind)
	{
		if (compiler.currentToken.kind != tokenKind)
			return false;

		compiler.Next();
		return true;
	}

	public static bool Consume(this Compiler compiler, int tokenKind, string errorMessage)
	{
		if (compiler.currentToken.kind == tokenKind)
		{
			compiler.Next();
			return true;
		}
		else
		{
			compiler.AddHardError(compiler.currentToken.slice, errorMessage);
			return false;
		}
	}

	public static void ParseWithPrecedence(this Compiler compiler, ParseRule[] parseRules, int precedence)
	{
		compiler.Next();
		if (compiler.previousToken.kind == Token.EndKind)
			return;

		var prefixRule = parseRules[compiler.previousToken.kind].prefixRule;
		if (prefixRule == null)
		{
			compiler.AddHardError(compiler.previousToken.slice, "Expected expression");
			return;
		}
		prefixRule(compiler, precedence);

		while (
			compiler.currentToken.kind != Token.EndKind &&
			precedence <= parseRules[compiler.currentToken.kind].precedence
		)
		{
			compiler.Next();
			var infixRule = parseRules[compiler.previousToken.kind].infixRule;
			infixRule(compiler, precedence);
		}

		compiler.onParseWithPrecedence(compiler, precedence);
	}
}