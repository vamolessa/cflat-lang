using Xunit;

public sealed class ExpressionTests
{
	[Theory]
	[InlineData("{}")]
	[InlineData("{{}}")]
	[InlineData("{tuple{}}")]
	[InlineData("{mut a=4 a=a+1 {}}")]
	[InlineData("{mut a=4 a=a+1 tuple{}}")]
	public void BlockUnitTests(string source)
	{
		TestHelper.RunExpression<Unit>(source, out var a);
		a.AssertSuccessCall();
	}

	[Theory]
	[InlineData("{0}", 0)]
	[InlineData("{4}", 4)]
	[InlineData("{({4})}", 4)]
	[InlineData("{let a=4 a}", 4)]
	[InlineData("{let a=4 a+5}", 9)]
	[InlineData("{let a=4 {let a=2 a+1} a+5}", 9)]
	public void BlockIntTests(string source, int expected)
	{
		var v = TestHelper.RunExpression<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v);
	}

	[Theory]
	[InlineData("if true {}")]
	[InlineData("if true {} else {}")]
	[InlineData("if true {} else if false {}")]
	[InlineData("if true {} else if false {} else {}")]
	[InlineData("if true {let a=0 a+1 tuple{}}")]
	[InlineData("if true {{}}")]
	[InlineData("if true {tuple{}}")]
	public void IfUnitTests(string source)
	{
		TestHelper.RunExpression<Unit>(source, out var a);
		a.AssertSuccessCall();
	}

	[Theory]
	[InlineData("if true {4} else {5}", 4)]
	[InlineData("if 2>3 {4} else {5}", 5)]
	[InlineData("if 3>3 {4} else if 3<3 {-4} else {5}", 5)]
	[InlineData("if if false{true}else{false} {4} else {5}", 5)]
	[InlineData("if true {4} else {5} + 10", 14)]
	[InlineData("20 + if true {4} else {5}", 24)]
	public void IfIntTests(string source, int expected)
	{
		var v = TestHelper.RunExpression<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v);
	}

	[Theory]
	[InlineData("true and true", true)]
	[InlineData("true and false", false)]
	[InlineData("false and true", false)]
	[InlineData("false and false", false)]
	[InlineData("true or true", true)]
	[InlineData("true or false", true)]
	[InlineData("false or true", true)]
	[InlineData("false or false", false)]
	[InlineData("{mut a=false true or {a=true false} a}", false)]
	[InlineData("{mut a=false false and {a=true true} a}", false)]
	public void LogicalTests(string source, bool expected)
	{
		var v = TestHelper.RunExpression<Bool>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v);
	}

	[Theory]
	[InlineData("{mut a=4 a=a+1 a}", 5)]
	[InlineData("{mut a=4 a=a=5 a}", 5)]
	[InlineData("{mut a=4 a=a=a+1 a}", 5)]
	[InlineData("{mut a=4 mut b=5 b+1 a=b=7 a}", 7)]
	public void AssignmentIntTests(string source, int expected)
	{
		var v = TestHelper.RunExpression<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v);
	}
}