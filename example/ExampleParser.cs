using System.Text;

public sealed class ExampleParser
{
	private enum TokenKind
	{
		OpenParenthesis,
		CloseParenthesis,
		OpenCurlyBrackets,
		CloseCurlyBrackets,
		IntegerNumber,
		RealNumber,
		String,
		Identifier,
		Sum,
		Minus,
		Asterisk,
		Slash,
	}

	public readonly Scanner[] scanners;
	public readonly Parser<Expression> parser;

	public ExampleParser()
	{
		scanners = new Scanner[] {
			new WhiteSpaceScanner().Ignore(),
			new EnclosedScanner("//", "\n").Ignore(),

			new IntegerNumberScanner().WithToken((int)TokenKind.IntegerNumber),
			new RealNumberScanner().WithToken((int)TokenKind.RealNumber),
			new EnclosedScanner("\"", "\"").WithToken((int)TokenKind.String),
			new IdentifierScanner("_").WithToken((int)TokenKind.Identifier),

			new ExactScanner("(").WithToken((int)TokenKind.OpenParenthesis),
			new ExactScanner(")").WithToken((int)TokenKind.CloseParenthesis),
			new ExactScanner("{").WithToken((int)TokenKind.OpenCurlyBrackets),
			new ExactScanner("}").WithToken((int)TokenKind.CloseCurlyBrackets),


		};

		Parser<Expression> valueParser = null;
		Parser<Expression> listParser = null;

		valueParser = Parser<Expression>.Build(builder => builder.Any(
			builder.Token((int)TokenKind.IntegerNumber).As((s, t) =>
			{
				int value;
				var sub = s.Substring(t.index, t.length);
				if (!int.TryParse(sub, out value))
					return Option.None;
				return new ValueExpression(value);
			}),
			builder.Token((int)TokenKind.RealNumber).As((s, t) =>
			{
				float value;
				var sub = s.Substring(t.index, t.length);
				if (!float.TryParse(sub, out value))
					return Option.None;
				return new ValueExpression(value);
			}),
			builder.Token((int)TokenKind.String).As((s, t) =>
			{
				var sub = s.Substring(t.index + 1, t.length - 2).Replace("\\\"", "\"");
				return new ValueExpression(sub);
			}),
			builder.Token((int)TokenKind.Identifier).As((s, t) =>
			{
				var sub = s.Substring(t.index, t.length);
				return new IdentifierExpression(sub);
			}),
			listParser
		)).Expect("Expected a number, string literal, identifier or list");

		listParser = Parser<Expression>.Build(builder => builder.All(
			builder.Token((int)TokenKind.OpenParenthesis).Expect("Expected a '('"),
			valueParser.Maybe().AtLeast(0).As(es => new ListExpression(es)),
			builder.Token((int)TokenKind.CloseParenthesis).Expect("Expected a ')'")
		)).Expect("Expected a list");

		parser = Parser<Expression>.Build(builder =>
			listParser.AtLeast(1).As(es => new ListExpression(es))
		);
	}

	public Result<Expression, string> Parse(string source)
	{
		var tokens = Tokenizer.Tokenize(scanners, source);
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
		{
			var errorMessage = CheckAst(expression.ok);
			if (!string.IsNullOrEmpty(errorMessage))
				return Result.Error(errorMessage);
			return Result.Ok(expression.ok);
		}

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

	public string CheckAst(Expression root)
	{
		var programList = root as ListExpression;
		if (programList == null)
			return "Root expression should be a list";
		if (programList.expressions.Count == 0)
			return "Program cannot be empty";

		foreach (var exp in programList.expressions)
		{
			var list = exp as ListExpression;
			if (list == null)
				return "Program can only contain lists";

			var result = CheckListStartsWithIdentifier(list);
			if (!string.IsNullOrEmpty(result))
				return result;
		}

		return "";
	}

	public string CheckListStartsWithIdentifier(ListExpression list)
	{
		if (list.expressions.Count == 0)
			return "List should have at least one element";

		var elementList = list.expressions[0] as ListExpression;
		if (elementList != null)
			return CheckListStartsWithIdentifier(elementList);
		var elementValue = list.expressions[0] as ValueExpression;
		if (elementValue != null)
			return "First list element cannot be a value";
		var elementIdentifier = list.expressions[0] as IdentifierExpression;
		if (elementIdentifier != null)
			return "";

		return "Invalid list";
	}
}
