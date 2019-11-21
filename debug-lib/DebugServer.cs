using System.Collections.Specialized;
using System.Text;
using System.Threading;

namespace cflat.debug
{
	public sealed class DebugServer : IDebugger, Server.IRequestHandler, System.IDisposable
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

		public void Stop()
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
							//lastPosition.uri.value == position.uri.value &&
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

		Server.ResponseType Server.IRequestHandler.OnRequest(string uriLocalPath, NameValueCollection query, StringBuilder sb)
		{
			var debugSb = new System.Text.StringBuilder();
			debugSb.AppendLine(uriLocalPath);
			foreach (var key in query.AllKeys)
			{
				debugSb.Append("> ");
				debugSb.Append(key);
				debugSb.Append(": ");
				debugSb.Append(query[key]);
				debugSb.AppendLine();
			}
			debugSb.AppendLine("--\n");
			System.Console.Write(debugSb);

			return uriLocalPath switch
			{
				"/execution/poll" =>
					this.ExecutionPoll(query, sb),
				"/execution/continue" =>
					this.ExecutionContinue(query, sb),
				"/execution/step" =>
					this.ExecutionStep(query, sb),
				"/execution/pause" =>
					this.ExecutionPause(query, sb),
				"/execution/stop" =>
					this.ExecutionStop(query, sb),

				"/breakpoints/list" =>
					this.BreakpointsAll(query, sb),
				"/breakpoints/clear" =>
					this.BreakpointsClear(query, sb),
				"/breakpoints/set" =>
					this.BreakpointsSet(query, sb),

				"/values/stack" =>
					this.Values(query, sb),

				"/stacktrace" =>
					this.Stacktrace(query, sb),

				"/sources/list" =>
					this.SourcesList(query, sb),
				"/sources/content" =>
					this.SourcesContent(query, sb),

				_ =>
					this.Help(query, sb),
			};
		}
	}
}
