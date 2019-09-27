using Xunit;

public sealed class SetStatementTests
{
	[Theory]
	[InlineData("fn f():int{let mut a=0 set a=9 a}")]
	[InlineData("struct S{x:int} fn f():int{let mut a=S{x=0} set a.x=9 a.x}")]
	[InlineData("struct S1{x:int}struct S2{y:S1} fn f():int{let mut a=S2{y=S1{x=0}} set a.y.x=9 a.y.x}")]
	[InlineData("fn getA(a:[mut int]):[mut int]{a} fn f():int{let a=[0,1] set getA(a)[0]=9 a[0]}")]
	[InlineData("struct T{x:int} struct S{y:[mut T]} fn getS(ts:[mut T]):S{S{y=ts}} fn f():int{let ts=[T{x=0},1] set getS(ts).y[0].x=9 ts[0].x}")]
	public void TestSetInt(string source)
	{
		var v = TestHelper.Run<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(9, v.value);
	}

	[Theory]
	[InlineData("fn getInt():int{0} fn f(){set getInt()=9}")]
	public void TestSetError(string source)
	{
		Assert.Throws<CompileErrorException>(() =>
		{
			TestHelper.Run<Unit>(source, out var a);
		});
	}
}