using System.Collections.Generic;

public static class LangInterpreter
{
	public static Result<ValueExpression, string> Eval(Expression expression, Dictionary<string, Expression> environment)
	{
		return Result.Error("ops");
	}
}