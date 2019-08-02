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
	public readonly struct Return
	{
		public readonly ValueData value;
		public readonly ValueType type;

		public Return(ValueData value, ValueType type)
		{
			this.value = value;
			this.type = type;
		}
	}

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

	public Result<Return, RuntimeError> Run(ByteCodeChunk chunk, string functionName)
	{
		this.chunk = chunk;
		maybeError = Option.None;

		typeStack.count = 0;
		valueStack.count = 0;
		callframeStack.count = 0;

		for (var i = 0; i < chunk.functions.count; i++)
		{
			var function = chunk.functions.buffer[i];
			if (function.name == functionName)
			{
				callframeStack.PushBack(new CallFrame(i, function.codeIndex, 1));
				break;
			}
		}

		if (callframeStack.count == 0)
		{
			return Result.Error(new RuntimeError(
				0,
				new Slice(),
				"Could not find 'main' function"
			));
		}

		heap = new Buffer<object>
		{
			buffer = chunk.stringLiterals.buffer,
			count = chunk.stringLiterals.count
		};

		var sb = new StringBuilder();

		while (true)
		{
			{
				var ip = callframeStack.buffer[callframeStack.count - 1].codeIndex;
				sb.Clear();
				VirtualMachineHelper.TraceStack(this, sb);
				chunk.DisassembleInstruction(ip, sb);
				System.Console.Write(sb);
			}

			var done = VirtualMachineInstructions.Tick(this);
			if (done)
				break;
		}

		if (maybeError.isSome)
			return Result.Error(maybeError.value);

		var type = PeekType();
		var value = PopValue();
		return Result.Ok(new Return(value, type));
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