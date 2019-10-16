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
	public enum Type : byte
	{
		EntryPoint,
		Function,
		NativeFunction,
	}

	public int codeIndex;
	public int baseStackIndex;
	public ushort functionIndex;
	public byte chunkIndex;
	public Type type;

	public CallFrame(byte chunkIndex, int codeIndex, int baseStackIndex, ushort functionIndex, Type type)
	{
		this.chunkIndex = chunkIndex;
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
	internal Buffer<object> nativeObjects = new Buffer<object>();
	internal DebugData debugData = new DebugData();
	internal Option<RuntimeError> error;

	public void Load(Linking linking)
	{
		this.linking = linking;
		error = Option.None;

		callframeStack.count = 0;
		valueStack.count = 0;
		valueHeap.count = 0;
		nativeObjects.count = 0;

		for (var i = 0; i < linking.chunks.count; i++)
		{
			var chunk = linking.chunks.buffer[i];
			var baseIndex = nativeObjects.count;
			nativeObjects.Grow(chunk.stringLiterals.count);
			for (var j = 0; j < chunk.stringLiterals.count; j++)
				nativeObjects.buffer[baseIndex + j] = chunk.stringLiterals.buffer[j];
		}

		debugData.Clear();
	}

	public void Error(string message)
	{
		var ip = -1;
		var chunkIndex = -1;
		if (callframeStack.count > 0)
		{
			var callframe = callframeStack.buffer[callframeStack.count - 1];
			ip = callframe.codeIndex;
			chunkIndex = callframe.chunkIndex;
		}

		error = Option.Some(new RuntimeError(
			ip,
			ip >= 0 && chunkIndex >= 0 ?
				linking.chunks.buffer[chunkIndex].slices.buffer[ip] :
				new Slice(),
			message
		));
	}
}