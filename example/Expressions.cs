public abstract class Expression
{
}

public sealed class VariableExpression : Expression
{
	public readonly Token token;
	public readonly string name;

	public VariableExpression(Token token, string name)
	{
		this.token = token;
		this.name = name;
	}
}

public sealed class ValueExpression : Expression
{
	public readonly Token token;
	public readonly object underlying;

	public ValueExpression(Token token, object underlying)
	{
		this.token = token;
		this.underlying = underlying;
	}
}

public sealed class UnaryOperationExpression : Expression
{
	public readonly Token token;
	public readonly Expression expression;

	public UnaryOperationExpression(Token token, Expression expression)
	{
		this.token = token;
		this.expression = expression;
	}
}

public sealed class BinaryOperationExpression : Expression
{
	public readonly Token token;
	public readonly Expression left;
	public readonly Expression right;

	public BinaryOperationExpression(Token token, Expression left, Expression right)
	{
		this.token = token;
		this.left = left;
		this.right = right;
	}
}

public sealed class GroupExpression : Expression
{
	public readonly Token leftToken;
	public readonly Token rightToken;
	public readonly Expression expression;

	public GroupExpression(Token leftToken, Token rightToken, Expression expression)
	{
		this.leftToken = leftToken;
		this.rightToken = rightToken;
		this.expression = expression;
	}
}
