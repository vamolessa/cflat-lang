using System.Collections.Generic;

public readonly struct Identifier
{
	public readonly Token token;
	public readonly string name;

	public Identifier(Token token, string name)
	{
		this.token = token;
		this.name = name;
	}
}

public abstract class Expression
{
}

public sealed class FunctionExpression : Expression
{
	public Identifier identifier;
	public List<Identifier> parameters;
	public Expression body;
}

public sealed class VariableExpression : Expression
{
	public readonly Identifier identifier;

	public VariableExpression(Identifier identifier)
	{
		this.identifier = identifier;
	}
}

public sealed class ValueExpression : Expression
{
	public readonly Token token;
	public readonly object value;

	public ValueExpression(Token token, object value)
	{
		this.token = token;
		this.value = value;
	}
}

public sealed class UnaryOperationExpression : Expression
{
	public readonly Token opToken;
	public readonly Expression expression;

	public UnaryOperationExpression(Token opToken, Expression expression)
	{
		this.opToken = opToken;
		this.expression = expression;
	}
}

public sealed class BinaryOperationExpression : Expression
{
	public readonly Token opToken;
	public readonly Expression left;
	public readonly Expression right;

	public BinaryOperationExpression(Token opToken, Expression left, Expression right)
	{
		this.opToken = opToken;
		this.left = left;
		this.right = right;
	}
}

public sealed class LogicOperationExpression : Expression
{
	public readonly Token opToken;
	public readonly Expression left;
	public readonly Expression right;

	public LogicOperationExpression(Token opToken, Expression left, Expression right)
	{
		this.opToken = opToken;
		this.left = left;
		this.right = right;
	}
}

public sealed class BlockExpression : Expression
{
	public List<Expression> expressions;
}

public sealed class WhileExpression : Expression
{
	public Expression condition;
	public Expression body;
}

public sealed class IfExpression : Expression
{
	public Expression condition;
	public Expression thenBlock;
	public Option<Expression> elseBlock;
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
	public Expression expression;
}

public sealed class AssignmentExpression : Expression
{
	public readonly VariableExpression target;
	public readonly Expression valueExpression;

	public AssignmentExpression(VariableExpression target, Expression valueExpression)
	{
		this.target = target;
		this.valueExpression = valueExpression;
	}
}

public sealed class CallExpression : Expression
{
	public Expression callee;
	public Token closeParenthesis;
	public List<Expression> arguments;
}