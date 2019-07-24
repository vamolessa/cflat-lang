using System.Text;

public sealed class VirtualMachine
{
	internal ByteCodeChunk chunk;
	internal int programCount;
	internal Buffer<Value> stack = new Buffer<Value>(256);

	public void Load(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
	}

	public bool Run(bool debug)
	{
		programCount = 0;
		stack.count = 0;

		StringBuilder sb = null;
		if (debug)
			sb = new StringBuilder();

		while (true)
		{
			if (debug)
			{
				sb.Clear();
				VirtualMachineHelper.TraceStack(this, sb);
				chunk.DisassembleInstruction(programCount, sb);
				System.Console.Write(sb);
			}

			var instruction = VirtualMachineHelper.NextInstruction(this);
			switch ((Instruction)instruction)
			{
			case Instruction.Return:
				var value = PopValue();
				System.Console.WriteLine(value.ToString());
				return true;
			case Instruction.LoadConstant:
				PushValue(VirtualMachineHelper.ReadConstant(this));
				break;
			default:
				break;
			}
		}
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