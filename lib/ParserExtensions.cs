public static class ParserExtensions
{
	public static RepeatUntilParser<A, B> RepeatUntil<A, B>(this OldParser<A> self, OldParser<B> endParser)
	{
		return new RepeatUntilParser<A, B>(self, endParser);
	}

	public static OptionalParser<A> Optional<A>(this OldParser<A> self)
	{
		return new OptionalParser<A>(self);
	}

	public static OldParser<B> Select<A, B>(this OldParser<A> self, System.Func<A, B> selector)
	{
		return new SelectParser<A, B>(self, selector);
	}

	public static OldParser<C> SelectMany<A, B, C>(this OldParser<A> self, System.Func<A, OldParser<B>> parserSelector, System.Func<A, B, C> resultSelector)
	{
		return new SelectManyParser<A, B, C>(self, parserSelector, resultSelector);
	}
}