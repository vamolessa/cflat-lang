public sealed class LispParser
{
	private enum TokenKind
	{
		OpenParenthesis,
		CloseParenthesis,
		IntegerNumber,
		RealNumber,
		String,
		Identifier,
	}

	public readonly Scanner[] scanners;
	public readonly Parser<Expression> parser;

	public LispParser()
	{
		scanners = new Scanner[] {
			new WhiteSpaceScanner().Ignore(),
			new CommentScanner("//").Ignore(),
			new CharScanner('(').WithToken((int)TokenKind.OpenParenthesis),
			new CharScanner(')').WithToken((int)TokenKind.CloseParenthesis),
			new IntegerNumberScanner().WithToken((int)TokenKind.IntegerNumber),
			new RealNumberScanner().WithToken((int)TokenKind.RealNumber),
			new EnclosedScanner('"').WithToken((int)TokenKind.String),
			new IdentifierScanner("+-*_.:@#$%&").WithToken((int)TokenKind.Identifier)
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
			builder.Token((int)TokenKind.OpenParenthesis).Expect("Expected a ("),
			valueParser.SupressError().RepeatAtLeast(0).As(es => new ListExpression(es)),
			builder.Token((int)TokenKind.CloseParenthesis).Expect("Expected a )")
		)).Expect("Expected a list");

		parser = Parser<Expression>.Build(builder =>
			listParser.RepeatAtLeast(1).As(es => new ListExpression(es))
		);
	}

	public Result<Expression> Parse(string source)
	{
		var tokens = Tokenizer.Tokenize(scanners, source);
		if (!tokens.IsOk)
		{
			var position = ParserHelper.GetLineAndColumn(source, tokens.errorIndex);
			return Result.Error(
				tokens.errorIndex,
				string.Format(
					"Unexpected char '{0}' at {1}",
					source[tokens.errorIndex],
					position
				)
			);
		}

		var expression = parser.Parse(source, tokens.ok);
		if (expression.IsOk)
		{
			var errorMessage = CheckAst(expression.ok);
			if (!string.IsNullOrEmpty(errorMessage))
				return Result.Error(0, errorMessage);
		}

		if (expression.errorIndex >= 0 && expression.errorIndex < tokens.ok.Count)
		{
			var errorToken = tokens.ok[expression.errorIndex];
			var position = ParserHelper.GetLineAndColumn(source, errorToken.index);

			return Result.Error(
				tokens.errorIndex,
				string.Format(
					"Unexpected token '{0}' at {1}\n{2}",
					source.Substring(errorToken.index, errorToken.length),
					position,
					expression.errorMessage
				)
			);
		}
		else
		{
			return expression;
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
