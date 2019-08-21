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

public sealed class VirtualMachine
{
	internal ByteCodeChunk chunk;
	internal Buffer<ValueData> valueStack = new Buffer<ValueData>(256);
	internal Buffer<CallFrame> callframeStack = new Buffer<CallFrame>(64);
	internal Buffer<object> heap;
	private Option<RuntimeError> maybeError;

	public void Load(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		maybeError = Option.None;

		valueStack.count = 0;
		callframeStack.count = 0;

		heap = new Buffer<object>
		{
			buffer = new object[chunk.stringLiterals.buffer.Length],
			count = chunk.stringLiterals.count
		};
		for (var i = 0; i < heap.count; i++)
			heap.buffer[i] = chunk.stringLiterals.buffer[i];
	}

	public void PushFunction(int functionIndex)
	{
		var function = chunk.functions.buffer[functionIndex];
		valueStack.PushBack(new ValueData(functionIndex));
		callframeStack.PushBack(new CallFrame(functionIndex, function.codeIndex, 1));
	}

	public Option<RuntimeError> CallTopFunction()
	{
		while (VirtualMachineInstructions.Tick(this)) { }
		return maybeError;
	}

	public Option<RuntimeError> CallTopFunctionDebug()
	{
		var sb = new StringBuilder();
		do
		{
			var ip = callframeStack.buffer[callframeStack.count - 1].codeIndex;
			sb.Clear();
			VirtualMachineHelper.TraceStack(this, sb);
			chunk.DisassembleInstruction(ip, sb);
			sb.AppendLine();
			System.Console.Write(sb);
		} while (VirtualMachineInstructions.Tick(this));

		return maybeError;
	}

	public bool Error(string message)
	{
		var ip = callframeStack.buffer[callframeStack.count - 1].codeIndex;
		maybeError = Option.Some(new RuntimeError(
			ip,
			ip >= 0 ? chunk.slices.buffer[ip] : new Slice(),
			message
		));
		return true;
	}
}