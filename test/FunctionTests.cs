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
		var errors = compiler.Compile(source, chunk, TestHelper.CompilerMode);
		if (errors.count > 0)
			error = CompilerHelper.FormatError(source, errors, 1, 8);
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
}