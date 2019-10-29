internal struct CallFrame
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

internal struct DebugData
{
	public Buffer<int> frameStack;
	public Buffer<ValueType> typeStack;

	public void Clear()
	{
		frameStack.count = 0;
		typeStack.count = 0;
	}
}

public sealed class VirtualMachine
{
	internal ByteCodeChunk chunk;
	internal Buffer<CallFrame> callframeStack = new Buffer<CallFrame>(64);
	internal Memory memory = new Memory(256);
	internal Buffer<object> nativeObjects;
	internal DebugData debugData = new DebugData();
	internal System.Action debugHook;
	internal Option<RuntimeError> error;

	internal void Load(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		error = Option.None;

		callframeStack.count = 0;
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
		if (callframeStack.count > 0)
			ip = callframeStack.buffer[callframeStack.count - 1].codeIndex;

		error = Option.Some(new RuntimeError(
			ip,
			ip >= 0 ? chunk.sourceSlices.buffer[ip] : new Slice(),
			message
		));
	}
}