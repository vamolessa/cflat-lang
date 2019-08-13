using System.Runtime.InteropServices;
using Xunit;

public sealed class ValueTests
{
	[Fact]
	public unsafe void SizeTest()
	{
		Assert.Equal(4, sizeof(ValueData));
		Assert.Equal(4, Marshal.SizeOf(typeof(ValueData)));
		Assert.Equal(4, sizeof(ValueType));
		Assert.Equal(4, Marshal.SizeOf(typeof(ValueType)));
	}

	[Theory]
	[InlineData(TypeKind.Unit, 0, 0)]
	[InlineData(TypeKind.Int, 0, 0)]
	[InlineData(TypeKind.Struct, 0, 0)]
	[InlineData(TypeKind.Struct, 99, 0)]
	[InlineData(TypeKind.Struct, 99, 1)]
	[InlineData(TypeKind.Unit, 0, 1)]
	public void ValueTypeTests(TypeKind kind, ushort index, byte flags)
	{
		byte b0, b1, b2, b3;
		var type = new ValueType(index, kind, flags);
		type.Write(out b0, out b1, out b2, out b3);
		type = ValueType.Read(b0, b1, b2, b3);

		Assert.Equal(kind, type.kind);
		Assert.Equal(index, type.index);
		Assert.Equal(flags, type.flags);
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
		var typeSize = type.GetSize(chunk);
		Assert.Equal(expectedSize, typeSize);
	}
}