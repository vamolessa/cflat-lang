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

		expression.parser = equality;

		equality.parser = new LeftAssociativeParser<Expression>(
			comparison,
			(int)ExampleTokenKind.EqualEqual,
			(int)ExampleTokenKind.BangEqual
		).Aggregate((t, l, r) => new BinaryOperationExpression(t, l, r));

		comparison.parser = new LeftAssociativeParser<Expression>(
			addition,
			(int)ExampleTokenKind.Lesser,
			(int)ExampleTokenKind.LesserEqual,
			(int)ExampleTokenKind.Greater,
			(int)ExampleTokenKind.GreaterEqual
		).Aggregate((t, l, r) => new BinaryOperationExpression(t, l, r));

		addition.parser = new LeftAssociativeParser<Expression>(
			multiplication,
			(int)ExampleTokenKind.Sum,
			(int)ExampleTokenKind.Minus
		).Aggregate((t, l, r) => new BinaryOperationExpression(t, l, r));

		multiplication.parser = new LeftAssociativeParser<Expression>(
			unary,
			(int)ExampleTokenKind.Asterisk,
			(int)ExampleTokenKind.Slash
		).Aggregate((t, l, r) => new BinaryOperationExpression(t, l, r));

		unary.parser = (
			(
				from op in (
					Parser.Token((int)ExampleTokenKind.Bang) |
					Parser.Token((int)ExampleTokenKind.Minus)
				)
				from un in unary
				select new UnaryOperationExpression(op, un) as Expression
			) |
			primary
		);

		primary.parser = (
			Parser.Token((int)ExampleTokenKind.IntegerNumber, (s, t) =>
			{
				var sub = s.Substring(t.index, t.length);
				var value = int.Parse(sub);
				return new ValueExpression(t, value) as Expression;
			}) |
			Parser.Token((int)ExampleTokenKind.RealNumber, (s, t) =>
			{
				var sub = s.Substring(t.index, t.length);
				var value = float.Parse(sub);
				return new ValueExpression(t, value) as Expression;
			}) |
			Parser.Token((int)ExampleTokenKind.String, (s, t) =>
			{
				var sub = s.Substring(t.index + 1, t.length - 2);
				return new ValueExpression(t, sub) as Expression;
			}) |
			Parser.Token((int)ExampleTokenKind.True, (s, t) =>
			{
				return new ValueExpression(t, true) as Expression;
			}) |
			Parser.Token((int)ExampleTokenKind.False, (s, t) =>
			{
				return new ValueExpression(t, false) as Expression;
			}) |
			Parser.Token((int)ExampleTokenKind.Nil, (s, t) =>
			{
				return new ValueExpression(t, null) as Expression;
			}) |
			from open in Parser.Token((int)ExampleTokenKind.OpenParenthesis)
			from exp in expression
			from close in Parser.Token((int)ExampleTokenKind.CloseParenthesis)
			select new GroupExpression(open, close, exp) as Expression
		).Expect("Expected a value or identifier");
	}

	public Result<Expression, string> Parse(string source)
	{
		var tokens = tokenizer.Tokenize(source);
		if (!tokens.isOk)
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
		if (expression.isOk)
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
