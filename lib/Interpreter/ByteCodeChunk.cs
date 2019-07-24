public sealed class ByteCodeChunk
{
	public Buffer<byte> bytes = new Buffer<byte>(256);
	public Buffer<LineAndColumn> positions = new Buffer<LineAndColumn>(256);
	public Buffer<Value> constants = new Buffer<Value>(256);

	public int AddConstant(Value value)
	{
		var index = constants.count;
		constants.PushBack(value);
		return index;
	}

	public void WriteByte(byte value, LineAndColumn position)
	{
		bytes.PushBack(value);
		positions.PushBack(position);
	}

	public void WriteInstruction(Instruction instruction, LineAndColumn position)
	{
		WriteByte((byte)instruction, position);
	}

	public void WriteConstantIndex(int index, LineAndColumn position)
	{
		WriteByte((byte)index, position);
	}
}