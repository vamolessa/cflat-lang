using Xunit;

public sealed class ByteCodeChunkTests
{
	[Fact]
	public unsafe void AnonymousStructTypeTest()
	{
		var chunk = new ByteCodeChunk();

		var builder = chunk.BeginStructType();
		Assert.Equal(0, chunk.structTypes.count);
		Assert.Equal(0, chunk.structTypeFields.count);

		builder.WithField("a", new ValueType(TypeKind.Int));
		builder.WithField("b", new ValueType(TypeKind.Int));
		Assert.Equal(2, chunk.structTypeFields.count);

		var typeIndex = builder.BuildAnonymous();
		var type = chunk.structTypes.buffer[typeIndex];
		Assert.Equal(1, chunk.structTypes.count);
		Assert.Equal(2, chunk.structTypeFields.count);

		Assert.Equal(2, type.size);
		Assert.Equal(0, type.fields.index);
		Assert.Equal(2, type.fields.length);

		builder = chunk.BeginStructType();
		Assert.Equal(1, chunk.structTypes.count);
		Assert.Equal(2, chunk.structTypeFields.count);

		builder.WithField("a", new ValueType(TypeKind.Int));
		builder.WithField("b", new ValueType(TypeKind.Int));
		Assert.Equal(4, chunk.structTypeFields.count);

		typeIndex = builder.BuildAnonymous();
		type = chunk.structTypes.buffer[typeIndex];
		Assert.Equal(1, chunk.structTypes.count);
		Assert.Equal(2, chunk.structTypeFields.count);

		Assert.Equal(2, type.size);
		Assert.Equal(0, type.fields.index);
		Assert.Equal(2, type.fields.length);
	}
}