public delegate void ParseFunction(Compiler compiler, int currentPrecedence);

public readonly struct ParseRule
{
	public readonly ParseFunction prefixRule;
	public readonly ParseFunction infixRule;
	public readonly int precedence;

	public ParseRule(ParseFunction prefixRule, ParseFunction infixRule, int precedence)
	{
		this.prefixRule = prefixRule;
		this.infixRule = infixRule;
		this.precedence = precedence;
	}
}
