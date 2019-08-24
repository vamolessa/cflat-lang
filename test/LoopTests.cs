using Xunit;

public sealed class LoopTests
{
	[Theory]
	[InlineData("{mut a=10 while a>0{a=a-1} a}", 0)]
	[InlineData("{mut a=10 let b=2 while a>b{a=a-1} a}", 2)]
	[InlineData("{mut a=10 mut b=0 while a>0{a=a-1 b=b+1} b}", 10)]
	public void WhileIntTests(string source, int expected)
	{
		var n = TestHelper.RunExpression<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, n);
	}

	[Theory]
	[InlineData("{mut a=0 for i=0,10{a=a+1} a}", 10)]
	[InlineData("{mut a=0 for i=0,10{a=a+1 i=i+1} a}", 5)]
	public void ForIntTests(string source, int expected)
	{
		var n = TestHelper.RunExpression<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, n);
	}
}