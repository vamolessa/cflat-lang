public delegate void ParseFunction(Compiler compiler);

public readonly struct ParseRule
{
	public readonly ParseFunction prefix;
	public readonly ParseFunction infix;
	public readonly int precedence;

	public ParseRule(ParseFunction prefix, ParseFunction infix, int precedence)
	{
		this.prefix = prefix;
		this.infix = infix;
		this.precedence = precedence;
	}
}
