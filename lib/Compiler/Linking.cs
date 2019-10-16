public sealed class Linking
{
	public Buffer<ByteCodeChunk> chunks = new Buffer<ByteCodeChunk>(2);

	public ByteCodeChunk BindingChunk
	{
		get { return chunks.buffer[0]; }
	}

	public ByteCodeChunk FindChunk(ChunkId chunkId)
	{
		for (var i = 0; i < chunks.count; i++)
		{
			var chunk = chunks.buffer[i];
			if (chunk.id.IsEqualTo(chunkId))
				return chunk;
		}

		return null;
	}

	public Linking()
	{
		chunks.PushBack(new ByteCodeChunk(new ChunkId()));
	}
}