internal sealed class ParseRules
{
	public delegate ValueType PrefixFunction(CompilerController controller);
	public delegate ValueType InfixFunction(CompilerController controller, CompilerController.ExpressionResult expression);

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

		Set(TokenKind.Dot, null, CompilerController.Dot, Precedence.Call);
		Set(TokenKind.OpenParenthesis, CompilerController.Grouping, CompilerController.Call, Precedence.Call);
		Set(TokenKind.OpenCurlyBrackets, CompilerController.BlockOrTupleExpression, null, Precedence.None);
		Set(TokenKind.OpenSquareBrackets, CompilerController.ArrayExpression, CompilerController.Index, Precedence.Call);
		Set(TokenKind.Minus, CompilerController.Unary, CompilerController.Binary, Precedence.Term);
		Set(TokenKind.Plus, null, CompilerController.Binary, Precedence.Term);
		Set(TokenKind.Slash, null, CompilerController.Binary, Precedence.Factor);
		Set(TokenKind.Asterisk, null, CompilerController.Binary, Precedence.Factor);
		Set(TokenKind.Bang, CompilerController.Unary, null, Precedence.None);
		Set(TokenKind.EqualEqual, null, CompilerController.Binary, Precedence.Equality);
		Set(TokenKind.BangEqual, null, CompilerController.Binary, Precedence.Equality);
		Set(TokenKind.Length, CompilerController.LengthExpression, null, Precedence.None);
		Set(TokenKind.Greater, null, CompilerController.Binary, Precedence.Comparison);
		Set(TokenKind.GreaterEqual, null, CompilerController.Binary, Precedence.Comparison);
		Set(TokenKind.Less, null, CompilerController.Binary, Precedence.Comparison);
		Set(TokenKind.LessEqual, null, CompilerController.Binary, Precedence.Comparison);
		Set(TokenKind.Identifier, CompilerController.Identifier, null, Precedence.None);
		Set(TokenKind.StringLiteral, CompilerController.Literal, null, Precedence.None);
		Set(TokenKind.IntLiteral, CompilerController.Literal, null, Precedence.None);
		Set(TokenKind.And, null, CompilerController.And, Precedence.And);
		Set(TokenKind.False, CompilerController.Literal, null, Precedence.None);
		Set(TokenKind.If, CompilerController.If, null, Precedence.None);
		Set(TokenKind.Or, null, CompilerController.Or, Precedence.Or);
		Set(TokenKind.FloatLiteral, CompilerController.Literal, null, Precedence.None);
		Set(TokenKind.True, CompilerController.Literal, null, Precedence.None);
		Set(TokenKind.Function, CompilerController.FunctionExpression, null, Precedence.None);
		Set(TokenKind.Ampersand, CompilerController.Reference, null, Precedence.None);
		Set(TokenKind.Int, CompilerController.Unary, null, Precedence.None);
		Set(TokenKind.Float, CompilerController.Unary, null, Precedence.None);
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
