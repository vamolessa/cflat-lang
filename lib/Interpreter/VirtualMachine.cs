namespace cflat
{
	public struct CallFrame
	{
		public enum Type : ushort
		{
			EntryPoint,
			Function,
			NativeFunction,
		}

		public int codeIndex;
		public int baseStackIndex;
		public ushort functionIndex;
		public Type type;

		public CallFrame(int codeIndex, int baseStackIndex, ushort functionIndex, Type type)
		{
			this.codeIndex = codeIndex;
			this.baseStackIndex = baseStackIndex;
			this.functionIndex = functionIndex;
			this.type = type;
		}
	}

	public struct DebugData
	{
		public readonly struct Frame
		{
			public readonly ushort stackTypesBaseIndex;
			public readonly ushort stackNamesBaseIndex;

			public Frame(ushort typeStackBaseIndex, ushort localVariableNamesBaseIndex)
			{
				this.stackTypesBaseIndex = typeStackBaseIndex;
				this.stackNamesBaseIndex = localVariableNamesBaseIndex;
			}
		}

		public Buffer<Frame> frameStack;
		public Buffer<ValueType> stackTypes;
		public Buffer<string> stackNames;

		public void Clear()
		{
			frameStack.count = 0;
			stackTypes.count = 0;
			stackNames.count = 0;
		}
	}

	public sealed class VirtualMachine
	{
		public ByteCodeChunk chunk;
		public Buffer<CallFrame> callFrameStack = new Buffer<CallFrame>(64);
		public Memory memory = new Memory(256);
		public Buffer<object> nativeObjects;
		public DebugData debugData = new DebugData();
		internal Option<IDebugger> debugger;
		internal Option<RuntimeError> error;

		internal void Load(ByteCodeChunk chunk)
		{
			this.chunk = chunk;
			error = Option.None;

			callFrameStack.count = 0;
			memory.Reset();

			nativeObjects = new Buffer<object>
			{
				buffer = new object[chunk.stringLiterals.buffer.Length],
				count = chunk.stringLiterals.count
			};
			for (var i = 0; i < nativeObjects.count; i++)
				nativeObjects.buffer[i] = chunk.stringLiterals.buffer[i];

			debugData.Clear();
		}

		public void Error(string message)
		{
			var ip = -1;
			if (callFrameStack.count > 0)
				ip = callFrameStack.buffer[callFrameStack.count - 1].codeIndex;

			error = Option.Some(new RuntimeError(
				ip,
				ip >= 0 ? chunk.sourceSlices.buffer[ip] : new Slice(),
				message
			));
		}
	}
}