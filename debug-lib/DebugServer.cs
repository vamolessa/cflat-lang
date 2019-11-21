using System.Collections.Specialized;
using System.Threading;

namespace cflat.debug
{
	public sealed class DebugServer : IDebugger, IRequestHandler, System.IDisposable
	{
		public enum Execution
		{
			Continuing,
			Stepping,
			ExternalPaused,
			BreakpointPaused,
			StepPaused,
		}

		public const int DefaultPort = 4747;

		internal VirtualMachine vm;
		internal Buffer<Source> sources = new Buffer<Source>();

		private readonly Server server;
		private SourcePosition lastPosition = new SourcePosition();

		internal Buffer<SourcePosition> breakpoints = new Buffer<SourcePosition>();
		internal Execution execution = Execution.ExternalPaused;

		public bool IsPaused
		{
			get
			{
				return
					execution == Execution.ExternalPaused ||
					execution == Execution.BreakpointPaused ||
					execution == Execution.StepPaused;
			}
		}

		public DebugServer(int port)
		{
			server = new Server(port, this);
		}

		public void StartExecuting()
		{
			execution = Execution.Continuing;
			server.Start();
		}

		public void StartPaused()
		{
			execution = Execution.ExternalPaused;
			server.Start();
		}

		public void ExecutionStop()
		{
			execution = Execution.ExternalPaused;
			server.Stop();
		}

		void System.IDisposable.Dispose()
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

			Execution ex;
			lock (this)
			{
				ex = execution;
			}

			switch (ex)
			{
			case Execution.Continuing:
				lock (this)
				{
					for (var i = 0; i < breakpoints.count; i++)
					{
						var breakpoint = breakpoints.buffer[i];
						var wasOnBreakpoint =
							lastPosition.uri.value == position.uri.value &&
							lastPosition.line == breakpoint.line;

						if (!wasOnBreakpoint && position.line == breakpoint.line)
						{
							execution = Execution.BreakpointPaused;
							break;
						}
					}
				}
				break;
			case Execution.Stepping:
				if (lastPosition.uri.value != position.uri.value || lastPosition.line != position.line)
				{
					lock (this)
					{
						execution = Execution.StepPaused;
					}
					break;
				}
				break;
			}

			while (true)
			{
				lock (this)
				{
					if (!IsPaused)
						break;
				}

				Thread.Sleep(1000);
			}

			lastPosition = position;
		}

		void IRequestHandler.OnRequest(string uriLocalPath, NameValueCollection query, JsonWriter writer)
		{
			var sb = new System.Text.StringBuilder();
			sb.AppendLine(uriLocalPath);
			foreach (var key in query.AllKeys)
			{
				sb.Append("> ");
				sb.Append(key);
				sb.Append(": ");
				sb.Append(query[key]);
				sb.AppendLine();
			}
			sb.AppendLine("--\n");
			System.Console.Write(sb);

			switch (uriLocalPath)
			{
			case "/execution/poll": this.ExecutionPoll(query, writer); break;
			case "/execution/continue": this.ExecutionContinue(query, writer); break;
			case "/execution/step": this.ExecutionStep(query, writer); break;
			case "/execution/pause": this.ExecutionPause(query, writer); break;
			case "/execution/stop": this.ExecutionStop(); break;

			case "/breakpoints/list": this.BreakpointsAll(query, writer); break;
			case "/breakpoints/clear": this.BreakpointsClear(query, writer); break;
			case "/breakpoints/set": this.BreakpointsSet(query, writer); break;

			case "/values/stack": this.Values(query, writer); break;

			case "/stacktrace": this.Stacktrace(query, writer); break;

			default: this.Help(query, writer); break;
			}
		}
	}
}
