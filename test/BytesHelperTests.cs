using Xunit;
using cflat;

public sealed class BytesHelperTests
{
	[Theory]
	[InlineData(0, 0, 0)]
	[InlineData(0, 1, 1)]
	[InlineData(1, 0, 0x100)]
	[InlineData(1, 1, 0x101)]
	[InlineData(1, 2, 0x102)]
	public void BytesToShortTest(byte b0, byte b1, ushort u16)
	{
		var r = BytesHelper.BytesToUShort(b0, b1);
		Assert.Equal(u16, r);
	}

	[Theory]
	[InlineData(0, 0, 0)]
	[InlineData(1, 0, 1)]
	[InlineData(0x100, 1, 0)]
	[InlineData(0x101, 1, 1)]
	[InlineData(0x102, 1, 2)]
	public void ShortToBytesTest(ushort u16, byte b0, byte b1)
	{
		BytesHelper.UShortToBytes(u16, out var r0, out var r1);
		Assert.Equal(b0, r0);
		Assert.Equal(b1, r1);
	}
}
