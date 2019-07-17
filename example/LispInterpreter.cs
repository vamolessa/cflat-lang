using System.Collections.Generic;
using System.Text;

public static class LispInterpreter
{
	public static Result<ValueExpression, string> Eval(Expression expression, Dictionary<string, Expression> environment)
	{
		switch (expression)
		{
		case ValueExpression value:
			return Result.Ok(value);
		case IdentifierExpression identifier:
			if (environment.TryGetValue(identifier.name, out var envValue))
				return Eval(envValue, environment);

			return Result.Error(string.Format(
				"Undefined identifier '{0}'", identifier.name
			));
		case ListExpression list:
			return EvalList(list, environment);
		default:
			return Result.Error(string.Format(
				"Invalid '{0}' expression", expression.GetType().Name
			));
		}
	}

	private static Result<ValueExpression, string> EvalList(ListExpression list, Dictionary<string, Expression> environment)
	{
		if (list.expressions.Count == 0)
			return Result.Error("List expression can not be empty");

		var firstExpression = list.expressions[0];
		if (firstExpression is IdentifierExpression identifier)
		{
			switch (identifier.name)
			{
			case "print":
				var sb = new StringBuilder();
				for (var i = 1; i < list.expressions.Count; i++)
				{
					var result = Eval(list.expressions[i], environment);
					if (!result.IsOk)
						return result;

					sb.Append(result.ok.underlying.ToString());
					sb.Append(" ");
				}
				System.Console.WriteLine(sb);
				return Result.Ok(new ValueExpression(0));
			case "+":
				var aggregate = 0;
				for (var i = 1; i < list.expressions.Count; i++)
				{
					var result = Eval(list.expressions[i], environment);
					if (!result.IsOk)
						return result;

					if (result.ok.underlying is int value)
						aggregate += value;
					else
						return Result.Error(string.Format(
							"'+' function can not accept type '{0}' at index {1}",
							result.ok.underlying.GetType().Name,
							i - 1
						));
				}
				return Result.Ok(new ValueExpression(aggregate));
			default:
				return Result.Error(string.Format(
					"Could not call function '{0}'", identifier.name
				));
			}
		}
		else
		{
			Result<ValueExpression, string> lastResult = Result.Error("Invalid list");
			foreach (var e in list.expressions)
			{
				lastResult = Eval(e, environment);
				if (!lastResult.IsOk)
					return lastResult;
			}
			return lastResult;
		}
	}
}