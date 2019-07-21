public static class ParserExtensions
{
	public static IgnoreParser<T> Ignore<T>(this Parser<T> self)
	{
		return new IgnoreParser<T>(self);
	}

	public static RepeatUntilParser<A, B> RepeatUntil<A, B>(this Parser<A> self, Parser<B> endParser)
	{
		return new RepeatUntilParser<A, B>(self, endParser);
	}

	public static OptionalParser<A> Optional<A>(this Parser<A> self)
	{
		return new OptionalParser<A>(self);
	}

	public static Parser<B> Select<A, B>(this Parser<A> self, System.Func<A, B> selector)
	{
		return new SelectParser<A, B>(self, selector);
	}

	public static Parser<C> SelectMany<A, B, C>(this Parser<A> self, System.Func<A, Parser<B>> parserSelector, System.Func<A, B, C> resultSelector)
	{
		return new SelectManyParser<A, B, C>(self, parserSelector, resultSelector);
	}
}