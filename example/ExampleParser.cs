using System.Collections.Generic;
using System.Text;

public sealed class ExampleParser
{
	public readonly ExampleTokenizer tokenizer;
	public readonly Parser<Expression> parser;

	public ExampleParser(ExampleTokenizer tokenizer)
	{
		this.tokenizer = tokenizer;

		var program = Parser.Declare<Expression>();
		var expression = Parser.Declare<Expression>();
		var declaration = Parser.Declare<Expression>();
		var assignment = Parser.Declare<Expression>();
		var logicOr = Parser.Declare<Expression>();
		var logicAnd = Parser.Declare<Expression>();
		var equality = Parser.Declare<Expression>();
		var comparison = Parser.Declare<Expression>();
		var addition = Parser.Declare<Expression>();
		var multiplication = Parser.Declare<Expression>();
		var unary = Parser.Declare<Expression>();
		var primary = Parser.Declare<Expression>();

		parser = program;

		program.parser =
			from exps in expression.RepeatUntil(Parser.End())
			from _ in Parser.End()
			select exps.Count > 0 ?
				exps[exps.Count - 1] :
				ValueExpression.New(Token.EndToken, null);

		expression.parser = declaration;

		declaration.parser = Parser.Any(
			from lt in Parser.Token((int)ExampleTokenKind.Let)
			from id in Parser.Token((int)ExampleTokenKind.Identifier)
			from eq in Parser.Token((int)ExampleTokenKind.Equal)
			from ex in assignment
			select new DeclarationExpression
			{
				identifierToken = id,
				expression = ex
			} as Expression,
			assignment
		);

		assignment.parser = Parser.Any(
			from id in Parser.Token((int)ExampleTokenKind.Identifier)
			from eq in Parser.Token((int)ExampleTokenKind.Equal)
			from ex in assignment
			select new AssignmentExpression
			{
				identifierToken = id,
				expression = ex
			} as Expression,
			logicOr
		);

		logicOr.parser = ExtraParsers.LeftAssociative(
			logicAnd,
			(int)ExampleTokenKind.Or
		).Aggregate(LogicOperationExpression.New);

		logicAnd.parser = ExtraParsers.LeftAssociative(
			equality,
			(int)ExampleTokenKind.And
		).Aggregate(LogicOperationExpression.New);

		equality.parser = ExtraParsers.LeftAssociative(
			comparison,
			(int)ExampleTokenKind.EqualEqual,
			(int)ExampleTokenKind.BangEqual
		).Aggregate(BinaryOperationExpression.New);

		comparison.parser = ExtraParsers.LeftAssociative(
			addition,
			(int)ExampleTokenKind.Lesser,
			(int)ExampleTokenKind.LesserEqual,
			(int)ExampleTokenKind.Greater,
			(int)ExampleTokenKind.GreaterEqual
		).Aggregate(BinaryOperationExpression.New);

		addition.parser = ExtraParsers.LeftAssociative(
			multiplication,
			(int)ExampleTokenKind.Sum,
			(int)ExampleTokenKind.Minus
		).Aggregate(BinaryOperationExpression.New);

		multiplication.parser = ExtraParsers.LeftAssociative(
			unary,
			(int)ExampleTokenKind.Asterisk,
			(int)ExampleTokenKind.Slash
		).Aggregate(BinaryOperationExpression.New);

		unary.parser = Parser.Any(
			from op in Parser.Any(
				Parser.Token((int)ExampleTokenKind.Bang),
				Parser.Token((int)ExampleTokenKind.Minus)
			)
			from un in unary
			select new UnaryOperationExpression
			{
				token = op,
				expression = un
			} as Expression,
			primary
		);

		primary.parser = Parser.Any(
			Parser.Token((int)ExampleTokenKind.IntegerNumber, (s, t) =>
			{
				var sub = s.Substring(t.index, t.length);
				var value = int.Parse(sub);
				return ValueExpression.New(t, value);
			}),
			Parser.Token((int)ExampleTokenKind.RealNumber, (s, t) =>
			{
				var sub = s.Substring(t.index, t.length);
				var value = float.Parse(sub);
				return ValueExpression.New(t, value);
			}),
			Parser.Token((int)ExampleTokenKind.String, (s, t) =>
			{
				var value = s.Substring(t.index + 1, t.length - 2);
				return ValueExpression.New(t, value);
			}),
			Parser.Token((int)ExampleTokenKind.True, (s, t) =>
			{
				return ValueExpression.New(t, true);
			}),
			Parser.Token((int)ExampleTokenKind.False, (s, t) =>
			{
				return ValueExpression.New(t, false);
			}),
			Parser.Token((int)ExampleTokenKind.Nil, (s, t) =>
			{
				return ValueExpression.New(t, null);
			}),
			Parser.Token((int)ExampleTokenKind.Identifier, (s, t) =>
			{
				var name = s.Substring(t.index, t.length);
				return new VariableExpression { token = t, name = name } as Expression;
			}),
			from open in Parser.Token((int)ExampleTokenKind.OpenParenthesis)
			from exp in expression
			from close in Parser.Token((int)ExampleTokenKind.CloseParenthesis)
			select new GroupExpression
			{
				leftToken = open,
				rightToken = close,
				expression = exp
			} as Expression
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
