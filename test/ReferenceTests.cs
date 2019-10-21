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
	[InlineData("fn b(a:int){} fn f(){let v=3 let r=&v b(r)}")]
	[InlineData("fn f(){let v=[4:1] let _=&v[0]}")]
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
	[InlineData("fn f(){let v=3 let _={true,&v}}")]
	[InlineData("fn f(){let v=3 let _=[&v:1]}")]
	[InlineData("fn f(){let v=3 let _=[1:&v]}")]
	public void CreateReferenceErrors(string source)
	{
		Assert.Throws<CompileErrorException>(() =>
		{
			TestHelper.Run<Unit>(source, out var a);
		});
	}

	[Theory]
	[InlineData("fn b(r:&int){} fn f(){}")]
	[InlineData("fn b(r:&mut int){} fn f(){}")]
	public void ReferenceDeclarationTests(string source)
	{
		TestHelper.Run<Unit>(source, out var a);
		a.AssertSuccessCall();
	}

	[Theory]
	[InlineData("struct S{r:&int} fn f(){}")]
	[InlineData("struct S{r:&mut int} fn f(){}")]
	[InlineData("fn b():&int{let a=0 &a} fn f(){}")]
	[InlineData("fn b():&mut int{let a=0 &mut a} fn f(){}")]
	public void ReferenceDeclarationErrors(string source)
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
	[InlineData("fn f():int{let v=3 let r=&v let r2=&r r2}", 3)]
	[InlineData("struct P{x:int,y:int}fn f():int{let v=P{x=11,y=12}let r=&v let r2=&r.x r2}", 11)]
	[InlineData("struct P{x:int,y:int}fn f():int{let v=P{x=11,y=12}let r=&v let r2=&r.y r2}", 12)]
	[InlineData("fn f():int{let mut v=[1:3] set v[1]=47 let r=&v[1] r}", 47)]
	[InlineData("struct P{x:int,y:int}fn f():int{let mut v=[P{x=1,y=1}:3] set v[1]=P{x=45,y=67} let r=&v[1].x r}", 45)]
	[InlineData("struct P{x:int,y:int}fn f():int{let mut v=[P{x=1,y=1}:3] set v[1]=P{x=45,y=67} let r=&v[1].y r}", 67)]
	public void LoadIntReferenceTests(string source, int expected)
	{
		var v = TestHelper.Run<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v.value);
	}

	[Theory]
	[InlineData("fn f():int{let mut v=3 let r=&mut v set r=5 v}", 5)]
	[InlineData("struct P{x:int,y:int}fn f():int{let mut v=P{x=1,y=2}let r=&mut v set r=P{x=5,y=2} v.x}", 5)]
	[InlineData("struct P{x:int,y:int}fn f():int{let mut v=P{x=1,y=2}let r=&mut v set r.x=5 v.x}", 5)]
	[InlineData("struct P{x:int,y:int}fn f():int{let mut v=P{x=1,y=2}let r=&mut v set r.y=7 v.y}", 7)]
	[InlineData("struct P{x:int,y:int}fn f():int{let mut v=P{x=1,y=2}let r=&mut v.x set r=7 v.x}", 7)]
	[InlineData("struct P{x:int,y:int}fn f():int{let mut v=P{x=1,y=2}let r=&mut v.y set r=9 v.y}", 9)]
	[InlineData("struct P{x:int,y:int}struct S{a:bool,b:P,c:float}fn f():int{let mut v=S{a=true,b=P{x=1,y=2},c=0.3}let r=&mut v set r.b.x=5 v.b.x}", 5)]
	[InlineData("struct P{x:int,y:int}struct S{a:bool,b:P,c:float}fn f():int{let mut v=S{a=true,b=P{x=1,y=2},c=0.3}let r=&mut v.b set r.x=5 v.b.x}", 5)]
	[InlineData("struct P{x:int,y:int}struct S{a:bool,b:P,c:float}fn f():int{let mut v=S{a=true,b=P{x=1,y=2},c=0.3}let r=&mut v.b set r.y=8 v.b.y}", 8)]
	[InlineData("struct P{x:int,y:int}struct S{a:bool,b:P,c:float}fn f():int{let mut v=S{a=true,b=P{x=1,y=2},c=0.3}let r=&mut v.b set r=P{x=3,y=4} v.b.x}", 3)]
	[InlineData("struct P{x:int,y:int}struct S{a:bool,b:P,c:float}fn f():int{let mut v=S{a=true,b=P{x=1,y=2},c=0.3}let r=&mut v.b set r=P{x=3,y=4} v.b.y}", 4)]
	[InlineData("fn inc(r:&mut int){set r=r+1} fn f():int{let mut v=3 inc(&mut v) v}", 4)]
	[InlineData("fn f():int{let mut v=[1:3] let r=&mut v[1] set r=47 v[1]}", 47)]
	[InlineData("struct P{x:int,y:int}fn f():int{let mut v=[P{x=1,y=1}:3] let r=&mut v[1] set r.x=52 v[1].x}", 52)]
	[InlineData("struct P{x:int,y:int}fn f():int{let mut v=[P{x=1,y=1}:3] let r=&mut v[1] set r.y=52 v[1].y}", 52)]
	[InlineData("struct P{x:int,y:int}fn f():int{let mut v=[P{x=1,y=1}:3] let r=&mut v[1].x set r=46 v[1].x}", 46)]
	[InlineData("struct P{x:int,y:int}fn f():int{let mut v=[P{x=1,y=1}:3] let r=&mut v[1].y set r=46 v[1].y}", 46)]
	public void SetIntReferenceTests(string source, int expected)
	{
		var v = TestHelper.Run<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v.value);
	}
}