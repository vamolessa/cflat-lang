public delegate void ParseFunction(Compiler compiler, Precedence currentPrecedence);

public readonly struct ParseRule
{
	public readonly ParseFunction prefixRule;
	public readonly ParseFunction infixRule;
	public readonly Precedence precedence;

	public ParseRule(ParseFunction prefixRule, ParseFunction infixRule, Precedence precedence)
	{
		this.prefixRule = prefixRule;
		this.infixRule = infixRule;
		this.precedence = precedence;
	}
}
