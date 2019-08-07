public static class PepperScanners
{
	public static readonly Scanner[] scanners = new Scanner[] {
		new ExactScanner("fn").ForToken(TokenKind.Function),
		new ExactScanner("struct").ForToken(TokenKind.Struct),
		new ExactScanner("for").ForToken(TokenKind.For),
		new ExactScanner("if").ForToken(TokenKind.If),
		new ExactScanner("else").ForToken(TokenKind.Else),
		new ExactScanner("while").ForToken(TokenKind.While),
		new ExactScanner("return").ForToken(TokenKind.Return),
		new ExactScanner("break").ForToken(TokenKind.Break),
		new ExactScanner("and").ForToken(TokenKind.And),
		new ExactScanner("or").ForToken(TokenKind.Or),

		new ExactScanner("let").ForToken(TokenKind.Let),
		new ExactScanner("mut").ForToken(TokenKind.Mut),

		new ExactScanner("bool").ForToken(TokenKind.Bool),
		new ExactScanner("int").ForToken(TokenKind.Int),
		new ExactScanner("float").ForToken(TokenKind.Float),
		new ExactScanner("string").ForToken(TokenKind.String),

		new ExactScanner("print").ForToken(TokenKind.Print),

		new CharScanner('.').ForToken(TokenKind.Dot),
		new CharScanner(',').ForToken(TokenKind.Comma),
		new CharScanner(':').ForToken(TokenKind.Colon),
		new CharScanner('(').ForToken(TokenKind.OpenParenthesis),
		new CharScanner(')').ForToken(TokenKind.CloseParenthesis),
		new CharScanner('{').ForToken(TokenKind.OpenCurlyBrackets),
		new CharScanner('}').ForToken(TokenKind.CloseCurlyBrackets),

		new CharScanner('+').ForToken(TokenKind.Plus),
		new CharScanner('-').ForToken(TokenKind.Minus),
		new CharScanner('*').ForToken(TokenKind.Asterisk),
		new CharScanner('/').ForToken(TokenKind.Slash),

		new CharScanner('=').ForToken(TokenKind.Equal),
		new ExactScanner("==").ForToken(TokenKind.EqualEqual),
		new ExactScanner("!=").ForToken(TokenKind.BangEqual),
		new ExactScanner("!").ForToken(TokenKind.Bang),

		new CharScanner('<').ForToken(TokenKind.Less),
		new CharScanner('>').ForToken(TokenKind.Greater),
		new ExactScanner("<=").ForToken(TokenKind.LessEqual),
		new ExactScanner(">=").ForToken(TokenKind.GreaterEqual),

		new IntegerNumberScanner().ForToken(TokenKind.IntLiteral),
		new RealNumberScanner().ForToken(TokenKind.FloatLiteral),
		new EnclosedScanner("\"", "\"").ForToken(TokenKind.StringLiteral),
		new ExactScanner("true").ForToken(TokenKind.True),
		new ExactScanner("false").ForToken(TokenKind.False),
		new IdentifierScanner("_").ForToken(TokenKind.Identifier),

		new WhiteSpaceScanner().Ignore(),
		new EnclosedScanner("//", "\n").Ignore(),
	};
}