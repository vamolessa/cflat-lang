using Xunit;

public sealed class TupleTests
{
	[Theory]
	[InlineData("fn f():int{let{a b}=tuple{true 3} a b}", 3)]
	[InlineData("fn f():int{let t=tuple{true 3} let{a b}=t a b}", 3)]
	public void TupleDeconstructionTests(string source, int expected)
	{
		TestHelper.Run(source, out var a).GetInt(out var v);
		a.AssertSuccessCall();
		Assert.Equal(expected, v);
	}

	[Theory]
	[InlineData("fn b(t:tuple{bool int}):int{let{a b}=t a b}fn f():int{b(tuple{true 3})}", 3)]
	public void TupleParameterTests(string source, int expected)
	{
		TestHelper.Run(source, out var a).GetInt(out var v);
		a.AssertSuccessCall();
		Assert.Equal(expected, v);
	}
}