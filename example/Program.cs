public sealed class Program
{
	public static void Main(string[] args)
	{
		var chunk = new ByteCodeChunk();
		var const1 = chunk.AddConstant(new Value(1));
		var const4 = chunk.AddConstant(new Value(4));

		chunk.WriteInstruction(Instruction.LoadConstant, new LineAndColumn(123, 0));
		chunk.WriteConstantIndex(const1, new LineAndColumn(123, 0));
		chunk.WriteInstruction(Instruction.LoadConstant, new LineAndColumn(123, 0));
		chunk.WriteConstantIndex(const4, new LineAndColumn(123, 0));
		chunk.WriteInstruction(Instruction.Add, new LineAndColumn(123, 0));
		chunk.WriteInstruction(Instruction.Return, new LineAndColumn(123, 0));

		var vm = new VirtualMachine();
		vm.Load(chunk);
		vm.Run(true);
	}
}
