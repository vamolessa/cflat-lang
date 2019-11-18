namespace cflat.debug
{
	public sealed class DebugServer : IDebugger
	{
		public const int DefaultPort = 14747;
		private Buffer<Source> sources = new Buffer<Source>();

		public DebugServer(int port)
		{

		}

		public void OnGetSources(Buffer<Source> sources)
		{
			this.sources = sources;
		}

		public void OnDebugHook(VirtualMachine vm)
		{
		}
	}
}
