using System.Text;

public sealed class VirtualMachine
{
	internal string source;
	internal ByteCodeChunk chunk;
	internal int programCount;
	internal Buffer<Value> stack = new Buffer<Value>(256);
	private string errorMessage;

	public Result<None, string> Run(string source, ByteCodeChunk chunk)
	{
		this.source = source;
		this.chunk = chunk;
		errorMessage = null;

		programCount = 0;
		stack.count = 0;

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

		if (string.IsNullOrEmpty(errorMessage))
			return Result.Ok(new None());

		return Result.Error(errorMessage);
	}

	public bool Error(string message)
	{
		errorMessage = message;
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