using System.Collections.Generic;
using System.Text;

namespace cflat
{
	public sealed class Debugger
	{
		public delegate void BreakCallback(Breakpoint breakpoint, Dictionary<string, string> localVariables);

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

		private readonly BreakCallback onBreak;
		private Buffer<Breakpoint> breakpoints = new Buffer<Breakpoint>(8);
		private Breakpoint lastPosition = new Breakpoint();

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

		public void AddBreakpoint(Breakpoint breakpoint)
		{
			breakpoints.PushBack(breakpoint);
		}

		public void DebugHook(VirtualMachine vm)
		{
			if (onBreak == null)
				return;

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
					var localVariables = GetLocalVariables(vm);
					onBreak(breakpoint, localVariables);
					break;
				}
			}

			lastPosition = position;
		}

		private static bool IsInsideSlice(ushort position, Slice slice)
		{
			return slice.index <= position && position <= slice.index + slice.length;
		}

		private static Dictionary<string, string> GetLocalVariables(VirtualMachine vm)
		{
			var stackIndex = vm.callFrameStack.buffer[vm.callFrameStack.count - 1].baseStackIndex;
			var topDebugFrame = vm.debugData.frameStack.buffer[vm.debugData.frameStack.count - 1];

			var count = System.Math.Min(
				vm.debugData.typeStack.count - topDebugFrame.typeStackBaseIndex,
				vm.debugData.localVariableNames.count - topDebugFrame.localVariableNamesBaseIndex
			);

			var localVariables = new Dictionary<string, string>(count);
			var sb = new StringBuilder();

			for (var i = 0; i < count; i++)
			{
				var type = vm.debugData.typeStack.buffer[topDebugFrame.typeStackBaseIndex + i];
				var name = vm.debugData.localVariableNames.buffer[topDebugFrame.localVariableNamesBaseIndex + i];

				sb.Clear();
				VirtualMachineHelper.ValueToString(vm, stackIndex, type, sb);

				localVariables.Add(name, sb.ToString());

				stackIndex += type.GetSize(vm.chunk);
			}

			return localVariables;
		}
	}
}