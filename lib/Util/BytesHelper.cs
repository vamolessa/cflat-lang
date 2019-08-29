public static class BytesHelper
{
	public static ushort BytesToUShort(byte b0, byte b1)
	{
		return (ushort)(b0 << 8 | b1);
	}

	public static void UShortToBytes(ushort u16, out byte b0, out byte b1)
	{
		b0 = (byte)(u16 >> 8);
		b1 = (byte)(u16 & 0xFF);
	}
}