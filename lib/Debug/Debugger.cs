namespace cflat
{
	public sealed class Debugger
	{
		public readonly struct Breakpoint
		{
			public readonly int sourceIndex;
			public readonly Slice slice;

			public Breakpoint(int sourceIndex, Slice slice)
			{
				this.sourceIndex = sourceIndex;
				this.slice = slice;
			}
		}

		private Buffer<Breakpoint> breakpoints = new Buffer<Breakpoint>(8);
		private Breakpoint lastPosition = new Breakpoint();

		public void Reset()
		{
			breakpoints.count = 0;
			lastPosition = new Breakpoint();
		}

		public void DebugHook(VirtualMachine vm)
		{
			var codeIndex = vm.callFrameStack.buffer[vm.callFrameStack.count - 1].codeIndex;
			if (codeIndex < 0)
				return;
			var sourceIndex = vm.chunk.FindSourceIndex(codeIndex);
			if (sourceIndex < 0)
				return;

			var sourceSlice = vm.chunk.sourceSlices.buffer[codeIndex];
			var position = new Breakpoint(sourceIndex, sourceSlice);

			for (var i = 0; i < breakpoints.count; i++)
			{
				var breakpoint = breakpoints.buffer[i];
				if (
					lastPosition.sourceIndex != position.sourceIndex ||
					!IsInsideSlice(lastPosition.slice.index, breakpoint.slice) &&
					IsInsideSlice(position.slice.index, breakpoint.slice)
				)
				{
					// INSPECT STATE
					break;
				}
			}
		}

		private static bool IsInsideSlice(ushort position, Slice slice)
		{
			return slice.index <= position && position <= slice.index + slice.length;
		}
	}
}