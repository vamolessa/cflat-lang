using Xunit;

public sealed class ValueTests
{
	[Theory]
	[InlineData(TypeKind.Unit, 0, false)]
	[InlineData(TypeKind.Int, 0, false)]
	[InlineData(TypeKind.Struct, 0, false)]
	[InlineData(TypeKind.Struct, 99, false)]
	[InlineData(TypeKind.Struct, 99, true)]
	[InlineData(TypeKind.Unit, 0, true)]
	public void ValueTypeTests(TypeKind kind, ushort index, bool isReference)
	{
		byte b0, b1, b2, b3;
		var type = new ValueType(index, kind, isReference);
		type.Write(out b0, out b1, out b2, out b3);
		type = ValueType.Read(b0, b1, b2, b3);

		Assert.Equal(kind, type.kind);
		Assert.Equal(index, type.index);
		Assert.Equal(isReference, type.isReference);
	}
}