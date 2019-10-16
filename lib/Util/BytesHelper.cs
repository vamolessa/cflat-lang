public static class BytesHelper
{
	public static ushort BytesToUShort(byte b0, byte b1)
	{
		return unchecked((ushort)(b0 << 8 | b1));
	}

	public static void UShortToBytes(ushort u16, out byte b0, out byte b1)
	{
		unchecked
		{
			b0 = (byte)(u16 >> 8);
			b1 = (byte)(u16);
		}
	}

	public static uint BytesToUInt(byte b0, byte b1, byte b2, byte b3)
	{
		return unchecked((uint)(b0 << 24 | b1 << 16 | b2 << 8 | b3));
	}

	public static void UIntToBytes(uint u32, out byte b0, out byte b1, out byte b2, out byte b3)
	{
		unchecked
		{
			b0 = (byte)(u32 >> 24);
			b1 = (byte)(u32 >> 16);
			b2 = (byte)(u32 >> 8);
			b3 = (byte)(u32);
		}
	}
}