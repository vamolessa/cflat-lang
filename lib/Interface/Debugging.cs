namespace cflat
{
	public interface IDebugger
	{
		void Reset(VirtualMachine vm, Buffer<Source> sources);
		void OnDebugHook();
	}

	public readonly struct SourcePosition
	{
		public readonly Uri uri;
		public readonly ushort line;

		public SourcePosition(Uri uri, ushort line)
		{
			this.uri = uri;
			this.line = line;
		}
	}
}