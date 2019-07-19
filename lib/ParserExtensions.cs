public static class ParserExtensions
{
	public static Parser<A> Debug<A>(this Parser<A> self, System.Action<DebugParser<A>.DebugInfo> checkpoint)
	{
		return new DebugParser<A>(self, checkpoint);
	}

	public static RepeatParser<A> Repeat<A>(this Parser<A> self)
	{
		return new RepeatParser<A>(self);
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