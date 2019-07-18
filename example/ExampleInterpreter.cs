using System.Collections.Generic;

public static class ExampleInterpreter
{
	public static Result<ValueExpression, string> Eval(Expression expression, Dictionary<string, Expression> environment)
	{
		return Result.Error("ops");
	}
}