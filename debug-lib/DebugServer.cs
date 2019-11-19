using System.Collections.Specialized;
using System.Threading;

namespace cflat.debug
{
	public sealed class DebugServer : IDebugger, IRequestHandler
	{
		public const int DefaultPort = 4747;

		internal VirtualMachine vm;
		internal Buffer<Source> sources = new Buffer<Source>();

		private readonly Server server;
		private SourcePosition lastPosition = new SourcePosition();

		internal Buffer<SourcePosition> breakpoints = new Buffer<SourcePosition>();
		internal bool paused = false;

		public DebugServer(int port)
		{
			server = new Server(port, this);
		}

		public void Start()
		{
			paused = true;
			server.Start();
		}

		public void ExecutionStop()
		{
			server.Stop();
		}

		void IDebugger.Reset(VirtualMachine vm, Buffer<Source> sources)
		{
			this.vm = vm;
			this.sources = sources;
		}

		void IDebugger.OnDebugHook()
		{
			var codeIndex = vm.callFrameStack.buffer[vm.callFrameStack.count - 1].codeIndex;
			if (codeIndex < 0)
				return;
			var sourceIndex = vm.chunk.FindSourceIndex(codeIndex);
			if (sourceIndex < 0)
				return;

			var source = sources.buffer[sourceIndex];
			var sourceSlice = vm.chunk.sourceSlices.buffer[codeIndex];
			var line = (ushort)(FormattingHelper.GetLineAndColumn(source.content, sourceSlice.index).lineIndex + 1);
			var position = new SourcePosition(source.uri, line);

			lock (this)
			{
				for (var i = 0; i < breakpoints.count; i++)
				{
					var breakpoint = breakpoints.buffer[i];
					if (
						(lastPosition.uri.value != position.uri.value ||
							lastPosition.line != breakpoint.line) &&
						position.line == breakpoint.line
					)
					{
						paused = true;
						break;
					}
				}
			}

			lastPosition = position;

			while (true)
			{
				lock (this)
				{
					if (paused)
						break;
				}

				Thread.Sleep(1000);
			}
		}

		void IRequestHandler.OnRequest(string uriLocalPath, NameValueCollection query, JsonWriter writer)
		{
			switch (uriLocalPath)
			{
			case "/execution/poll": this.ExecutionPoll(query, writer); break;
			case "/execution/continue": this.ExecutionContinue(query, writer); break;
			case "/execution/pause": this.ExecutionPause(query, writer); break;
			case "/execution/stop": this.ExecutionStop(); break;

			case "/breakpoints/all": this.BreakpointsAll(query, writer); break;
			case "/breakpoints/clear": this.BreakpointsClear(query, writer); break;
			case "/breakpoints/set": this.BreakpointsSet(query, writer); break;

			case "/values": this.Values(query, writer); break;
			case "/stacktrace": this.Stacktrace(query, writer); break;

			default: this.Help(query, writer); break;
			}
		}
	}
}
