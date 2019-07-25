public sealed class ByteCodeChunk
{
	public Buffer<byte> bytes = new Buffer<byte>(256);
	public Buffer<int> sourceIndexes = new Buffer<int>(256);
	public Buffer<Value> constants = new Buffer<Value>(256);

	public int AddConstant(Value value)
	{
		var index = constants.count;
		constants.PushBack(value);
		return index;
	}

	public void WriteByte(byte value, int sourceIndex)
	{
		bytes.PushBack(value);
		sourceIndexes.PushBack(sourceIndex);
	}
}