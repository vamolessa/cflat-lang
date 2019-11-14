internal sealed class ParseRules
{
	public delegate ValueType PrefixFunction(Compiler controller);
	public delegate ValueType InfixFunction(Compiler controller, Compiler.ExpressionResult expression);

	private const int RuleCount = (int)TokenKind.COUNT;
	private readonly Precedence[] precedences = new Precedence[RuleCount];
	private readonly PrefixFunction[] prefixRules = new PrefixFunction[RuleCount];
	private readonly InfixFunction[] infixRules = new InfixFunction[RuleCount];

	public ParseRules()
	{
		void Set(TokenKind kind, PrefixFunction prefix, InfixFunction infix, Precedence precedence)
		{
			var index = (int)kind;
			precedences[index] = precedence;
			prefixRules[index] = prefix;
			infixRules[index] = infix;
		}

		Set(TokenKind.Dot, null, Compiler.Dot, Precedence.Call);
		Set(TokenKind.OpenParenthesis, Compiler.Grouping, Compiler.Call, Precedence.Call);
		Set(TokenKind.OpenCurlyBrackets, Compiler.BlockOrTupleExpression, null, Precedence.None);
		Set(TokenKind.OpenSquareBrackets, Compiler.ArrayExpression, Compiler.Index, Precedence.Call);
		Set(TokenKind.Minus, Compiler.Unary, Compiler.Binary, Precedence.Term);
		Set(TokenKind.Plus, null, Compiler.Binary, Precedence.Term);
		Set(TokenKind.Slash, null, Compiler.Binary, Precedence.Factor);
		Set(TokenKind.Asterisk, null, Compiler.Binary, Precedence.Factor);
		Set(TokenKind.Bang, Compiler.Unary, null, Precedence.None);
		Set(TokenKind.EqualEqual, null, Compiler.Binary, Precedence.Equality);
		Set(TokenKind.BangEqual, null, Compiler.Binary, Precedence.Equality);
		Set(TokenKind.Length, Compiler.LengthExpression, null, Precedence.None);
		Set(TokenKind.Greater, null, Compiler.Binary, Precedence.Comparison);
		Set(TokenKind.GreaterEqual, null, Compiler.Binary, Precedence.Comparison);
		Set(TokenKind.Less, null, Compiler.Binary, Precedence.Comparison);
		Set(TokenKind.LessEqual, null, Compiler.Binary, Precedence.Comparison);
		Set(TokenKind.Identifier, Compiler.Identifier, null, Precedence.None);
		Set(TokenKind.StringLiteral, Compiler.Literal, null, Precedence.None);
		Set(TokenKind.IntLiteral, Compiler.Literal, null, Precedence.None);
		Set(TokenKind.And, null, Compiler.And, Precedence.And);
		Set(TokenKind.False, Compiler.Literal, null, Precedence.None);
		Set(TokenKind.If, Compiler.If, null, Precedence.None);
		Set(TokenKind.Or, null, Compiler.Or, Precedence.Or);
		Set(TokenKind.FloatLiteral, Compiler.Literal, null, Precedence.None);
		Set(TokenKind.True, Compiler.Literal, null, Precedence.None);
		Set(TokenKind.Function, Compiler.FunctionExpression, null, Precedence.None);
		Set(TokenKind.Ampersand, Compiler.Reference, null, Precedence.None);
		Set(TokenKind.Int, Compiler.Unary, null, Precedence.None);
		Set(TokenKind.Float, Compiler.Unary, null, Precedence.None);
	}

	public Precedence GetPrecedence(TokenKind kind)
	{
		return precedences[(int)kind];
	}

	public PrefixFunction GetPrefixRule(TokenKind kind)
	{
		return prefixRules[(int)kind];
	}

	public InfixFunction GetInfixRule(TokenKind kind)
	{
		return infixRules[(int)kind];
	}
}
