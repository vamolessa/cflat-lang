public readonly struct RuntimeError
{
	public readonly int instructionIndex;
	public readonly Slice slice;
	public readonly string message;

	public RuntimeError(int instructionIndex, Slice slice, string message)
	{
		this.instructionIndex = instructionIndex;
		this.slice = slice;
		this.message = message;
	}
}

internal struct CallFrame
{
	public enum Type : ushort
	{
		EntryPoint,
		Function,
		NativeFunction,
		AutoNativeFunction,
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
	public Buffer<ValueType> typeStack;
	public Buffer<ushort> baseTypeStackIndex;
}

public sealed class VirtualMachine
{
	internal ByteCodeChunk chunk;
	internal Buffer<ValueData> valueStack = new Buffer<ValueData>(256);
	internal Buffer<CallFrame> callframeStack = new Buffer<CallFrame>(64);
	internal DebugData debugData = new DebugData();
	internal Buffer<object> nativeObjects;
	internal Option<RuntimeError> error;

	public void Load(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		error = Option.None;

		valueStack.count = 0;
		callframeStack.count = 0;

		nativeObjects = new Buffer<object>
		{
			buffer = new object[chunk.stringLiterals.buffer.Length],
			count = chunk.stringLiterals.count
		};
		for (var i = 0; i < nativeObjects.count; i++)
			nativeObjects.buffer[i] = chunk.stringLiterals.buffer[i];
	}

	public void Error(string message)
	{
		var ip = -1;
		if (callframeStack.count > 0)
			ip = callframeStack.buffer[callframeStack.count - 1].codeIndex;

		error = Option.Some(new RuntimeError(
			ip,
			ip >= 0 ? chunk.slices.buffer[ip] : new Slice(),
			message
		));
	}
}