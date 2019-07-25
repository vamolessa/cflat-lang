using System.Text;
using Impl = VirtualMachineInstructions;

public sealed class VirtualMachine
{
	internal string source;
	internal ByteCodeChunk chunk;
	internal int programCount;
	internal Buffer<Value> stack = new Buffer<Value>(256);

	public bool Run(string source, ByteCodeChunk chunk)
	{
		this.source = source;
		this.chunk = chunk;

		programCount = 0;
		stack.count = 0;

		var sb = new StringBuilder();

		while (true)
		{
			sb.Clear();
			VirtualMachineHelper.TraceStack(this, sb);
			chunk.DisassembleInstruction(source, programCount, sb);
			System.Console.Write(sb);

			var done = Tick();
			if (done)
				return true;
		}
	}

	private bool Tick()
	{
		var instruction = VirtualMachineHelper.NextInstruction(this);

		switch (instruction)
		{
		case Instruction.Return: Impl.Return(this); return true;
		case Instruction.LoadConstant: Impl.LoadConstant(this); break;
		case Instruction.Negate: Impl.Negate(this); break;
		case Instruction.Add: Impl.Add(this); break;
		case Instruction.Subtract: Impl.Subtract(this); break;
		case Instruction.Multiply: Impl.Multiply(this); break;
		case Instruction.Divide: Impl.Divide(this); break;
		default:
			break;
		}

		return false;
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