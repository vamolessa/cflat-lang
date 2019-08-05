public enum TokenKind
{
	IntLiteral, FloatLiteral, StringLiteral, True, False, Identifier,
	Function, Struct, For, If, Else, While, Return, Break,
	And, Or, Dot, Comma, Colon,

	Let, Mut,

	Print,

	OpenParenthesis, CloseParenthesis, OpenCurlyBrackets, CloseCurlyBrackets,

	Plus, Minus, Asterisk, Slash,
	Equal, EqualEqual, BangEqual, Bang,
	Less, Greater, LessEqual, GreaterEqual,

	COUNT
}

public enum Precedence
{
	None,
	Assignment, // =
	Or, // or
	And, // and
	Equality, // == !=
	Comparison, // < > <= >=
	Term,// + -
	Factor, // * /
	Unary, // ! -
	Call, // . () []
	Primary
}

public static class LangScanners
{
	public static readonly Scanner[] scanners = new Scanner[] {
		new ExactScanner("fn").ForToken((int)TokenKind.Function),
		new ExactScanner("struct").ForToken((int)TokenKind.Struct),
		new ExactScanner("for").ForToken((int)TokenKind.For),
		new ExactScanner("if").ForToken((int)TokenKind.If),
		new ExactScanner("else").ForToken((int)TokenKind.Else),
		new ExactScanner("while").ForToken((int)TokenKind.While),
		new ExactScanner("return").ForToken((int)TokenKind.Return),
		new ExactScanner("break").ForToken((int)TokenKind.Break),
		new ExactScanner("and").ForToken((int)TokenKind.And),
		new ExactScanner("or").ForToken((int)TokenKind.Or),

		new ExactScanner("let").ForToken((int)TokenKind.Let),
		new ExactScanner("mut").ForToken((int)TokenKind.Mut),

		new ExactScanner("print").ForToken((int)TokenKind.Print),

		new CharScanner('.').ForToken((int)TokenKind.Dot),
		new CharScanner(',').ForToken((int)TokenKind.Comma),
		new CharScanner(':').ForToken((int)TokenKind.Colon),
		new CharScanner('(').ForToken((int)TokenKind.OpenParenthesis),
		new CharScanner(')').ForToken((int)TokenKind.CloseParenthesis),
		new CharScanner('{').ForToken((int)TokenKind.OpenCurlyBrackets),
		new CharScanner('}').ForToken((int)TokenKind.CloseCurlyBrackets),

		new CharScanner('+').ForToken((int)TokenKind.Plus),
		new CharScanner('-').ForToken((int)TokenKind.Minus),
		new CharScanner('*').ForToken((int)TokenKind.Asterisk),
		new CharScanner('/').ForToken((int)TokenKind.Slash),

		new CharScanner('=').ForToken((int)TokenKind.Equal),
		new ExactScanner("==").ForToken((int)TokenKind.EqualEqual),
		new ExactScanner("!=").ForToken((int)TokenKind.BangEqual),
		new ExactScanner("!").ForToken((int)TokenKind.Bang),

		new CharScanner('<').ForToken((int)TokenKind.Less),
		new CharScanner('>').ForToken((int)TokenKind.Greater),
		new ExactScanner("<=").ForToken((int)TokenKind.LessEqual),
		new ExactScanner(">=").ForToken((int)TokenKind.GreaterEqual),

		new IntegerNumberScanner().ForToken((int)TokenKind.IntLiteral),
		new RealNumberScanner().ForToken((int)TokenKind.FloatLiteral),
		new EnclosedScanner("\"", "\"").ForToken((int)TokenKind.StringLiteral),
		new ExactScanner("true").ForToken((int)TokenKind.True),
		new ExactScanner("false").ForToken((int)TokenKind.False),
		new IdentifierScanner("_").ForToken((int)TokenKind.Identifier),

		new WhiteSpaceScanner().Ignore(),
		new EnclosedScanner("//", "\n").Ignore(),
	};
}