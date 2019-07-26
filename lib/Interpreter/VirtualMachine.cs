using System.Text;

public readonly struct RuntimeError
{
	public readonly int instructionIndex;
	public readonly int sourceIndex;
	public readonly string message;

	public RuntimeError(int instructionIndex, int sourceIndex, string message)
	{
		this.instructionIndex = instructionIndex;
		this.sourceIndex = sourceIndex;
		this.message = message;
	}
}

public sealed class VirtualMachine
{
	internal string source;
	internal ByteCodeChunk chunk;
	internal int programCount;
	internal Buffer<Value> stack = new Buffer<Value>(256);
	internal Buffer<object> heap;
	private Option<RuntimeError> maybeError;

	public Result<None, RuntimeError> Run(string source, ByteCodeChunk chunk)
	{
		this.source = source;
		this.chunk = chunk;
		maybeError = Option.None;

		programCount = 0;
		stack.count = 0;

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
			programCount,
			chunk.sourceIndexes.buffer[programCount],
			message
		));
		return true;
	}

	public void PushValue(Value value)
	{
		stack.PushBack(value);
	}

	public Value PopValue()
	{
		return stack.PopLast();
	}
}