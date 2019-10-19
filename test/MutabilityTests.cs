using Xunit;

public sealed class MutabilityTests
{
	[Theory]
	[InlineData("fn b(a:[int]){} fn f(){b([0:1])}")]
	[InlineData("fn b(mut a:[int]):[int]{a} fn f(){b([0:1])}")]
	[InlineData("fn b(mut a:[int]):[int]{return a} fn f(){b([0:1])}")]
	[InlineData("struct S{a:[int]} fn f(){S{a=[0:1]}}")]
	[InlineData("struct S{a:[int]} fn f(){let s=S{a=[0:1]} if true{s.a}else{[0:1]}}")]
	public void Tests(string source)
	{
		TestHelper.Run<Unit>(source, out var a);
		a.AssertSuccessCall();
	}

	[Theory]
	[InlineData("fn f(){let a=0 set a=3}")]
	[InlineData("fn b(a:[int]){set a[0] = 0} fn f(){b()}")]
	public void Errors(string source)
	{
		Assert.Throws<CompileErrorException>(() =>
		{
			TestHelper.Run<Unit>(source, out var a);
		});
	}

	[Theory]
	[InlineData("fn f(){let mut a=0 let _=&a}")]
	[InlineData("fn f(){let mut a=0 let _=&mut a}")]
	[InlineData("fn f(){let mut a=0 let r=&mut a let _=&r}")]
	[InlineData("fn f(){let mut a=0 let r=&mut a let _=&mut r}")]
	[InlineData("fn b(r:&int){} fn f(){let mut v=3 b(&mut v)}")]
	public void ReferenceMutabilityTests(string source)
	{
		TestHelper.Run<Unit>(source, out var a);
		a.AssertSuccessCall();
	}

	[Theory]
	[InlineData("fn f(){let a=0 let _=&mut a}")]
	[InlineData("fn f(){let a=0 let r=&a let _=&mut r}")]
	[InlineData("fn f(){let mut a=0 let r=&a let _=&mut r}")]
	[InlineData("fn f(){let a=0 let r=&a set r=2}")]
	[InlineData("fn f(){let mut a=0 let r=&a set r=2}")]
	[InlineData("fn b(r:&mut int){} fn f(){let mut v=3 b(&v)}")]
	public void ReferenceMutabilityErrors(string source)
	{
		Assert.Throws<CompileErrorException>(() =>
		{
			TestHelper.Run<Unit>(source, out var a);
		});
	}
}