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
	internal Linking linking;
	internal Buffer<CallFrame> callframeStack = new Buffer<CallFrame>(64);
	internal Buffer<ValueData> valueStack = new Buffer<ValueData>(256);
	internal Buffer<ValueData> valueHeap = new Buffer<ValueData>(256);
	internal Buffer<object> nativeObjects;
	internal DebugData debugData = new DebugData();
	internal Option<RuntimeError> error;

	public void Load(Linking linking)
	{
		this.linking = linking;
		error = Option.None;

		callframeStack.count = 0;
		valueStack.count = 0;
		valueHeap.count = 0;

		nativeObjects = new Buffer<object>
		{
			buffer = new object[linking.byteCodeChunk.stringLiterals.buffer.Length],
			count = linking.byteCodeChunk.stringLiterals.count
		};
		for (var i = 0; i < nativeObjects.count; i++)
			nativeObjects.buffer[i] = linking.byteCodeChunk.stringLiterals.buffer[i];

		debugData.Clear();
	}

	public void Error(string message)
	{
		var ip = -1;
		if (callframeStack.count > 0)
			ip = callframeStack.buffer[callframeStack.count - 1].codeIndex;

		error = Option.Some(new RuntimeError(
			ip,
			ip >= 0 ? linking.byteCodeChunk.slices.buffer[ip] : new Slice(),
			message
		));
	}
}