public sealed class VirtualMachine
{
	private int programCount;
	private ByteCodeChunk chunk;

	public void Load(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
	}

	public void Run()
	{
		programCount = 0;
	}
}