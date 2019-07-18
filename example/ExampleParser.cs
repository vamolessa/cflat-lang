using System.Text;

public sealed class ExampleParser
{
	public readonly ExampleTokenizer tokenizer;
	public readonly Parser<Expression> parser;

	public ExampleParser(ExampleTokenizer tokenizer)
	{
		this.tokenizer = tokenizer;

		var expression = Parser<Expression>.Declare();
		var equality = Parser<Expression>.Declare();
		var comparison = Parser<Expression>.Declare();
		var addition = Parser<Expression>.Declare();
		var multiplication = Parser<Expression>.Declare();
		var unary = Parser<Expression>.Declare();
		var primary = Parser<Expression>.Declare();

		parser = expression;

		expression.Build(b => equality);

		equality.Build(b => b.All(
			comparison,
			b.All(
				b.Any(
					b.Token((int)ExampleTokenKind.EqualEqual),
					b.Token((int)ExampleTokenKind.BangEqual)
				),
				comparison
			).AtLeast(0)
		));

		comparison.Build(b => b.All(
			addition,
			b.All(
				b.Any(
					b.Token((int)ExampleTokenKind.Lesser),
					b.Token((int)ExampleTokenKind.LesserEqual),
					b.Token((int)ExampleTokenKind.Greater),
					b.Token((int)ExampleTokenKind.GreaterEqual)
				),
				addition
			).AtLeast(0)
		));

		addition.Build(b => b.All(
			multiplication,
			b.All(
				b.Any(
					b.Token((int)ExampleTokenKind.Sum),
					b.Token((int)ExampleTokenKind.Minus)
				),
				multiplication
			).AtLeast(0)
		));

		multiplication.Build(b => b.All(
			unary,
			b.All(
				b.Any(
					b.Token((int)ExampleTokenKind.Asterisk),
					b.Token((int)ExampleTokenKind.Slash)
				),
				unary
			).AtLeast(0)
		));

		unary.Build(b => b.Any(
			b.All(
				b.Any(
					b.Token((int)ExampleTokenKind.Bang),
					b.Token((int)ExampleTokenKind.Minus)
				),
				unary
			),
			primary
		));

		primary.Build(b => b.Any(
			b.Token((int)ExampleTokenKind.IntegerNumber),
			b.Token((int)ExampleTokenKind.RealNumber),
			b.Token((int)ExampleTokenKind.String),
			b.Token((int)ExampleTokenKind.True),
			b.Token((int)ExampleTokenKind.False),
			b.Token((int)ExampleTokenKind.Nil),
			b.All(
				b.Token((int)ExampleTokenKind.OpenParenthesis),
				expression,
				b.Token((int)ExampleTokenKind.CloseParenthesis)
			)
		));

		/*
		Parser<Expression> valueParser = null;
		Parser<Expression> listParser = null;

		valueParser = Parser<Expression>.Build(builder => builder.Any(
			builder.Token((int)ExampleToken.IntegerNumber).As((s, t) =>
			{
				int value;
				var sub = s.Substring(t.index, t.length);
				if (!int.TryParse(sub, out value))
					return Option.None;
				return new ValueExpression(value);
			}),
			builder.Token((int)ExampleToken.RealNumber).As((s, t) =>
			{
				float value;
				var sub = s.Substring(t.index, t.length);
				if (!float.TryParse(sub, out value))
					return Option.None;
				return new ValueExpression(value);
			}),
			builder.Token((int)ExampleToken.String).As((s, t) =>
			{
				var sub = s.Substring(t.index + 1, t.length - 2).Replace("\\\"", "\"");
				return new ValueExpression(sub);
			}),
			builder.Token((int)ExampleToken.Identifier).As((s, t) =>
			{
				var sub = s.Substring(t.index, t.length);
				return new IdentifierExpression(sub);
			}),
			listParser
		)).Expect("Expected a number, string literal, identifier or list");

		listParser = Parser<Expression>.Build(builder => builder.All(
			builder.Token((int)ExampleToken.OpenParenthesis).Expect("Expected a '('"),
			valueParser.Maybe().AtLeast(0).As(es => new ListExpression(es)),
			builder.Token((int)ExampleToken.CloseParenthesis).Expect("Expected a ')'")
		)).Expect("Expected a list");

		parser = Parser<Expression>.Build(builder =>
			listParser.AtLeast(1).As(es => new ListExpression(es))
		);
		*/
	}

	public Result<Expression, string> Parse(string source)
	{
		var tokens = tokenizer.Tokenize(source);
		if (!tokens.IsOk)
		{
			var sb = new StringBuilder();
			foreach (var errorIndex in tokens.error)
			{
				var position = ParserHelper.GetLineAndColumn(source, errorIndex);
				sb.AppendLine(string.Format(
					"Unexpected char '{0}' at {1}\n{2}",
					source[errorIndex],
					position,
					ParserHelper.GetContext(source, position, 2)
				));
			}

			return Result.Error(sb.ToString());
		}

		var expression = parser.Parse(source, tokens.ok);
		if (expression.IsOk)
			return Result.Ok(expression.ok);

		{
			LineAndColumn position;
			if (expression.error.tokenIndex >= 0 && expression.error.tokenIndex < tokens.ok.Count)
			{
				var errorToken = tokens.ok[expression.error.tokenIndex];
				position = ParserHelper.GetLineAndColumn(source, errorToken.index);
			}
			else
			{
				var errorToken = tokens.ok[tokens.ok.Count - 1];
				position = ParserHelper.GetLineAndColumn(source, errorToken.index + errorToken.length);
			}

			return Result.Error(
				string.Format(
					"{0} at {1}\n{2}",
					expression.error.message,
					position,
					ParserHelper.GetContext(source, position, 2)
				)
			);
		}
	}
}
