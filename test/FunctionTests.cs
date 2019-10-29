using Xunit;

public sealed class FunctionTests
{
	[Theory]
	[InlineData(
		"fn f(a:fn(bool,bool),b:int){}",
		new[] { 0, 2, 2, 2 },
		new[] {
			TypeKind.Bool,
			TypeKind.Bool,
			TypeKind.Function,
			TypeKind.Int
		}
	)]
	[InlineData(
		"fn f(a:int,b:fn(bool,bool),c:int){}",
		new[] { 0, 2, 2, 3 },
		new[] {
			TypeKind.Bool,
			TypeKind.Bool,
			TypeKind.Int,
			TypeKind.Function,
			TypeKind.Int
		}
	)]
	[InlineData(
		"fn f(a:int,b:fn(fn(float,float),bool),c:int){}",
		new[] { 0, 2, 2, 2, 4, 3 },
		new[] {
			TypeKind.Float,
			TypeKind.Float,
			TypeKind.Function,
			TypeKind.Bool,
			TypeKind.Int,
			TypeKind.Function,
			TypeKind.Int
		}
	)]
	public void NestedFunctionExpressionTest(string source, int[] expectedSlices, TypeKind[] expectedParamsKinds)
	{
		var compiler = new CompilerController();
		var chunk = new ByteCodeChunk();

		string error = null;
		var errors = compiler.Compile(chunk, TestHelper.CompilerMode, source);
		if (errors.count > 0)
			error = FormattingHelper.FormatCompileError(source, errors, TestHelper.TabSize);
		Assert.Null(error);

		var sliceCount = expectedSlices.Length / 2;
		Assert.Equal(sliceCount, chunk.functionTypes.count);
		Assert.Equal(expectedParamsKinds.Length, chunk.functionParamTypes.count);

		var slices = new int[chunk.functionTypes.count * 2];
		for (var i = 0; i < sliceCount; i++)
		{
			var functionType = chunk.functionTypes.buffer[i];
			slices[i * 2] = functionType.parameters.index;
			slices[i * 2 + 1] = functionType.parameters.length;
		}
		Assert.Equal(expectedSlices, slices);

		var fieldKinds = new TypeKind[chunk.functionParamTypes.count];
		for (var i = 0; i < fieldKinds.Length; i++)
		{
			var functionParamType = chunk.functionParamTypes.buffer[i];
			fieldKinds[i] = functionParamType.kind;
		}
		Assert.Equal(expectedParamsKinds, fieldKinds);
	}

	[Theory]
	[InlineData("fn f():int{if true{1}else{0}}", 1)]
	[InlineData("fn f():int{if true{return 1}else{return 0}}", 1)]
	[InlineData("fn f():int{if true{1}else{return 0}}", 1)]
	[InlineData("fn f():int{if true{return 1}else{0}}", 1)]
	[InlineData("fn f():int{let a=if true{return 1}else{0} a}", 1)]
	[InlineData("fn f():int{let a=if true{1}else{return 0} a}", 1)]
	[InlineData("fn f():int{if true{return 1} 0}", 1)]
	private void ReturnIntTest(string source, int expected)
	{
		var v = TestHelper.Run<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v.value);
	}

	[Theory]
	[InlineData("fn f():int{let a=if true{return 1} a}")]
	[InlineData("fn f():int{let a=if true{1} a}")]
	private void ReturnIntTestError(string source)
	{
		Assert.Throws<CompileErrorException>(() =>
		{
			TestHelper.Run<Int>(source, out var a);
		});
	}

	[Theory]
	[InlineData("fn f():int{getS().a}", 2)]
	[InlineData("fn f():int{getS().b}", 3)]
	private void ReturnStructTest(string source, int expected)
	{
		var declarations = "struct S{a:int,b:int} fn getS():S{S{a=2,b=3}} ";
		source = declarations + source;
		var v = TestHelper.Run<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v.value);
	}

	[Theory]
	[InlineData("fn f():int{getA()[0]}", 7)]
	[InlineData("fn f():int{getA()[1]}", 8)]
	private void ReturnArrayTest(string source, int expected)
	{
		var declarations = "fn getA():[int]{let mut a=[7:2] set a[1]=8 a} ";
		source = declarations + source;
		var v = TestHelper.Run<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v.value);
	}

	[Theory]
	[InlineData("fn f():int{getSs()[0].a}", 2)]
	[InlineData("fn f():int{getSs()[0].b}", 3)]
	private void ReturnStructArrayTest(string source, int expected)
	{
		var declarations = "struct S{a:int,b:int} fn getSs():[S]{[S{a=2,b=3}:1]} ";
		source = declarations + source;
		var v = TestHelper.Run<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v.value);
	}

	[Theory]
	[InlineData("fn f() fn f(){}")]
	[InlineData("fn a(x:bool) fn b(x:bool){if x{a(!x)}} fn a(x:bool){b(x)} fn f(){a(true)}")]
	private void FunctionPrototypeTest(string source)
	{
		TestHelper.Run<Unit>(source, out var a);
		a.AssertSuccessCall();
	}

	[Theory]
	[InlineData("fn f(a:int) fn f(){}")]
	[InlineData("fn a() fn f(){}")]
	[InlineData("fn f() fn f() fn f(){}")]
	[InlineData("fn f(){} fn f()")]
	[InlineData("fn a() fn a() fn f(){}")]
	private void FunctionPrototypeTestError(string source)
	{
		Assert.Throws<CompileErrorException>(() =>
		{
			TestHelper.Run<Unit>(source, out var a);
		});
	}
}