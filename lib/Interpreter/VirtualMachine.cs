using System.Text;

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

public sealed class VirtualMachine
{
	internal struct CallFrame
	{
		public int functionIndex;
		public int codeIndex;
		public int baseStackIndex;

		public CallFrame(int functionIndex, int codeIndex, int baseStackIndex)
		{
			this.functionIndex = functionIndex;
			this.codeIndex = codeIndex;
			this.baseStackIndex = baseStackIndex;
		}
	}

	internal ByteCodeChunk chunk;
	internal Buffer<ValueType> typeStack = new Buffer<ValueType>(256);
	internal Buffer<ValueData> valueStack = new Buffer<ValueData>(256);
	internal Buffer<CallFrame> callframeStack = new Buffer<CallFrame>(64);
	internal Buffer<object> heap;
	private Option<RuntimeError> maybeError;

	public Option<RuntimeError> RunLastFunction(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		maybeError = Option.None;

		typeStack.count = 0;
		valueStack.count = 0;
		callframeStack.count = 0;

		if (chunk.functions.count == 0)
		{
			return Option.Some(new RuntimeError(
				0,
				new Slice(),
				"No function defined"
			));
		}

		var functionIndex = chunk.functions.count - 1;
		var function = chunk.functions.buffer[functionIndex];
		valueStack.PushBack(new ValueData(functionIndex));
		typeStack.PushBack(new ValueType(ValueKind.Function, function.typeIndex));
		callframeStack.PushBack(new CallFrame(functionIndex, function.codeIndex, 1));

		heap = new Buffer<object>
		{
			buffer = new object[chunk.stringLiterals.buffer.Length],
			count = chunk.stringLiterals.count
		};
		for (var i = 0; i < heap.count; i++)
			heap.buffer[i] = chunk.stringLiterals.buffer[i];

		var sb = new StringBuilder();

		while (true)
		{
			{
				var ip = callframeStack.buffer[callframeStack.count - 1].codeIndex;
				sb.Clear();
				VirtualMachineHelper.TraceStack(this, sb);
				chunk.DisassembleInstruction(ip, sb);
				//System.Console.Write(sb);
			}

			var done = VirtualMachineInstructions.Tick(this);
			if (done)
				break;
		}

		return maybeError;
	}

	public bool Error(string message)
	{
		var ip = callframeStack.buffer[callframeStack.count - 1].codeIndex;
		maybeError = Option.Some(new RuntimeError(
			ip,
			chunk.slices.buffer[ip],
			message
		));
		return true;
	}
}