public static class LangParseRules
{
	public static void InitRulesFor(LangCompiler c)
	{
		Set(c, TokenKind.OpenParenthesis, c.Grouping, c.Call, Precedence.Call);
		Set(c, TokenKind.OpenCurlyBrackets, c.Block, null, Precedence.None);
		Set(c, TokenKind.Minus, c.Unary, c.Binary, Precedence.Term);
		Set(c, TokenKind.Plus, null, c.Binary, Precedence.Term);
		Set(c, TokenKind.Slash, null, c.Binary, Precedence.Factor);
		Set(c, TokenKind.Asterisk, null, c.Binary, Precedence.Factor);
		Set(c, TokenKind.Bang, c.Unary, null, Precedence.None);
		Set(c, TokenKind.BangEqual, null, c.Binary, Precedence.Equality);
		Set(c, TokenKind.EqualEqual, null, c.Binary, Precedence.Equality);
		Set(c, TokenKind.Greater, null, c.Binary, Precedence.Comparison);
		Set(c, TokenKind.GreaterEqual, null, c.Binary, Precedence.Comparison);
		Set(c, TokenKind.Less, null, c.Binary, Precedence.Comparison);
		Set(c, TokenKind.LessEqual, null, c.Binary, Precedence.Comparison);
		Set(c, TokenKind.Identifier, c.Variable, null, Precedence.None);
		Set(c, TokenKind.StringLiteral, c.Literal, null, Precedence.None);
		Set(c, TokenKind.IntLiteral, c.Literal, null, Precedence.None);
		Set(c, TokenKind.And, null, c.And, Precedence.And);
		Set(c, TokenKind.False, c.Literal, null, Precedence.None);
		Set(c, TokenKind.If, c.If, null, Precedence.None);
		Set(c, TokenKind.Or, null, c.Or, Precedence.Or);
		Set(c, TokenKind.FloatLiteral, c.Literal, null, Precedence.None);
		Set(c, TokenKind.True, c.Literal, null, Precedence.None);
		Set(c, TokenKind.Function, c.FunctionExpression, null, Precedence.None);
	}

	private static void Set(LangCompiler c, TokenKind kind, ParseFunction prefix, ParseFunction infix, Precedence precedence)
	{
		c.rules[(int)kind] = new ParseRule(prefix, infix, precedence);
	}
}