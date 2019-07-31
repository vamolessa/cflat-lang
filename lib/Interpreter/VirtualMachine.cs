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
		public int baseIndex;
		public int instructionIndex;

		public CallFrame(int baseIndex, int instructionIndex)
		{
			this.baseIndex = baseIndex;
			this.instructionIndex = instructionIndex;
		}
	}

	internal ByteCodeChunk chunk;
	internal Buffer<ValueType> typeStack = new Buffer<ValueType>(256);
	internal Buffer<ValueData> valueStack = new Buffer<ValueData>(256);
	internal Buffer<CallFrame> callframeStack = new Buffer<CallFrame>(64);
	internal Buffer<object> heap;
	private Option<RuntimeError> maybeError;

	public Result<None, RuntimeError> Run(string source, ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		maybeError = Option.None;

		typeStack.count = 0;
		valueStack.count = 0;
		callframeStack.count = 0;

		callframeStack.PushBack(new CallFrame(0, 0));

		heap = new Buffer<object>
		{
			buffer = chunk.stringLiterals.buffer,
			count = chunk.stringLiterals.count
		};

		var sb = new StringBuilder();

		while (true)
		{
			{
				var ip = callframeStack.buffer[callframeStack.count - 1].instructionIndex;
				sb.Clear();
				VirtualMachineHelper.TraceStack(this, sb);
				chunk.PrintLineNumber(source, ip, sb);
				chunk.DisassembleInstruction(ip, sb);
				System.Console.Write(sb);
			}

			var done = VirtualMachineInstructions.Tick(this);
			if (done)
				break;
		}

		if (maybeError.isSome)
			return Result.Error(maybeError.value);

		return Result.Ok(new None());
	}

	public bool Error(string message)
	{
		var ip = callframeStack.buffer[callframeStack.count - 1].instructionIndex;
		maybeError = Option.Some(new RuntimeError(
			ip,
			chunk.slices.buffer[ip],
			message
		));
		return true;
	}

	public void PushValue(ValueData value, ValueType type)
	{
		typeStack.PushBack(type);
		valueStack.PushBack(value);
	}

	public ValueData PopValue()
	{
		typeStack.count -= 1;
		return valueStack.PopLast();
	}

	public ref ValueData Peek()
	{
		return ref valueStack.buffer[valueStack.count - 1];
	}

	public ref ValueData PeekBefore()
	{
		return ref valueStack.buffer[valueStack.count - 2];
	}

	public ValueType PeekType()
	{
		return typeStack.buffer[typeStack.count - 1];
	}
}