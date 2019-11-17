using System.Text;

namespace cflat
{
	public interface IDebugger
	{
		void OnGetSources(Buffer<Source> sources);
		void OnDebugHook(VirtualMachine vm);
	}

	public sealed class Debugger : IDebugger
	{
		public delegate void BreakCallback(SourcePosition sourcePosition, LocalVariable[] localVariables);

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

		public readonly struct LocalVariable
		{
			public readonly string name;
			public readonly string type;
			public readonly string value;

			public LocalVariable(string name, string type, string value)
			{
				this.name = name;
				this.type = type;
				this.value = value;
			}
		}

		private readonly BreakCallback onBreak;
		private Buffer<SourcePosition> breakpoints = new Buffer<SourcePosition>(8);
		private Buffer<Source> sources = new Buffer<Source>();
		private SourcePosition lastPosition = new SourcePosition();

		public static void Break()
		{
		}

		public Debugger(BreakCallback onBreak)
		{
			this.onBreak = onBreak;
		}

		public void ClearBreakpoints()
		{
			breakpoints.count = 0;
		}

		public void AddBreakpoint(SourcePosition breakpoint)
		{
			breakpoints.PushBack(breakpoint);
		}

		public void OnGetSources(Buffer<Source> sources)
		{
			this.sources = sources;
		}

		public void OnDebugHook(VirtualMachine vm)
		{
			if (onBreak == null)
				return;

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

			for (var i = 0; i < breakpoints.count; i++)
			{
				var breakpoint = breakpoints.buffer[i];
				if (
					(lastPosition.uri.value != position.uri.value ||
						lastPosition.line != breakpoint.line) &&
					position.line == breakpoint.line
				)
				{
					var localVariables = GetLocalVariables(vm);
					onBreak(breakpoint, localVariables);
					break;
				}
			}

			lastPosition = position;
		}

		private static LocalVariable[] GetLocalVariables(VirtualMachine vm)
		{
			var topCallFrame = vm.callFrameStack.buffer[vm.callFrameStack.count - 1];
			if (topCallFrame.type != CallFrame.Type.Function)
				return new LocalVariable[0];

			var topDebugFrame = vm.debugData.frameStack.buffer[vm.debugData.frameStack.count - 1];
			var stackTypesBaseIndex = topDebugFrame.stackTypesBaseIndex + 1;

			var count = System.Math.Min(
				vm.debugData.stackTypes.count - stackTypesBaseIndex,
				vm.debugData.stackNames.count - topDebugFrame.stackNamesBaseIndex
			);
			var stackIndex = topCallFrame.baseStackIndex;

			var localVariables = new LocalVariable[count];
			var sb = new StringBuilder();

			for (var i = 0; i < count; i++)
			{
				var type = vm.debugData.stackTypes.buffer[stackTypesBaseIndex + i];
				var name = vm.debugData.stackNames.buffer[topDebugFrame.stackNamesBaseIndex + i];

				sb.Clear();
				type.Format(vm.chunk, sb);
				var typeString = sb.ToString();

				sb.Clear();
				VirtualMachineHelper.ValueToString(vm, stackIndex, type, sb);
				var valueString = sb.ToString();

				localVariables[i] = new LocalVariable(name, typeString, valueString);

				stackIndex += type.GetSize(vm.chunk);
			}

			return localVariables;
		}
	}
}