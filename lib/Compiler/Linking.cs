public sealed class Linking
{
	public Buffer<ByteCodeChunk> chunks = new Buffer<ByteCodeChunk>(2);

	public ByteCodeChunk BindingChunk
	{
		get { return chunks.buffer[0]; }
	}

	public ByteCodeChunk GetTypeChunk(ValueType type)
	{
		return chunks.buffer[type.chunkIndex];
	}

	public Linking()
	{
		chunks.PushBack(new ByteCodeChunk());
	}
}