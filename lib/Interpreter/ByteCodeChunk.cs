public sealed class ByteCodeChunk
{
	public Buffer<byte> bytes = new Buffer<byte>(256);
	public Buffer<LineAndColumn> positions = new Buffer<LineAndColumn>(256);
	public Buffer<Value> constants = new Buffer<Value>(256);

	public void Write(byte code, LineAndColumn position)
	{
		bytes.PushBack(code);
		positions.PushBack(position);
	}

	public void WriteConstant(Value value, LineAndColumn position)
	{
		bytes.PushBack((byte)Instruction.LoadConstant);
		positions.PushBack(position);

		var index = System.Array.IndexOf(constants.buffer, value);
		if (index < 0)
		{
			index = constants.count;
			constants.PushBack(value);
		}

		bytes.PushBack((byte)index);
	}
}