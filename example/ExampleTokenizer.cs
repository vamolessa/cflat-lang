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
	Let,
	For,
	If,
	While,
	Return,

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
			new WhiteSpaceScanner().Ignore(),
			new EnclosedScanner("//", "\n").Ignore(),

			new IntegerNumberScanner().ForToken((int)ExampleTokenKind.IntegerNumber),
			new RealNumberScanner().ForToken((int)ExampleTokenKind.RealNumber),
			new EnclosedScanner("\"", "\"").ForToken((int)ExampleTokenKind.String),
			new ExactScanner("true").ForToken((int)ExampleTokenKind.True),
			new ExactScanner("false").ForToken((int)ExampleTokenKind.False),
			new ExactScanner("nil").ForToken((int)ExampleTokenKind.Nil),
			new IdentifierScanner("_").ForToken((int)ExampleTokenKind.Identifier),

			new ExactScanner("fn").ForToken((int)ExampleTokenKind.Function),
			new ExactScanner("let").ForToken((int)ExampleTokenKind.Let),
			new ExactScanner("for").ForToken((int)ExampleTokenKind.For),
			new ExactScanner("if").ForToken((int)ExampleTokenKind.If),
			new ExactScanner("while").ForToken((int)ExampleTokenKind.While),
			new ExactScanner("return").ForToken((int)ExampleTokenKind.Return),

			new ExactScanner("and").ForToken((int)ExampleTokenKind.And),
			new ExactScanner("or").ForToken((int)ExampleTokenKind.Or),

			new ExactScanner(",").ForToken((int)ExampleTokenKind.Colon),
			new ExactScanner(";").ForToken((int)ExampleTokenKind.Semicolon),
			new ExactScanner("(").ForToken((int)ExampleTokenKind.OpenParenthesis),
			new ExactScanner(")").ForToken((int)ExampleTokenKind.CloseParenthesis),
			new ExactScanner("{").ForToken((int)ExampleTokenKind.OpenCurlyBrackets),
			new ExactScanner("}").ForToken((int)ExampleTokenKind.CloseCurlyBrackets),

			new ExactScanner("+").ForToken((int)ExampleTokenKind.Sum),
			new ExactScanner("-").ForToken((int)ExampleTokenKind.Minus),
			new ExactScanner("*").ForToken((int)ExampleTokenKind.Asterisk),
			new ExactScanner("/").ForToken((int)ExampleTokenKind.Slash),

			new ExactScanner("=").ForToken((int)ExampleTokenKind.Equal),
			new ExactScanner("==").ForToken((int)ExampleTokenKind.EqualEqual),
			new ExactScanner("!=").ForToken((int)ExampleTokenKind.BangEqual),
			new ExactScanner("!").ForToken((int)ExampleTokenKind.Bang),

			new ExactScanner("<").ForToken((int)ExampleTokenKind.Lesser),
			new ExactScanner(">").ForToken((int)ExampleTokenKind.Greater),
			new ExactScanner("<=").ForToken((int)ExampleTokenKind.LesserEqual),
			new ExactScanner(">=").ForToken((int)ExampleTokenKind.GreaterEqual),
		};
	}

	public Result<List<Token>, List<int>> Tokenize(string source)
	{
		return Tokenizer.Tokenize(scanners, source);
	}
}