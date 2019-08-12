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

	[Theory]
	[InlineData("struct A{a:int}", 1)]
	[InlineData("struct A{a:int b:int}", 2)]
	[InlineData("struct A{a:int b:int c:int}", 3)]
	[InlineData("struct A{a:int b:int} struct B{a:A b:int}", 3)]
	[InlineData("struct A{a:int b:int} struct B{a:A b:A}", 4)]
	public void ValueSizeTests(string source, int expectedSize)
	{
		var cc = new CompilerController();
		var chunk = new ByteCodeChunk();
		var errors = cc.Compile(source, chunk);
		Assert.Empty(errors);

		var type = new ValueType(TypeKind.Struct, chunk.structTypes.count - 1);
		var typeSize = chunk.GetTypeSize(type);
		Assert.Equal(expectedSize, typeSize);
	}
}