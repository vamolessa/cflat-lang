using Xunit;

public sealed class ValueTests
{
	[Theory]
	[InlineData(ValueKind.Unit, 0, false)]
	[InlineData(ValueKind.Int, 0, false)]
	[InlineData(ValueKind.Struct, 0, false)]
	[InlineData(ValueKind.Struct, 99, false)]
	[InlineData(ValueKind.Struct, 99, true)]
	[InlineData(ValueKind.Unit, 0, true)]
	public void ValueTypeTests(ValueKind kind, ushort index, bool isReference)
	{
		var bytes = new byte[4];
		var type = new ValueType(index, kind, isReference);
		type.Write(bytes, 0);
		type = ValueType.Read(bytes, 0);

		Assert.Equal(kind, type.kind);
		Assert.Equal(index, type.index);
		Assert.Equal(isReference, type.isReference);
	}

	[Theory]
	[InlineData(ValueKind.Unit, 0, false)]
	[InlineData(ValueKind.Int, 0, false)]
	[InlineData(ValueKind.Struct, 0, false)]
	[InlineData(ValueKind.Struct, 99, false)]
	[InlineData(ValueKind.Struct, 99, true)]
	[InlineData(ValueKind.Unit, 0, true)]
	public void ValueTypeWithOffsetTests(ValueKind kind, ushort index, bool isReference)
	{
		var offset = 3;
		var bytes = new byte[4 + offset];
		var type = new ValueType(index, kind, isReference);
		type.Write(bytes, offset);
		type = ValueType.Read(bytes, offset);

		Assert.Equal(kind, type.kind);
		Assert.Equal(index, type.index);
		Assert.Equal(isReference, type.isReference);
	}
}