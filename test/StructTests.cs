using Xunit;

public sealed class StructTests
{
	[Theory]
	[InlineData("struct S{a:int} fn f():int{let s=S{a=3}s.a}", 3)]
	[InlineData("struct S{a:int,b:int} fn f():int{let s=S{a=3,b=7}s.a}", 3)]
	[InlineData("struct S{a:int,b:int} fn f():int{let s=S{a=3,b=7}s.b}", 7)]
	[InlineData("struct S{a:int,b:int,c:int} fn f():int{let s=S{a=3,b=7,c=9}s.a}", 3)]
	[InlineData("struct S{a:int,b:int,c:int} fn f():int{let s=S{a=3,b=7,c=9}s.b}", 7)]
	[InlineData("struct S{a:int,b:int,c:int} fn f():int{let s=S{a=3,b=7,c=9}s.c}", 9)]
	[InlineData("struct S{a:int,b:int} struct T{s:S,c:int} fn f():int{let t=T{s=S{a=3,b=7},c=9}t.s.a}", 3)]
	[InlineData("struct S{a:int,b:int} struct T{s:S,c:int} fn f():int{let t=T{s=S{a=3,b=7},c=9}t.s.b}", 7)]
	[InlineData("struct S{a:int,b:int} struct T{s:S,c:int} fn f():int{let t=T{s=S{a=3,b=7},c=9}t.c}", 9)]
	public void StructGetFieldTests(string source, int expected)
	{
		var v = TestHelper.Run<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v.value);
	}

	[Theory]
	[InlineData("struct S{a:int} fn f():int{let mut s=S{a=0}s.a=3 s.a}", 3)]
	[InlineData("struct S{a:int,b:int} fn f():int{let mut s=S{a=0,b=0}s.a=3 s.a}", 3)]
	[InlineData("struct S{a:int,b:int} fn f():int{let mut s=S{a=0,b=0}s.b=7 s.b}", 7)]
	[InlineData("struct S{a:int,b:int,c:int} fn f():int{let mut s=S{a=0,b=0,c=0}s.a=3 s.a}", 3)]
	[InlineData("struct S{a:int,b:int,c:int} fn f():int{let mut s=S{a=0,b=0,c=0}s.b=7 s.b}", 7)]
	[InlineData("struct S{a:int,b:int,c:int} fn f():int{let mut s=S{a=0,b=0,c=0}s.c=9 s.c}", 9)]
	[InlineData("struct S{a:int,b:int} struct T{s:S,c:int} fn f():int{let mut t=T{s=S{a=0,b=0},c=0}t.s.a=3 t.s.a}", 3)]
	[InlineData("struct S{a:int,b:int} struct T{s:S,c:int} fn f():int{let mut t=T{s=S{a=0,b=0},c=0}t.s.b=7 t.s.b}", 7)]
	[InlineData("struct S{a:int,b:int} struct T{s:S,c:int} fn f():int{let mut t=T{s=S{a=0,b=0},c=0}t.c=9 t.c}", 9)]
	public void StructSetFieldTests(string source, int expected)
	{
		var v = TestHelper.Run<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v.value);
	}

	[Theory]
	[InlineData("struct S{x:int} fn a():S{S{x=3}} fn f():int{a().x}", 3)]
	[InlineData("struct S{x:int,y:int} fn a():S{S{x=3,y=5}} fn f():int{a().x}", 3)]
	[InlineData("struct S{x:int,y:int} fn a():S{S{x=3,y=5}} fn f():int{a().y}", 5)]
	[InlineData("struct S{x:int,y:int,z:int} fn a():S{S{x=3,y=5,z=7}} fn f():int{a().x}", 3)]
	[InlineData("struct S{x:int,y:int,z:int} fn a():S{S{x=3,y=5,z=7}} fn f():int{a().y}", 5)]
	[InlineData("struct S{x:int,y:int,z:int} fn a():S{S{x=3,y=5,z=7}} fn f():int{a().z}", 7)]
	public void FunctionStructReturnTests(string source, int expected)
	{
		var v = TestHelper.Run<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v.value);
	}

	[Theory]
	[InlineData("fn f():int{p().xy1.x}", 3)]
	[InlineData("fn f():int{p().xy1.y}", 9)]
	[InlineData("fn f():int{p().xy2.x}", 11)]
	[InlineData("fn f():int{p().xy2.y}", 13)]
	public void FunctionNestedStructReturnTests(string lastFunctionSource, int expected)
	{
		var source = string.Concat(@"
			struct XY {
				x:int,
				y:int
			}
			struct Point {
				xy1:XY,
				xy2:XY
			}
			fn p():Point {
				Point{xy1=XY{x=3,y=9},xy2=XY{x=11,y=13}}
			}
		", lastFunctionSource);

		var v = TestHelper.Run<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v.value);
	}
}