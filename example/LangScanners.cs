public enum TokenKind
{
	IntegerNumber, RealNumber, String, True, False, Nil, Identifier,
	Function, For, If, Else, While, Return, Break,
	And, Or, Dot, Comma, Semicolon,

	OpenParenthesis, CloseParenthesis, OpenCurlyBrackets, CloseCurlyBrackets,

	Sum, Minus, Asterisk, Slash,
	Equal, EqualEqual, BangEqual, Bang,
	Lesser, Greater, LesserEqual, GreaterEqual,
}

public static class LangScanners
{
	public static readonly Scanner[] scanners = new Scanner[] {
		new ExactScanner("fn").ForToken((int)TokenKind.Function),
		new ExactScanner("for").ForToken((int)TokenKind.For),
		new ExactScanner("if").ForToken((int)TokenKind.If),
		new ExactScanner("else").ForToken((int)TokenKind.Else),
		new ExactScanner("while").ForToken((int)TokenKind.While),
		new ExactScanner("return").ForToken((int)TokenKind.Return),

		new ExactScanner("and").ForToken((int)TokenKind.And),
		new ExactScanner("or").ForToken((int)TokenKind.Or),

		new CharScanner('.').ForToken((int)TokenKind.Dot),
		new CharScanner(',').ForToken((int)TokenKind.Comma),
		new CharScanner(';').ForToken((int)TokenKind.Semicolon),
		new CharScanner('(').ForToken((int)TokenKind.OpenParenthesis),
		new CharScanner(')').ForToken((int)TokenKind.CloseParenthesis),
		new CharScanner('{').ForToken((int)TokenKind.OpenCurlyBrackets),
		new CharScanner('}').ForToken((int)TokenKind.CloseCurlyBrackets),

		new CharScanner('+').ForToken((int)TokenKind.Sum),
		new CharScanner('-').ForToken((int)TokenKind.Minus),
		new CharScanner('*').ForToken((int)TokenKind.Asterisk),
		new CharScanner('/').ForToken((int)TokenKind.Slash),

		new CharScanner('=').ForToken((int)TokenKind.Equal),
		new ExactScanner("==").ForToken((int)TokenKind.EqualEqual),
		new ExactScanner("!=").ForToken((int)TokenKind.BangEqual),
		new ExactScanner("!").ForToken((int)TokenKind.Bang),

		new CharScanner('<').ForToken((int)TokenKind.Lesser),
		new CharScanner('>').ForToken((int)TokenKind.Greater),
		new ExactScanner("<=").ForToken((int)TokenKind.LesserEqual),
		new ExactScanner(">=").ForToken((int)TokenKind.GreaterEqual),

		new IntegerNumberScanner().ForToken((int)TokenKind.IntegerNumber),
		new RealNumberScanner().ForToken((int)TokenKind.RealNumber),
		new EnclosedScanner("\"", "\"").ForToken((int)TokenKind.String),
		new ExactScanner("true").ForToken((int)TokenKind.True),
		new ExactScanner("false").ForToken((int)TokenKind.False),
		new ExactScanner("nil").ForToken((int)TokenKind.Nil),
		new IdentifierScanner("_").ForToken((int)TokenKind.Identifier),

		new WhiteSpaceScanner().Ignore(),
		new EnclosedScanner("//", "\n").Ignore(),
	};
}