using Xunit;

public sealed class StructTests
{
	public static string Run(string source, out ValueData value, out ValueType type)
	{
		const int TabSize = 8;
		value = new ValueData();
		type = new ValueType();

		var pepper = new Pepper();
		var compileErrors = pepper.CompileSource(source);
		if (compileErrors.Count > 0)
			return "COMPILE ERROR: " + CompilerHelper.FormatError(source, compileErrors, 1, TabSize);

		var runError = pepper.RunLastFunction();
		if (runError.isSome)
			return "RUNTIME ERROR: " + VirtualMachineHelper.FormatError(source, runError.value, 1, TabSize);

		pepper.virtualMachine.Pop(out value, out type);
		return null;
	}

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
		var error = Run(source, out var v, out var t);
		Assert.Null(error);
		Assert.Equal(new ValueType(TypeKind.Int), t);
		Assert.Equal(expected, v.asInt);
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
		var error = Run(source, out var v, out var t);
		Assert.Null(error);
		Assert.Equal(new ValueType(TypeKind.Int), t);
		Assert.Equal(expected, v.asInt);
	}

	[Theory]
	[InlineData("struct S{x:int} fn a():S{S{x=3}} fn b():int{a().x}", 3)]
	[InlineData("struct S{x:int y:int} fn a():S{S{x=3 y=5}} fn b():int{a().x}", 3)]
	[InlineData("struct S{x:int y:int} fn a():S{S{x=3 y=5}} fn b():int{a().y}", 5)]
	[InlineData("struct S{x:int y:int z:int} fn a():S{S{x=3 y=5 z=7}} fn b():int{a().x}", 3)]
	[InlineData("struct S{x:int y:int z:int} fn a():S{S{x=3 y=5 z=7}} fn b():int{a().y}", 5)]
	[InlineData("struct S{x:int y:int z:int} fn a():S{S{x=3 y=5 z=7}} fn b():int{a().z}", 7)]
	public void FunctionStructReturnTests(string source, int expected)
	{
		var error = Run(source, out var v, out var t);
		Assert.Null(error);
		Assert.Equal(new ValueType(TypeKind.Int), t);
		Assert.Equal(expected, v.asInt);
	}

	[Theory]
	[InlineData("fn b():int{p().xy1.x}", 3)]
	[InlineData("fn b():int{p().xy1.y}", 9)]
	[InlineData("fn b():int{p().xy2.x}", 11)]
	[InlineData("fn b():int{p().xy2.y}", 13)]
	public void FunctionStructInceptionReturnTests(string lastFunctionSource, int expected)
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

		var error = Run(source, out var v, out var t);
		Assert.Null(error);
		Assert.Equal(new ValueType(TypeKind.Int), t);
		Assert.Equal(expected, v.asInt);
	}

	[Theory]
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
	public void Nested1AnonymousStructTest(string source, int[] slices, TypeKind[] fieldKinds)
	{
		var compiler = new CompilerController();
		var chunk = new ByteCodeChunk();

		string error = null;
		var errors = compiler.Compile(source, chunk);
		if (errors.Count > 0)
			error = CompilerHelper.FormatError(source, errors, 1, 8);
		Assert.Null(error);

		var sliceCount = slices.Length / 2;
		Assert.Equal(sliceCount, chunk.structTypes.count);
		Assert.Equal(fieldKinds.Length, chunk.structTypeFields.count);

		for (var i = 0; i < sliceCount; i++)
		{
			var structType = chunk.structTypes.buffer[i];
			var slice = new Slice(slices[i * 2], slices[i * 2 + 1]);

			Assert.Equal(slice.index, structType.fields.index);
			Assert.Equal(slice.length, structType.fields.length);
		}

		for (var i = 0; i < fieldKinds.Length; i++)
		{
			var structTypeField = chunk.structTypeFields.buffer[i];
			Assert.Equal(fieldKinds[i], structTypeField.type.kind);
		}
	}
}