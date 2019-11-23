using System.Collections.Specialized;
using System.Text;

namespace cflat.debug
{
	internal static class DebugServerActions
	{
		public static Server.ResponseType Help(this DebugServer self, NameValueCollection query, StringBuilder sb)
		{
			using var writer = new JsonWriter(sb);
			using var root = writer.Object;

			root.String("/", "show this help");

			root.String("/execution/poll", "poll execution state");
			root.String("/execution/continue", "resume execution");
			root.String("/execution/step", "step execution");
			root.String("/execution/pause", "pause execution");
			root.String("/execution/stop", "stop debug server");

			root.String("/breakpoints/list", "list all breakpoints of all sources");
			root.String("/breakpoints/clear", "clear all breakpoints of all sources");
			root.String("/breakpoints/set?path=source/path&lines=1,2,42,999", "set all breakpoints for a single source");

			root.String("/values/stack", "list values on the stack");

			root.String("/stacktrace", "query the stacktrace");

			root.String("/sources/list", "list all loaded sources");
			root.String("/sources/content?uri=source/uri", "list all loaded sources");
			return Server.ResponseType.Json;
		}

		public static Server.ResponseType ExecutionPoll(this DebugServer self, NameValueCollection query, StringBuilder sb)
		{
			using var writer = new JsonWriter(sb);
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
			return Server.ResponseType.Json;
		}

		public static Server.ResponseType ExecutionContinue(this DebugServer self, NameValueCollection query, StringBuilder sb)
		{
			self.execution = DebugServer.Execution.Continuing;
			return self.ExecutionPoll(query, sb);
		}

		public static Server.ResponseType ExecutionStep(this DebugServer self, NameValueCollection query, StringBuilder sb)
		{
			self.execution = DebugServer.Execution.Stepping;
			return self.ExecutionPoll(query, sb);
		}

		public static Server.ResponseType ExecutionPause(this DebugServer self, NameValueCollection query, StringBuilder sb)
		{
			self.execution = DebugServer.Execution.ExternalPaused;
			return self.ExecutionPoll(query, sb);
		}

		public static Server.ResponseType ExecutionStop(this DebugServer self, NameValueCollection query, StringBuilder sb)
		{
			self.Stop();
			return Server.ResponseType.Text;
		}

		public static Server.ResponseType BreakpointsAll(this DebugServer self, NameValueCollection query, StringBuilder sb)
		{
			using var writer = new JsonWriter(sb);
			using var root = writer.Array;

			for (var i = 0; i < self.breakpoints.count; i++)
			{
				var breakpoint = self.breakpoints.buffer[i];

				using var b = root.Object;
				b.String("source", breakpoint.uri.value);
				b.Number("line", breakpoint.line);
			}

			return Server.ResponseType.Json;
		}

		public static Server.ResponseType BreakpointsClear(this DebugServer self, NameValueCollection query, StringBuilder sb)
		{
			self.breakpoints.count = 0;
			return self.BreakpointsAll(query, sb);
		}

		public static Server.ResponseType BreakpointsSet(this DebugServer self, NameValueCollection query, StringBuilder sb)
		{
			using var writer = new JsonWriter(sb);
			using var root = writer.Object;

			var path = query["path"];
			if (string.IsNullOrEmpty(path))
				return Server.ResponseType.Json;

			var lines = query["lines"].Split(',');

			var uri = new Uri();
			for (var i = 0; i < self.sources.count; i++)
			{
				var source = self.sources.buffer[i];
				if (path.EndsWith(source.uri.value))
				{
					uri = source.uri;
					break;
				}
			}

			if (string.IsNullOrEmpty(uri.value))
				return Server.ResponseType.Json;

			root.String("sourceUri", uri.value);
			using var breakpoints = root.Array("breakpoints");

			for (var i = self.breakpoints.count - 1; i >= 0; i--)
			{
				var breakpoint = self.breakpoints.buffer[i];
				if (breakpoint.uri.value == uri.value)
					self.breakpoints.SwapRemove(i);
			}

			foreach (var line in lines)
			{
				if (int.TryParse(line, out var lineNumber))
				{
					self.breakpoints.PushBack(new SourcePosition(
						uri,
						(ushort)lineNumber
					));

					breakpoints.Number(lineNumber);
				}
			}

			return Server.ResponseType.Json;
		}

		public static Server.ResponseType Values(this DebugServer self, NameValueCollection query, StringBuilder sb)
		{
			using var writer = new JsonWriter(sb);
			using var root = writer.Object;

			var pathString = query["path"];
			var path = string.IsNullOrEmpty(pathString) ?
				new string[1] :
				pathString.Split('.');

			if (self.vm == null)
				return Server.ResponseType.Json;

			var topCallFrame = self.vm.callFrameStack.buffer[self.vm.callFrameStack.count - 1];
			if (topCallFrame.type != CallFrame.Type.Function)
				return Server.ResponseType.Json;

			var topDebugFrame = self.vm.debugData.frameStack.buffer[self.vm.debugData.frameStack.count - 1];
			var stackTypesBaseIndex = topDebugFrame.stackTypesBaseIndex + 1;

			var count = System.Math.Min(
				self.vm.debugData.stackTypes.count - stackTypesBaseIndex,
				self.vm.debugData.stackNames.count - topDebugFrame.stackNamesBaseIndex
			);
			var stackIndex = topCallFrame.baseStackIndex;

			var cacheSb = new StringBuilder();

			if (string.IsNullOrEmpty(path[0]))
			{
				using var valuesWriter = root.Array("values");
				for (var i = 0; i < count; i++)
				{
					var name = self.vm.debugData.stackNames.buffer[topDebugFrame.stackNamesBaseIndex + i];
					var type = self.vm.debugData.stackTypes.buffer[stackTypesBaseIndex + i];

					using var valueWriter = valuesWriter.Object;
					path[0] = name;
					NestedValues(
						self.vm,
						ref stackIndex,
						path,
						1,
						type,
						cacheSb,
						valueWriter
					);
				}
			}
			else
			{
				for (var i = 0; i < count; i++)
				{
					var name = self.vm.debugData.stackNames.buffer[topDebugFrame.stackNamesBaseIndex + i];
					var type = self.vm.debugData.stackTypes.buffer[stackTypesBaseIndex + i];

					if (path[0] == name)
					{
						NestedValues(
							self.vm,
							ref stackIndex,
							path,
							1,
							type,
							cacheSb,
							root
						);
						break;
					}
				}
			}

			return Server.ResponseType.Json;
		}

		private static void NestedValues(VirtualMachine vm, ref int memoryIndex, string[] path, int pathIndex, ValueType type, StringBuilder sb, JsonWriter.ObjectScope writer)
		{
			var isReferenceType = type.IsArray || type.IsReference;

			if (pathIndex < path.Length)
			{
				if (isReferenceType)
					return;

				var name = path[pathIndex++];
				if (type.kind == TypeKind.Struct)
				{
					var structType = vm.chunk.structTypes.buffer[type.index];
					for (var i = 0; i < structType.fields.length; i++)
					{
						var field = vm.chunk.structTypeFields.buffer[structType.fields.index + i];
						if (field.name == name)
						{
							NestedValues(
								vm,
								ref memoryIndex,
								path,
								pathIndex,
								field.type,
								sb,
								writer
							);
							break;
						}

						memoryIndex += field.type.GetSize(vm.chunk);
					}
				}
				else if (type.kind == TypeKind.Tuple)
				{
					if (!int.TryParse(name, out var elementIndex))
						return;

					var tupleType = vm.chunk.tupleTypes.buffer[type.index];
					for (var i = 0; i < tupleType.elements.length; i++)
					{
						var elementType = vm.chunk.tupleElementTypes.buffer[tupleType.elements.index + i];

						if (elementIndex == i)
						{
							NestedValues(
								vm,
								ref memoryIndex,
								path,
								pathIndex,
								elementType,
								sb,
								writer
							);
							break;
						}

						memoryIndex += elementType.GetSize(vm.chunk);
					}
				}
			}
			else
			{
				DebugHelper.WriteValue(
					vm,
					memoryIndex,
					path[path.Length - 1],
					type,
					sb,
					writer
				);

				if (isReferenceType)
					return;

				if (type.kind == TypeKind.Struct)
				{
					using var valueWriter = writer.Array("values");
					var structType = vm.chunk.structTypes.buffer[type.index];
					for (var i = 0; i < structType.fields.length; i++)
					{
						var field = vm.chunk.structTypeFields.buffer[structType.fields.index + i];
						using var fieldWriter = valueWriter.Object;

						DebugHelper.WriteValue(
							vm,
							memoryIndex,
							field.name,
							field.type,
							sb,
							fieldWriter
						);

						memoryIndex += field.type.GetSize(vm.chunk);
					}
				}
				else if (type.kind == TypeKind.Tuple)
				{
					using var valueWriter = writer.Array("values");
					var tupleType = vm.chunk.tupleTypes.buffer[type.index];
					for (var i = 0; i < tupleType.elements.length; i++)
					{
						var elementType = vm.chunk.tupleElementTypes.buffer[tupleType.elements.index + i];
						using var elementWriter = valueWriter.Object;

						DebugHelper.WriteValue(
							vm,
							memoryIndex,
							i.ToString(),
							elementType,
							sb,
							elementWriter
						);

						memoryIndex += elementType.GetSize(vm.chunk);
					}
				}
			}
		}

		public static Server.ResponseType Stacktrace(this DebugServer self, NameValueCollection query, StringBuilder sb)
		{
			using var writer = new JsonWriter(sb);
			using var root = writer.Array;

			if (self.vm == null)
				return Server.ResponseType.Json;

			var cache = new StringBuilder();

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
						var sourceContentIndex = self.vm.chunk.sourceSlices.buffer[codeIndex].index;
						var source = self.sources.buffer[self.vm.chunk.FindSourceIndex(codeIndex)];
						var pos = FormattingHelper.GetLineAndColumn(
							source.content,
							sourceContentIndex
						);

						cache.Clear();
						self.vm.chunk.FormatFunction(callframe.functionIndex, cache);
						st.String("name", cache.ToString());
						st.Number("line", pos.lineIndex + 1);
						st.Number("column", pos.columnIndex + 1);
						st.String("sourceUri", source.uri.value);
					}
					break;
				case CallFrame.Type.NativeFunction:
					using (var st = root.Object)
					{
						cache.Clear();
						cache.Append("native ");
						self.vm.chunk.FormatNativeFunction(callframe.functionIndex, cache);
						st.String("name", cache.ToString());
					}
					break;
				}
			}

			return Server.ResponseType.Json;
		}

		public static Server.ResponseType SourcesList(this DebugServer self, NameValueCollection query, StringBuilder sb)
		{
			using var writer = new JsonWriter(sb);
			using var root = writer.Array;
			for (var i = 0; i < self.sources.count; i++)
			{
				var source = self.sources.buffer[i];
				root.String(source.uri.value);
			}

			return Server.ResponseType.Json;
		}

		public static Server.ResponseType SourcesContent(this DebugServer self, NameValueCollection query, StringBuilder sb)
		{
			var sourceUri = query["uri"];
			if (string.IsNullOrEmpty(sourceUri))
				return Server.ResponseType.Text;

			var uri = new Uri(sourceUri);

			for (var i = 0; i < self.sources.count; i++)
			{
				var source = self.sources.buffer[i];
				if (source.uri.value == uri.value)
				{
					sb.Append(source.content);
					break;
				}
			}

			return Server.ResponseType.Text;
		}
	}
}