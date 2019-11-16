using Xunit;
using cflat;

public sealed class ExpressionTests
{
	[Theory]
	[InlineData("{}")]
	[InlineData("{{}}")]
	[InlineData("{let mut a=4 set a=a+1 {}}")]
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
		Assert.Equal(expected, v.value);
	}

	[Theory]
	[InlineData("if true {}")]
	[InlineData("if true {} else {}")]
	[InlineData("if true {} else if false {}")]
	[InlineData("if true {} else if false {} else {}")]
	[InlineData("if true {let a=0 a+1 {}}")]
	[InlineData("if true {{}}")]
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
		Assert.Equal(expected, v.value);
	}

	[Theory]
	[InlineData("true && true", true)]
	[InlineData("true && false", false)]
	[InlineData("false && true", false)]
	[InlineData("false && false", false)]
	[InlineData("true || true", true)]
	[InlineData("true || false", true)]
	[InlineData("false || true", true)]
	[InlineData("false || false", false)]
	[InlineData("{let mut a=false true || {set a=true false} a}", false)]
	[InlineData("{let mut a=false false && {set a=true true} a}", false)]
	public void LogicalTests(string source, bool expected)
	{
		var v = TestHelper.RunExpression<Bool>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v.value);
	}

	[Theory]
	[InlineData("{let mut a=4 set a=a+1 a}", 5)]
	[InlineData("{let mut a=4 set a=5 set a=a a}", 5)]
	[InlineData("{let mut a=4 let mut b=5 set b=7 set a=b a}", 7)]
	public void AssignmentIntTests(string source, int expected)
	{
		var v = TestHelper.RunExpression<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v.value);
	}

	[Theory]
	[InlineData("fn f():int{let mut a=0 a=2 a}")]
	public void TestAssignmentExpressionError(string source)
	{
		Assert.Throws<CompileErrorException>(() =>
		{
			TestHelper.Run<Int>(source, out var a);
		});
	}

	[Fact]
	public void VariableUsageTest()
	{
		TestHelper.Run<Unit>("fn f(){let _a=0}", out var a);
		a.AssertSuccessCall();
	}

	[Fact]
	public void VariableUsageError()
	{
		Assert.Throws<CompileErrorException>(() =>
		{
			TestHelper.Run<Unit>("fn f(){let a=0}", out var a);
		});
	}
}