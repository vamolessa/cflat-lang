public sealed class ByteCodeChunk
{
	public Buffer<byte> instructions = new Buffer<byte>(256);
	public Buffer<int> constantIntegers = new Buffer<int>(256);
	public Buffer<LineAndColumn> lines = new Buffer<LineAndColumn>(256);

	public void WriteByte(byte b)
	{
		instructions.PushBack(b);
	}
}