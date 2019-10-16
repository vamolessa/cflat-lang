using System.Runtime.InteropServices;
using Xunit;

public sealed class ValueTests
{
	[Fact]
	public unsafe void SizeTest()
	{
		void AssertIsBlitable<T>() where T : unmanaged { }

		AssertIsBlitable<ValueData>();
		Assert.Equal(4, sizeof(ValueData));
		Assert.Equal(4, Marshal.SizeOf(typeof(ValueData)));

		AssertIsBlitable<ValueType>();
		Assert.Equal(4, sizeof(ValueType));
		Assert.Equal(4, Marshal.SizeOf(typeof(ValueType)));
	}

	[Theory]
	[InlineData(TypeKind.Unit, 0, TypeFlags.None)]
	[InlineData(TypeKind.Int, 0, TypeFlags.None)]
	[InlineData(TypeKind.Struct, 0, TypeFlags.None)]
	[InlineData(TypeKind.Struct, 99, TypeFlags.None)]
	[InlineData(TypeKind.Struct, 99, TypeFlags.Array)]
	[InlineData(TypeKind.Unit, 0, TypeFlags.Array)]
	public void ValueTypeTests(TypeKind kind, ushort index, TypeFlags flags)
	{
		byte b0, b1, b2, b3;
		var type = new ValueType(kind, flags, index);
		type.Write(out b0, out b1, out b2, out b3);
		type = ValueType.Read(b0, b1, b2, b3);

		Assert.Equal(kind, type.Kind);
		Assert.Equal(index, type.index);
		Assert.Equal(flags, type.Flags);
	}

	[Theory]
	[InlineData("struct A{a:int}", 1)]
	[InlineData("struct A{a:int,b:int}", 2)]
	[InlineData("struct A{a:int,b:int,c:int}", 3)]
	[InlineData("struct A{a:int,b:int} struct B{a:A,b:int}", 3)]
	[InlineData("struct A{a:int,b:int} struct B{a:A,b:A}", 4)]
	public void StructSizeTests(string source, int expectedSize)
	{
		var cc = new CompilerController();
		var linking = new Linking();
		var errors = cc.Compile(linking, TestHelper.CompilerMode, source);
		var chunk = linking.chunks.buffer[1];
		Assert.Empty(errors.ToArray());

		var type = new ValueType(TypeKind.Struct, chunk.structTypes.count - 1);
		var typeSize = type.GetSize(chunk);
		Assert.Equal(expectedSize, typeSize);
	}

	[Fact]
	public void ArrayTypeTest()
	{
		var arrayType = new ValueType(TypeKind.Int, TypeFlags.Array, 18);
		Assert.True(arrayType.IsArray);
		Assert.Equal(TypeKind.Int, arrayType.Kind);
		Assert.Equal(TypeFlags.Array, arrayType.Flags);
		Assert.Equal(18, arrayType.index);

		arrayType = new ValueType(TypeKind.Int, 18).ToArrayType();
		Assert.True(arrayType.IsArray);
		Assert.Equal(TypeKind.Int, arrayType.Kind);
		Assert.Equal(TypeFlags.Array, arrayType.Flags);
		Assert.Equal(18, arrayType.index);
	}
}