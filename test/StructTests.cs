using Xunit;

public sealed class StructTests
{
	[Theory]
	[InlineData("struct S{a:int} fn f():int{let s=S{a=3}s.a}", 3)]
	[InlineData("struct S{a:int b:int} fn f():int{let s=S{a=3 b=7}s.a}", 3)]
	[InlineData("struct S{a:int b:int} fn f():int{let s=S{a=3 b=7}s.b}", 7)]
	[InlineData("struct S{a:int b:int c:int} fn f():int{let s=S{a=3 b=7 c=9}s.a}", 3)]
	[InlineData("struct S{a:int b:int c:int} fn f():int{let s=S{a=3 b=7 c=9}s.b}", 7)]
	[InlineData("struct S{a:int b:int c:int} fn f():int{let s=S{a=3 b=7 c=9}s.c}", 9)]
	[InlineData("struct S{a:int b:int} struct T{s:S c:int} fn f():int{let t=T{s=S{a=3 b=7} c=9}t.s.a}", 3)]
	[InlineData("struct S{a:int b:int} struct T{s:S c:int} fn f():int{let t=T{s=S{a=3 b=7} c=9}t.s.b}", 7)]
	[InlineData("struct S{a:int b:int} struct T{s:S c:int} fn f():int{let t=T{s=S{a=3 b=7} c=9}t.c}", 9)]
	public void StructGetFieldTests(string source, int expected)
	{
		TestHelper.Run(source, out var a).GetInt(out var v);
		a.AssertSuccessCall();
		Assert.Equal(expected, v);
	}

	[Theory]
	[InlineData("struct S{a:int} fn f():int{mut s=S{a=0}s.a=3 s.a}", 3)]
	[InlineData("struct S{a:int b:int} fn f():int{mut s=S{a=0 b=0}s.a=3 s.a}", 3)]
	[InlineData("struct S{a:int b:int} fn f():int{mut s=S{a=0 b=0}s.b=7 s.b}", 7)]
	[InlineData("struct S{a:int b:int c:int} fn f():int{mut s=S{a=0 b=0 c=0}s.a=3 s.a}", 3)]
	[InlineData("struct S{a:int b:int c:int} fn f():int{mut s=S{a=0 b=0 c=0}s.b=7 s.b}", 7)]
	[InlineData("struct S{a:int b:int c:int} fn f():int{mut s=S{a=0 b=0 c=0}s.c=9 s.c}", 9)]
	[InlineData("struct S{a:int b:int} struct T{s:S c:int} fn f():int{mut t=T{s=S{a=0 b=0} c=0}t.s.a=3 t.s.a}", 3)]
	[InlineData("struct S{a:int b:int} struct T{s:S c:int} fn f():int{mut t=T{s=S{a=0 b=0} c=0}t.s.b=7 t.s.b}", 7)]
	[InlineData("struct S{a:int b:int} struct T{s:S c:int} fn f():int{mut t=T{s=S{a=0 b=0} c=0}t.c=9 t.c}", 9)]
	public void StructSetFieldTests(string source, int expected)
	{
		TestHelper.Run(source, out var a).GetInt(out var v);
		a.AssertSuccessCall();
		Assert.Equal(expected, v);
	}

	[Theory]
	[InlineData("struct S{x:int} fn a():S{S{x=3}} fn f():int{a().x}", 3)]
	[InlineData("struct S{x:int y:int} fn a():S{S{x=3 y=5}} fn f():int{a().x}", 3)]
	[InlineData("struct S{x:int y:int} fn a():S{S{x=3 y=5}} fn f():int{a().y}", 5)]
	[InlineData("struct S{x:int y:int z:int} fn a():S{S{x=3 y=5 z=7}} fn f():int{a().x}", 3)]
	[InlineData("struct S{x:int y:int z:int} fn a():S{S{x=3 y=5 z=7}} fn f():int{a().y}", 5)]
	[InlineData("struct S{x:int y:int z:int} fn a():S{S{x=3 y=5 z=7}} fn f():int{a().z}", 7)]
	public void FunctionStructReturnTests(string source, int expected)
	{
		TestHelper.Run(source, out var a).GetInt(out var v);
		a.AssertSuccessCall();
		Assert.Equal(expected, v);
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
				x:int
				y:int
			}

			struct Point {
				xy1:XY
				xy2:XY
			}

			fn p():Point {
				Point{xy1=XY{x=3 y=9} xy2=XY{x=11 y=13}}
			}
		", lastFunctionSource);

		TestHelper.Run(source, out var a).GetInt(out var v);
		a.AssertSuccessCall();
		Assert.Equal(expected, v);
	}

	[Theory]
	[InlineData(
		"fn f(){struct{a=struct{b=true c=true} d=3}}",
		new[] { 0, 2, 2, 2 },
		new[] {
			TypeKind.Bool,
			TypeKind.Bool,
			TypeKind.Struct,
			TypeKind.Int
		}
	)]
	[InlineData(
		"fn f(){struct{a=0 b=struct{c=true d=true} e=3}}",
		new[] { 0, 2, 2, 3 },
		new[] {
			TypeKind.Bool,
			TypeKind.Bool,
			TypeKind.Int,
			TypeKind.Struct,
			TypeKind.Int
		}
	)]
	[InlineData(
		"fn f(){struct{a=0 b=struct{c=struct{d=1.0 e=2.0} f=true} g=3}}",
		new[] { 0, 2, 2, 2, 4, 3 },
		new[] {
			TypeKind.Float,
			TypeKind.Float,
			TypeKind.Struct,
			TypeKind.Bool,
			TypeKind.Int,
			TypeKind.Struct,
			TypeKind.Int
		}
	)]
	public void NestedAnonymousStructTest(string source, int[] expectedSlices, TypeKind[] expectedFieldKinds)
	{
		var compiler = new CompilerController();
		var chunk = new ByteCodeChunk();

		string error = null;
		var errors = compiler.Compile(source, chunk);
		if (errors.count > 0)
			error = CompilerHelper.FormatError(source, errors, 1, 8);
		Assert.Null(error);

		var sliceCount = expectedSlices.Length / 2;
		Assert.Equal(sliceCount, chunk.structTypes.count);
		Assert.Equal(expectedFieldKinds.Length, chunk.structTypeFields.count);

		var slices = new int[chunk.structTypes.count * 2];
		for (var i = 0; i < sliceCount; i++)
		{
			var structType = chunk.structTypes.buffer[i];
			slices[i * 2] = structType.fields.index;
			slices[i * 2 + 1] = structType.fields.length;
		}
		Assert.Equal(expectedSlices, slices);

		var fieldKinds = new TypeKind[chunk.structTypeFields.count];
		for (var i = 0; i < fieldKinds.Length; i++)
		{
			var structTypeField = chunk.structTypeFields.buffer[i];
			fieldKinds[i] = structTypeField.type.kind;
		}
		Assert.Equal(expectedFieldKinds, fieldKinds);
	}
}