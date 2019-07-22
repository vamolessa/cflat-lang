using System.Collections.Generic;

public abstract class Expression
{
}

public sealed class VariableExpression : Expression
{
	public Token token;
	public string name;
}

public sealed class ValueExpression : Expression
{
	public Token token;
	public object value;

	public static Expression New(Token token, object value)
	{
		return new ValueExpression
		{
			token = token,
			value = value
		};
	}
}

public sealed class UnaryOperationExpression : Expression
{
	public Token token;
	public Expression expression;
}

public sealed class BinaryOperationExpression : Expression
{
	public Token token;
	public Expression left;
	public Expression right;

	public static Expression New(Token token, Expression left, Expression right)
	{
		return new BinaryOperationExpression
		{
			token = token,
			left = left,
			right = right
		};
	}
}

public sealed class LogicOperationExpression : Expression
{
	public Token token;
	public Expression left;
	public Expression right;

	public static Expression New(Token token, Expression left, Expression right)
	{
		return new LogicOperationExpression
		{
			token = token,
			left = left,
			right = right
		};
	}
}

public sealed class BlockExpression : Expression
{
	public List<Expression> expressions;
}

public sealed class WhileExpression : Expression
{
	public Expression condition;
	public BlockExpression body;
}

public sealed class IfExpression : Expression
{
	public Expression condition;
	public BlockExpression thenBlock;
	public Option<BlockExpression> elseBlock;
}

public sealed class BreakExpression : Expression
{
}

public sealed class ReturnExpression : Expression
{
	public Expression expression;
}

public sealed class GroupExpression : Expression
{
	public Token leftToken;
	public Token rightToken;
	public Expression expression;
}

public sealed class AssignmentExpression : Expression
{
	public Token identifierToken;
	public Expression expression;
}