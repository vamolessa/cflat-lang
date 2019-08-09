public sealed class ParseRules
{
	public delegate void RuleFunction(CompilerController controller, Precedence precedence);

	private const int RuleCount = (int)TokenKind.COUNT;
	private readonly Precedence[] precedences = new Precedence[RuleCount];
	private readonly RuleFunction[] prefixRules = new RuleFunction[RuleCount];
	private readonly RuleFunction[] infixRules = new RuleFunction[RuleCount];

	public ParseRules()
	{
		void Set(TokenKind kind, RuleFunction prefix, RuleFunction infix, Precedence precedence)
		{
			var index = (int)kind;
			precedences[index] = precedence;
			prefixRules[index] = prefix;
			infixRules[index] = infix;
		}

		Set(TokenKind.OpenParenthesis, CompilerController.Grouping, CompilerController.Call, Precedence.Call);
		Set(TokenKind.OpenCurlyBrackets, CompilerController.Block, null, Precedence.None);
		Set(TokenKind.Minus, CompilerController.Unary, CompilerController.Binary, Precedence.Term);
		Set(TokenKind.Plus, null, CompilerController.Binary, Precedence.Term);
		Set(TokenKind.Slash, null, CompilerController.Binary, Precedence.Factor);
		Set(TokenKind.Asterisk, null, CompilerController.Binary, Precedence.Factor);
		Set(TokenKind.Bang, CompilerController.Unary, null, Precedence.None);
		Set(TokenKind.BangEqual, null, CompilerController.Binary, Precedence.Equality);
		Set(TokenKind.EqualEqual, null, CompilerController.Binary, Precedence.Equality);
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
		Set(TokenKind.Int, CompilerController.Unary, null, Precedence.None);
		Set(TokenKind.Float, CompilerController.Unary, null, Precedence.None);
	}

	public Precedence GetPrecedence(TokenKind kind)
	{
		return precedences[(int)kind];
	}

	public RuleFunction GetPrefixRule(TokenKind kind)
	{
		return prefixRules[(int)kind];
	}

	public RuleFunction GetInfixRule(TokenKind kind)
	{
		return infixRules[(int)kind];
	}
}