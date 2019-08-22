using Xunit;

public sealed class TupleTests
{
	[Theory]
	[InlineData("fn f():int{let{a b}=tuple{true 3} a b}", 3)]
	[InlineData("fn f():int{let{_ b}=tuple{true 3} b}", 3)]
	[InlineData("fn f():int{let t=tuple{true 3} let{a b}=t a b}", 3)]
	public void TupleDeconstructionTests(string source, int expected)
	{
		TestHelper.Run(source, out var a).GetInt(out var v);
		a.AssertSuccessCall();
		Assert.Equal(expected, v);
	}

	[Theory]
	[InlineData("fn b(t:tuple{bool int}):int{let{a b}=t a b}fn f():int{b(tuple{true 3})}", 3)]
	public void TupleParameterTests(string source, int expected)
	{
		TestHelper.Run(source, out var a).GetInt(out var v);
		a.AssertSuccessCall();
		Assert.Equal(expected, v);
	}

	[Theory]
	[InlineData(
		"fn f(){tuple{tuple{true true} 3}}",
		new[] { 0, 2, 2, 2 },
		new[] {
			TypeKind.Bool,
			TypeKind.Bool,
			TypeKind.Tuple,
			TypeKind.Int
		}
	)]
	[InlineData(
		"fn f(){tuple{0 tuple{true true} 3}}",
		new[] { 0, 2, 2, 3 },
		new[] {
			TypeKind.Bool,
			TypeKind.Bool,
			TypeKind.Int,
			TypeKind.Tuple,
			TypeKind.Int
		}
	)]
	[InlineData(
		"fn f(){tuple{0 tuple{tuple{1.0 2.0} true} 3}}",
		new[] { 0, 2, 2, 2, 4, 3 },
		new[] {
			TypeKind.Float,
			TypeKind.Float,
			TypeKind.Tuple,
			TypeKind.Bool,
			TypeKind.Int,
			TypeKind.Tuple,
			TypeKind.Int
		}
	)]
	public void NestedTupleTest(string source, int[] expectedSlices, TypeKind[] expectedElementKinds)
	{
		var compiler = new CompilerController();
		var chunk = new ByteCodeChunk();

		string error = null;
		var errors = compiler.Compile(source, chunk);
		if (errors.count > 0)
			error = CompilerHelper.FormatError(source, errors, 1, 8);
		Assert.Null(error);

		var sliceCount = expectedSlices.Length / 2;
		Assert.Equal(sliceCount, chunk.tupleTypes.count);
		Assert.Equal(expectedElementKinds.Length, chunk.tupleElementTypes.count);

		var slices = new int[chunk.tupleTypes.count * 2];
		for (var i = 0; i < sliceCount; i++)
		{
			var tupleType = chunk.tupleTypes.buffer[i];
			slices[i * 2] = tupleType.elements.index;
			slices[i * 2 + 1] = tupleType.elements.length;
		}
		Assert.Equal(expectedSlices, slices);

		var elementKinds = new TypeKind[chunk.tupleElementTypes.count];
		for (var i = 0; i < elementKinds.Length; i++)
		{
			var elementType = chunk.tupleElementTypes.buffer[i];
			elementKinds[i] = elementType.kind;
		}
		Assert.Equal(expectedElementKinds, elementKinds);
	}
}