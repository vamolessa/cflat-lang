using System.Collections.Generic;

public abstract class Expression
{
}

public sealed class IdentifierExpression : Expression
{
	public readonly string name;

	public IdentifierExpression(string name)
	{
		this.name = name;
	}
}

public sealed class ValueExpression : Expression
{
	public readonly object underlying;

	public ValueExpression(object underlying)
	{
		this.underlying = underlying;
	}
}

public sealed class ListExpression : Expression
{
	public readonly List<Expression> expressions;

	public ListExpression(List<Expression> expressions)
	{
		this.expressions = expressions;
	}
}
