using System.Collections.Generic;

public enum ExampleTokenKind
{
	IntegerNumber,
	RealNumber,
	String,
	True,
	False,
	Nil,
	Identifier,

	Function,
	For,
	If,
	Else,
	While,
	Return,
	Break,

	And,
	Or,

	Colon,
	Semicolon,
	OpenParenthesis,
	CloseParenthesis,
	OpenCurlyBrackets,
	CloseCurlyBrackets,

	Sum,
	Minus,
	Asterisk,
	Slash,

	Equal,
	EqualEqual,
	BangEqual,
	Bang,

	Lesser,
	Greater,
	LesserEqual,
	GreaterEqual,
}

public sealed class ExampleTokenizer
{
	public readonly Scanner[] scanners;

	public ExampleTokenizer()
	{
		scanners = new Scanner[] {
			new ExactScanner("fn").ForToken((int)ExampleTokenKind.Function),
			new ExactScanner("for").ForToken((int)ExampleTokenKind.For),
			new ExactScanner("if").ForToken((int)ExampleTokenKind.If),
			new ExactScanner("else").ForToken((int)ExampleTokenKind.Else),
			new ExactScanner("while").ForToken((int)ExampleTokenKind.While),
			new ExactScanner("return").ForToken((int)ExampleTokenKind.Return),

			new ExactScanner("and").ForToken((int)ExampleTokenKind.And),
			new ExactScanner("or").ForToken((int)ExampleTokenKind.Or),

			new CharScanner(',').ForToken((int)ExampleTokenKind.Colon),
			new CharScanner(';').ForToken((int)ExampleTokenKind.Semicolon),
			new CharScanner('(').ForToken((int)ExampleTokenKind.OpenParenthesis),
			new CharScanner(')').ForToken((int)ExampleTokenKind.CloseParenthesis),
			new CharScanner('{').ForToken((int)ExampleTokenKind.OpenCurlyBrackets),
			new CharScanner('}').ForToken((int)ExampleTokenKind.CloseCurlyBrackets),

			new CharScanner('+').ForToken((int)ExampleTokenKind.Sum),
			new CharScanner('-').ForToken((int)ExampleTokenKind.Minus),
			new CharScanner('*').ForToken((int)ExampleTokenKind.Asterisk),
			new CharScanner('/').ForToken((int)ExampleTokenKind.Slash),

			new CharScanner('=').ForToken((int)ExampleTokenKind.Equal),
			new ExactScanner("==").ForToken((int)ExampleTokenKind.EqualEqual),
			new ExactScanner("!=").ForToken((int)ExampleTokenKind.BangEqual),
			new ExactScanner("!").ForToken((int)ExampleTokenKind.Bang),

			new CharScanner('<').ForToken((int)ExampleTokenKind.Lesser),
			new CharScanner('>').ForToken((int)ExampleTokenKind.Greater),
			new ExactScanner("<=").ForToken((int)ExampleTokenKind.LesserEqual),
			new ExactScanner(">=").ForToken((int)ExampleTokenKind.GreaterEqual),

			new IntegerNumberScanner().ForToken((int)ExampleTokenKind.IntegerNumber),
			new RealNumberScanner().ForToken((int)ExampleTokenKind.RealNumber),
			new EnclosedScanner("\"", "\"").ForToken((int)ExampleTokenKind.String),
			new ExactScanner("true").ForToken((int)ExampleTokenKind.True),
			new ExactScanner("false").ForToken((int)ExampleTokenKind.False),
			new ExactScanner("nil").ForToken((int)ExampleTokenKind.Nil),
			new IdentifierScanner("_").ForToken((int)ExampleTokenKind.Identifier),

			new WhiteSpaceScanner().Ignore(),
			new EnclosedScanner("//", "\n").Ignore(),
		};
	}

	public Result<List<Token>, List<int>> Tokenize(string source)
	{
		return Tokenizer.Tokenize(scanners, source);
	}
}