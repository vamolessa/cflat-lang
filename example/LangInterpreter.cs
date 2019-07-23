using System.Collections.Generic;

public static class LangInterpreter
{
	public static Result<object, string> Eval(Expression expression, Dictionary<string, object> environment)
	{
		switch (expression)
		{
		case FunctionExpression fune:
			return Result.Ok<object>("function " + fune.identifier.name);
		case VariableExpression vare:
			return Result.Ok<object>("variable " + vare.identifier.name);
		case ValueExpression vale:
			return Result.Ok(vale.value);
		case UnaryOperationExpression unary:
			return EvalUnary(unary, environment);
		default:
			return Result.Error("eval error");
		}
	}

	private static Result<object, string> EvalUnary(UnaryOperationExpression unary, Dictionary<string, object> environment)
	{
		var value = Eval(unary.expression, environment);
		if (!value.isOk)
			return Result.Error(value.error);

		switch ((TokenKind)unary.opToken.kind)
		{
		case TokenKind.Minus:
			switch (value.ok)
			{
			case int i:
				return Result.Ok<object>(-i);
			case float f:
				return Result.Ok<object>(-f);
			default:
				return Result.Error("can only apply '-' operator to numbers");
			}
		case TokenKind.Bang:
			return Result.Ok<object>(InterpreterHelper.ToBool(value));
		default:
			return Result.Error("invalid operator");
		}
	}
}