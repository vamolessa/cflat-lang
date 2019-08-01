public static class LangParseRules
{
	public static readonly ParseRule[] rules = new ParseRule[(int)TokenKind.COUNT];

	public static void InitRules()
	{
		Set(TokenKind.OpenParenthesis, LangCompiler.Grouping, null, Precedence.Primary);
		Set(TokenKind.OpenCurlyBrackets, LangCompiler.Block, null, Precedence.None);
		Set(TokenKind.Minus, LangCompiler.Unary, LangCompiler.Binary, Precedence.Term);
		Set(TokenKind.Plus, null, LangCompiler.Binary, Precedence.Term);
		Set(TokenKind.Slash, null, LangCompiler.Binary, Precedence.Factor);
		Set(TokenKind.Asterisk, null, LangCompiler.Binary, Precedence.Factor);
		Set(TokenKind.Bang, LangCompiler.Unary, null, Precedence.None);
		Set(TokenKind.BangEqual, null, LangCompiler.Binary, Precedence.Equality);
		Set(TokenKind.EqualEqual, null, LangCompiler.Binary, Precedence.Equality);
		Set(TokenKind.Greater, null, LangCompiler.Binary, Precedence.Comparison);
		Set(TokenKind.GreaterEqual, null, LangCompiler.Binary, Precedence.Comparison);
		Set(TokenKind.Less, null, LangCompiler.Binary, Precedence.Comparison);
		Set(TokenKind.LessEqual, null, LangCompiler.Binary, Precedence.Comparison);
		Set(TokenKind.Identifier, LangCompiler.Variable, null, Precedence.None);
		Set(TokenKind.StringLiteral, LangCompiler.Literal, null, Precedence.None);
		Set(TokenKind.IntLiteral, LangCompiler.Literal, null, Precedence.None);
		Set(TokenKind.And, null, LangCompiler.And, Precedence.And);
		Set(TokenKind.False, LangCompiler.Literal, null, Precedence.None);
		Set(TokenKind.If, LangCompiler.If, null, Precedence.None);
		Set(TokenKind.Or, null, LangCompiler.Or, Precedence.Or);
		Set(TokenKind.FloatLiteral, LangCompiler.Literal, null, Precedence.None);
		Set(TokenKind.True, LangCompiler.Literal, null, Precedence.None);
	}

	private static void Set(TokenKind kind, ParseFunction prefix, ParseFunction infix, Precedence precedence)
	{
		rules[(int)kind] = new ParseRule(prefix, infix, (int)precedence);
	}
}