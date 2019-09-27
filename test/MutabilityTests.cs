using Xunit;

public sealed class MutabilityTests
{
	[Theory]
	[InlineData("fn b(a:[int]){} fn f(){b([0,1])}")]
	[InlineData("fn b(a:[mut int]):[int]{a} fn f(){b([0,1])}")]
	[InlineData("fn b(a:[mut int]):[int]{return a} fn f(){b([0,1])}")]
	[InlineData("struct S{a:[int]} fn f(){S{a=[0,1]}}")]
	[InlineData("struct S{a:[mut int]} fn f(){S{a=[0,1]}}")]
	[InlineData("struct S{a:[int]} fn f(){let s=S{a=[0,1]} if true{s.a}else{[0,1]}}")]
	public void Tests(string source)
	{
		TestHelper.Run<Unit>(source, out var a);
		a.AssertSuccessCall();
	}

	[Theory]
	[InlineData("fn b(a:[int]):[mut int]{a} fn f(){b([0,1])}")]
	[InlineData("fn b(a:[int]):[mut int]{return a} fn f(){b([0,1])}")]
	[InlineData("struct S{a:[int]} fn b(a:[mut int]){} fn f(){let s=S{a=[0,1]} b(s.a)}")]
	[InlineData("struct S{a:[int]} fn f(){let s=S{a=[0,1]} if true{[0,1]}else{s.a}}")]
	public void Errors(string source)
	{
		Assert.Throws<CompileErrorException>(() =>
		{
			TestHelper.Run<Unit>(source, out var a);
		});
	}
}