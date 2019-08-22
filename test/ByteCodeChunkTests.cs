using Xunit;

public sealed class ByteCodeChunkTests
{
	[Fact]
	public unsafe void TupleDeclarationTest()
	{
		var chunk = new ByteCodeChunk();

		var builder = chunk.BeginTupleType();
		Assert.Equal(0, chunk.tupleTypes.count);
		Assert.Equal(0, chunk.tupleElementTypes.count);

		builder.WithElement(new ValueType(TypeKind.Int));
		builder.WithElement(new ValueType(TypeKind.Int));
		Assert.Equal(2, chunk.tupleElementTypes.count);

		var typeIndex = builder.Build();
		var type = chunk.tupleTypes.buffer[typeIndex];
		Assert.Equal(1, chunk.tupleTypes.count);
		Assert.Equal(2, chunk.tupleElementTypes.count);

		Assert.Equal(2, type.size);
		Assert.Equal(0, type.elements.index);
		Assert.Equal(2, type.elements.length);

		builder = chunk.BeginTupleType();
		Assert.Equal(1, chunk.tupleTypes.count);
		Assert.Equal(2, chunk.tupleElementTypes.count);

		builder.WithElement(new ValueType(TypeKind.Int));
		builder.WithElement(new ValueType(TypeKind.Int));
		Assert.Equal(4, chunk.tupleElementTypes.count);

		typeIndex = builder.Build();
		type = chunk.tupleTypes.buffer[typeIndex];
		Assert.Equal(1, chunk.tupleTypes.count);
		Assert.Equal(2, chunk.tupleElementTypes.count);

		Assert.Equal(2, type.size);
		Assert.Equal(0, type.elements.index);
		Assert.Equal(2, type.elements.length);
	}
}