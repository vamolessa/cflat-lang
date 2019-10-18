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
	public void CreateReferenceTests(string source)
	{
		TestHelper.Run<Unit>(source, out var a);
		a.AssertSuccessCall();
	}
}