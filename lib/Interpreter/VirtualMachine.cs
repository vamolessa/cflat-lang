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
	internal string source;
	internal ByteCodeChunk chunk;
	internal int programCount;
	internal int previousProgramCount;
	internal Buffer<ValueType> typeStack = new Buffer<ValueType>(256);
	internal Buffer<ValueData> valueStack = new Buffer<ValueData>(256);
	internal Buffer<object> heap;
	private Option<RuntimeError> maybeError;

	public Result<None, RuntimeError> Run(string source, ByteCodeChunk chunk)
	{
		this.source = source;
		this.chunk = chunk;
		maybeError = Option.None;

		programCount = 0;
		previousProgramCount = 0;
		typeStack.count = 0;
		valueStack.count = 0;

		heap = new Buffer<object>
		{
			buffer = chunk.stringLiterals.buffer,
			count = chunk.stringLiterals.count
		};

		var sb = new StringBuilder();

		while (true)
		{
			sb.Clear();
			VirtualMachineHelper.TraceStack(this, sb);
			chunk.DisassembleInstruction(source, programCount, sb);
			System.Console.Write(sb);

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
		maybeError = Option.Some(new RuntimeError(
			previousProgramCount,
			chunk.slices.buffer[previousProgramCount],
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