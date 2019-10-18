using Xunit;

public sealed class ReferenceTests
{
	[Theory]
	[InlineData("fn f(){let v=true let _=&v}")]
	[InlineData("fn f(){let v=3 let _=&v}")]
	[InlineData("fn f(){let v={3,true} let _=&v}")]
	[InlineData("struct S{a:int,b:bool}fn f(){let v=S{a=3,b=true}let _=&v}")]
	[InlineData("struct S{a:int,b:bool}fn f(){let v=S{a=3,b=true}let _=&v.a}")]
	[InlineData("struct S{a:int,b:bool}fn f(){let v=S{a=3,b=true}let _=&v.b}")]
	[InlineData("struct P{x:float,y:float}struct S{a:int,b:P,c:bool}fn f(){let v=S{a=3,b=P{x=11.0,y=12.0},c=true}let _=&v.b}")]
	[InlineData("struct P{x:float,y:float}struct S{a:int,b:P,c:bool}fn f(){let v=S{a=3,b=P{x=11.0,y=12.0},c=true}let _=&v.b.x}")]
	[InlineData("struct P{x:float,y:float}struct S{a:int,b:P,c:bool}fn f(){let v=S{a=3,b=P{x=11.0,y=12.0},c=true}let _=&v.b.y}")]
	[InlineData("fn f(){let v=true let r=&v let _=&r}")]
	[InlineData("struct S{a:int,b:bool}fn f(){let v=S{a=3,b=true}let r=&v let _=&r.a}")]
	public void CreateReferenceTests(string source)
	{
		TestHelper.Run<Unit>(source, out var a);
		a.AssertSuccessCall();
	}

	[Theory]
	[InlineData("fn f(){let _=&3}")]
	[InlineData("fn f(){let _=&{3,true}}")]
	[InlineData("fn b(){} fn f(){let _=&b}")]
	[InlineData("fn b():int{3} fn f(){let _=&b()}")]
	[InlineData("fn f(){let t={3,true} let{a,b}=&t}")]
	public void CreateReferenceErrors(string source)
	{
		Assert.Throws<CompileErrorException>(() =>
		{
			TestHelper.Run<Unit>(source, out var a);
		});
	}

	[Theory]
	[InlineData("fn f():int{let v=3 let r=&v r}", 3)]
	[InlineData("struct S{a:int,b:bool}fn f():int{let v=S{a=3,b=true}let r=&v r.a}", 3)]
	[InlineData("struct S{a:int,b:bool}fn f():int{let v=S{a=3,b=true}let r=&v.a r}", 3)]
	[InlineData("struct P{x:int,y:int}struct S{a:int,b:P,c:bool}fn f():int{let v=S{a=3,b=P{x=11,y=12},c=true}let r=&v.b r.x}", 11)]
	[InlineData("struct P{x:int,y:int}struct S{a:int,b:P,c:bool}fn f():int{let v=S{a=3,b=P{x=11,y=12},c=true}let r=&v.b r.y}", 12)]
	[InlineData("struct P{x:int,y:int}struct S{a:int,b:P,c:bool}fn f():int{let v=S{a=3,b=P{x=11,y=12},c=true}let r=&v.b.x r}", 11)]
	[InlineData("struct P{x:int,y:int}struct S{a:int,b:P,c:bool}fn f():int{let v=S{a=3,b=P{x=11,y=12},c=true}let r=&v.b.y r}", 12)]
	[InlineData("struct S{a:int,b:bool}fn f():int{let v=S{a=3,b=true}let r=&v let r2=&r.a r2}", 3)]
	[InlineData("fn f():int{let v={3,true} let r=&v let{a,_}=r a}", 3)]
	public void LoadIntReferenceTests(string source, int expected)
	{
		var v = TestHelper.Run<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v.value);
	}
}