using System.Collections.Specialized;

namespace cflat.debug
{
	public sealed class DebugServer : IDebugger, IRequestHandler
	{
		public const int DefaultPort = 14747;

		private readonly Server server;
		private Buffer<Source> sources = new Buffer<Source>();

		internal Buffer<SourcePosition> breakpoints = new Buffer<SourcePosition>();
		internal bool paused = true;

		public DebugServer(int port)
		{
			server = new Server(port, this);
		}

		public void Start()
		{
			server.Start();
		}

		public void Stop()
		{
			server.Stop();
		}

		void IDebugger.OnGetSources(Buffer<Source> sources)
		{
			this.sources = sources;
		}

		void IDebugger.OnDebugHook(VirtualMachine vm)
		{
		}

		void IRequestHandler.OnRequest(string uriLocalPath, NameValueCollection query, JsonWriter writer)
		{
			switch (uriLocalPath)
			{
			case "/continue": this.Continue(query, writer); break;
			case "/pause": this.Pause(query, writer); break;
			case "/stop": this.Stop(); break;

			case "/breakpoints/all": this.BreakpointsAll(query, writer); break;
			case "/breakpoints/clear": this.BreakpointsClear(query, writer); break;
			case "/breakpoints/set": this.BreakpointsSet(query, writer); break;

			case "/query/paused": this.QueryPaused(query, writer); break;
			case "/query/all": this.QueryAll(query, writer); break;
			case "/query/value": this.QueryValue(query, writer); break;

			default: this.Help(query, writer); break;
			}
		}
	}
}
