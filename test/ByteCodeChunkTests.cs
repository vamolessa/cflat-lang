using Xunit;

public sealed class ByteCodeChunkTests
{
	[Fact]
	public unsafe void AnonymousStructTypeTest()
	{
		var chunk = new ByteCodeChunk();

		var builder = chunk.BeginAddStructType();
		Assert.Equal(0, chunk.structTypes.count);
		Assert.Equal(0, chunk.structTypeFields.count);

		builder.AddField("a", new ValueType(TypeKind.Int));
		builder.AddField("b", new ValueType(TypeKind.Int));
		Assert.Equal(2, chunk.structTypeFields.count);

		var type = chunk.GetAnonymousStructType(builder);
		Assert.Equal(1, chunk.structTypes.count);
		Assert.Equal(2, chunk.structTypeFields.count);

		Assert.Equal(2, type.size);
		Assert.Equal(0, type.fields.index);
		Assert.Equal(2, type.fields.length);

		builder = chunk.BeginAddStructType();
		Assert.Equal(1, chunk.structTypes.count);
		Assert.Equal(2, chunk.structTypeFields.count);

		builder.AddField("a", new ValueType(TypeKind.Int));
		builder.AddField("b", new ValueType(TypeKind.Int));
		Assert.Equal(4, chunk.structTypeFields.count);

		type = chunk.GetAnonymousStructType(builder);
		Assert.Equal(1, chunk.structTypes.count);
		Assert.Equal(2, chunk.structTypeFields.count);

		Assert.Equal(2, type.size);
		Assert.Equal(0, type.fields.index);
		Assert.Equal(2, type.fields.length);
	}
}