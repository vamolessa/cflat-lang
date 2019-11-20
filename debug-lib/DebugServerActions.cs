using System.Collections.Specialized;
using System.Text;

namespace cflat.debug
{
	internal static class DebugServerActions
	{
		public static void Help(this DebugServer self, NameValueCollection query, JsonWriter writer)
		{
			using var root = writer.Object;

			root.String("/", "show this help");

			root.String("/execution/poll", "poll execution state");
			root.String("/execution/continue", "resume execution");
			root.String("/execution/step", "step execution");
			root.String("/execution/pause", "pause execution");
			root.String("/execution/stop", "stop debug server");

			root.String("/breakpoints/all", "list all breakpoints of all sources");
			root.String("/breakpoints/clear", "clear all breakpoints of all sources");
			root.String("/breakpoints/set?source=dot.separated.source.uri&lines=1,2,42,999", "set all breakpoints for a single source");

			root.String("/values?myvar.field", "query values on the stack");
			root.String("/stacktrace", "query the stacktrace");
		}

		public static void ExecutionPoll(this DebugServer self, NameValueCollection query, JsonWriter writer)
		{
			using var root = writer.Object;
			var execution = self.execution switch
			{
				DebugServer.Execution.Continuing =>
					nameof(DebugServer.Execution.Continuing),
				DebugServer.Execution.Stepping =>
					nameof(DebugServer.Execution.Stepping),
				DebugServer.Execution.ExternalPaused =>
					nameof(DebugServer.Execution.ExternalPaused),
				DebugServer.Execution.BreakpointPaused =>
					nameof(DebugServer.Execution.BreakpointPaused),
				DebugServer.Execution.StepPaused =>
					nameof(DebugServer.Execution.StepPaused),
				_ => "",
			};
			root.String("execution", execution);
		}

		public static void ExecutionContinue(this DebugServer self, NameValueCollection query, JsonWriter writer)
		{
			self.execution = DebugServer.Execution.Continuing;
			self.ExecutionPoll(query, writer);
		}

		public static void ExecutionStep(this DebugServer self, NameValueCollection query, JsonWriter writer)
		{
			self.execution = DebugServer.Execution.Stepping;
			self.ExecutionPoll(query, writer);
		}

		public static void ExecutionPause(this DebugServer self, NameValueCollection query, JsonWriter writer)
		{
			self.execution = DebugServer.Execution.ExternalPaused;
			self.ExecutionPoll(query, writer);
		}

		public static void BreakpointsAll(this DebugServer self, NameValueCollection query, JsonWriter writer)
		{
			using var root = writer.Array;

			for (var i = 0; i < self.breakpoints.count; i++)
			{
				var breakpoint = self.breakpoints.buffer[i];

				using var b = root.Object;
				b.String("source", breakpoint.uri.value);
				b.Number("line", breakpoint.line);
			}
		}

		public static void BreakpointsClear(this DebugServer self, NameValueCollection query, JsonWriter writer)
		{
			self.breakpoints.count = 0;
			self.BreakpointsAll(query, writer);
		}

		public static void BreakpointsSet(this DebugServer self, NameValueCollection query, JsonWriter writer)
		{
			using var root = writer.Array;

			var uri = query["source"];
			if (string.IsNullOrEmpty(uri))
				return;

			var lines = query["lines"].Split(',');

			for (var i = self.breakpoints.count - 1; i >= 0; i--)
			{
				var breakpoint = self.breakpoints.buffer[i];
				if (breakpoint.uri.value == uri)
					self.breakpoints.SwapRemove(i);
			}

			foreach (var line in lines)
			{
				if (int.TryParse(line, out var lineNumber))
				{
					self.breakpoints.PushBack(new SourcePosition(
						new Uri(uri),
						(ushort)lineNumber
					));

					root.Number(lineNumber);
				}
			}
		}

		public static void Values(this DebugServer self, NameValueCollection query, JsonWriter writer)
		{
			using var root = writer.Array;

			var topCallFrame = self.vm.callFrameStack.buffer[self.vm.callFrameStack.count - 1];
			if (topCallFrame.type != CallFrame.Type.Function)
				return;

			var topDebugFrame = self.vm.debugData.frameStack.buffer[self.vm.debugData.frameStack.count - 1];
			var stackTypesBaseIndex = topDebugFrame.stackTypesBaseIndex + 1;

			var count = System.Math.Min(
				self.vm.debugData.stackTypes.count - stackTypesBaseIndex,
				self.vm.debugData.stackNames.count - topDebugFrame.stackNamesBaseIndex
			);
			var stackIndex = topCallFrame.baseStackIndex;

			var sb = new StringBuilder();

			for (var i = 0; i < count; i++)
			{
				var type = self.vm.debugData.stackTypes.buffer[stackTypesBaseIndex + i];
				var name = self.vm.debugData.stackNames.buffer[topDebugFrame.stackNamesBaseIndex + i];

				using var variable = root.Object;
				variable.String("name", name);

				sb.Clear();
				type.Format(self.vm.chunk, sb);
				variable.String("type", sb.ToString());

				sb.Clear();
				VirtualMachineHelper.ValueToString(self.vm, stackIndex, type, sb);
				variable.String("value", sb.ToString());

				stackIndex += type.GetSize(self.vm.chunk);
			}
		}

		public static void Stacktrace(this DebugServer self, NameValueCollection query, JsonWriter writer)
		{
			using var root = writer.Array;
			if (self.vm == null)
				return;

			var sb = new StringBuilder();

			for (var i = self.vm.callFrameStack.count - 1; i >= 0; i--)
			{
				var callframe = self.vm.callFrameStack.buffer[i];

				switch (callframe.type)
				{
				case CallFrame.Type.EntryPoint:
					break;
				case CallFrame.Type.Function:
					using (var st = root.Object)
					{
						var codeIndex = System.Math.Max(callframe.codeIndex - 1, 0);
						var sourceIndex = self.vm.chunk.sourceSlices.buffer[codeIndex].index;
						var source = self.sources.buffer[self.vm.chunk.FindSourceIndex(codeIndex)];
						var pos = FormattingHelper.GetLineAndColumn(
							source.content,
							sourceIndex
						);

						st.String("name", source.uri.value);
						st.Number("line", pos.lineIndex + 1);
						st.Number("column", pos.columnIndex + 1);
						st.String("source", source.uri.value);
					}
					break;
				case CallFrame.Type.NativeFunction:
					using (var st = root.Object)
					{
						sb.Clear();
						sb.Append("native ");
						self.vm.chunk.FormatNativeFunction(callframe.functionIndex, sb);
						st.String("name", sb.ToString());
					}
					break;
				}
			}
		}
	}
}