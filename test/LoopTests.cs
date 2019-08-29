using Xunit;

public sealed class LoopTests
{
	[Theory]
	[InlineData("{mut a=10 while a>0{a=a-1} a}", 0)]
	[InlineData("{mut a=10 let b=2 while a>b{a=a-1} a}", 2)]
	[InlineData("{mut a=10 mut b=0 while a>0{a=a-1 b=b+1} b}", 10)]
	[InlineData("{mut a=10 while a>0{a=a-1 break} a}", 9)]
	[InlineData("{mut a=10 while:loop a>0{a=a-1 break:loop} a}", 9)]
	[InlineData("{mut a=10 while:outer a>0{while true{a=a-1 break:outer}} a}", 9)]
	public void WhileIntTests(string source, int expected)
	{
		var n = TestHelper.RunExpression<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, n);
	}

	[Theory]
	[InlineData("{mut a=0 for i=0,10{a=a+1} a}", 10)]
	[InlineData("{mut a=0 for i=0,10{a=a+1 i=i+1} a}", 5)]
	[InlineData("{mut a=0 for i=0,10{a=a+1 break} a}", 1)]
	[InlineData("{mut a=0 for:loop i=0,10{a=a+1 break:loop} a}", 1)]
	public void ForIntTests(string source, int expected)
	{
		var n = TestHelper.RunExpression<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, n);
	}
}