using System.Collections.Generic;
using System.Text;

public static class LispInterpreter
{
	public readonly struct Result
	{
		public readonly ValueExpression value;
		public readonly string error;

		public bool IsSuccess
		{
			get { return string.IsNullOrEmpty(error) && value != null; }
		}

		public Result(ValueExpression value)
		{
			this.value = value;
			this.error = null;
		}

		public Result(string error)
		{
			this.value = null;
			this.error = error;
		}
	}

	public static Result Eval(Expression expression, Dictionary<string, Expression> environment)
	{
		switch (expression)
		{
		case ValueExpression value:
			return new Result(value);
		case IdentifierExpression identifier:
			if (environment.TryGetValue(identifier.name, out var envValue))
				return Eval(envValue, environment);

			return new Result(
				string.Format("Undefined identifier '{0}'", identifier.name)
			);
		case ListExpression list:
			return EvalList(list, environment);
		default:
			return new Result(string.Format(
				"Invalid '{0}' expression", expression.GetType().Name
			));
		}
	}

	private static Result EvalList(ListExpression list, Dictionary<string, Expression> environment)
	{
		if (list.expressions.Count == 0)
			return new Result("List expression can not be empty");

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
					if (!result.IsSuccess)
						return result;

					sb.Append(result.value.underlying.ToString());
					sb.Append(" ");
				}
				System.Console.WriteLine(sb);
				return new Result(new ValueExpression(0));
			case "+":
				var aggregate = 0;
				for (var i = 1; i < list.expressions.Count; i++)
				{
					var result = Eval(list.expressions[i], environment);
					if (!result.IsSuccess)
						return result;

					if (result.value.underlying is int value)
						aggregate += value;
					else
						return new Result(string.Format(
							"'+' function can not accept type '{0}' at index {1}",
							result.value.underlying.GetType().Name,
							i - 1
						));
				}
				return new Result(new ValueExpression(aggregate));
			default:
				return new Result(
					string.Format("Could not call function '{0}'", identifier.name)
				);
			}
		}
		else
		{
			Result lastResult = new Result("Invalid list");
			foreach (var e in list.expressions)
			{
				lastResult = Eval(e, environment);
				if (!lastResult.IsSuccess)
					return lastResult;
			}
			return lastResult;
		}
	}
}